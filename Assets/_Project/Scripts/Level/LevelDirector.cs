using System.Collections.Generic;
using Margin.Core;
using Margin.Input;
using Margin.Player;
using UnityEngine;

namespace Margin.Level
{
    /// <summary>
    /// Runs a world made of rooms (spec 11): one per scene. Only the current room is active. Each tick, after the
    /// player moves, it checks the current room's doors and its pit line.
    ///
    ///   Door: fade to paper while the player keeps walking out, switch rooms, put the player at the target door's
    ///         arrival point already walking in, fade back. The player can't be hurt meanwhile.
    ///   Pit:  falling below the room costs LevelSettings.pitDamage, then the player is put back at the door they
    ///         came in by (or the checkpoint, if they respawned in this room).
    ///   Death: the screen fades out over the end of the defeat animation; PlayerHealth.Respawn puts the player at
    ///         the last ink pot and this switches to the checkpoint's room and fades back in.
    /// Everything counts ticks, so transitions pause and frame-step with the game.
    /// </summary>
    public sealed class LevelDirector : MonoBehaviour, ITickable
    {
        [SerializeField] private LevelSettings settings;
        [Tooltip("The room the scene starts in. Empty = the room containing the player.")]
        [SerializeField] private Room startRoom;
        [Tooltip("Empty = the first player in the scene.")]
        [SerializeField] private PlayerController player;

        private readonly TransitionTimer timer = new TransitionTimer();
        private readonly List<Room> rooms = new List<Room>();
        private RoomDoor arrivalDoor;      // set while fading out through a door
        private bool pitPending;           // fading out after a pit fall
        private bool respawnPending;       // PlayerHealth.Respawn happened; switch rooms on the next tick
        private int entryWalkLeft;
        private Vector2 safePoint;         // where a pit fall puts the player back

        public static LevelDirector Instance { get; private set; }

        /// <summary>After the player (0), their health (12) and enemies (22) have moved this tick.</summary>
        public int TickOrder => 30;

        public LevelSettings Settings => settings != null ? settings : LevelSettings.Defaults;
        public PlayerController Player => player;
        public Room CurrentRoom { get; private set; }
        /// <summary>The room of the last ink pot touched (the start room until then). Respawns go here.</summary>
        public Room CheckpointRoom { get; private set; }
        public IReadOnlyList<Room> Rooms => rooms;
        /// <summary>A door, pit or respawn is fading the screen or walking the player in.</summary>
        public bool InTransition => timer.Active || entryWalkLeft > 0;

        /// <summary>How much of the screen is covered in paper: 0 clear, 1 covered (read by the UI).</summary>
        public float Fade
        {
            get
            {
                if (timer.Active) return timer.Fade;
                // Defeat: fade out over the last deathFadeFrames of the animation, so the respawn is hidden.
                if (player != null && player.CurrentState is DefeatedState && player.Combat != null)
                {
                    int deathFrames = player.Combat.Settings.playerDeathFrames;
                    int fadeFrames = Mathf.Min(Settings.deathFadeFrames, deathFrames);
                    return Mathf.Clamp01((player.FramesInState - (deathFrames - fadeFrames)) / (float)fadeFrames);
                }
                return 0f;
            }
        }

        public void Configure(LevelSettings levelSettings, Room start, PlayerController target)
        {
            settings = levelSettings;
            startRoom = start;
            player = target;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogError("Only one LevelDirector may exist. Disabling the duplicate.", this);
                enabled = false;
                return;
            }
            Instance = this;
        }

        private void OnEnable()
        {
            GameLoop.Register(this);
            PlayerEvents.Respawned += OnRespawned;
        }

        private void OnDisable()
        {
            GameLoop.Unregister(this);
            PlayerEvents.Respawned -= OnRespawned;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Start() => Begin();

        /// <summary>Finds the rooms, switches off all but the starting one and enters it. Called on Start (tests call it directly).</summary>
        public void Begin()
        {
            if (player == null) player = SceneQuery.FindFirst<PlayerController>();
            rooms.Clear();
            rooms.AddRange(SceneQuery.FindAll<Room>());
            if (rooms.Count == 0)
            {
                Debug.LogWarning("LevelDirector: no Room in the scene, nothing to do.", this);
                return;
            }

            Room start = startRoom;
            if (start == null && player != null) start = RoomAt(player.transform.position);
            if (start == null) start = rooms[0];

            // Only the starting room runs; the rest wake up when entered.
            foreach (Room room in rooms)
                if (room != start) room.gameObject.SetActive(false);
            CheckpointRoom = start;
            safePoint = player != null ? (Vector2)player.transform.position : start.WorldBounds.center;
            EnterRoom(start, resetContents: false);
        }

        /// <summary>The room whose camera bounds contain a point, or null.</summary>
        public Room RoomAt(Vector2 point)
        {
            foreach (Room room in rooms)
                if (room != null && room.Contains(point)) return room;
            return null;
        }

        /// <summary>An ink pot was touched: respawns now happen in its room.</summary>
        public void SetCheckpoint(Checkpoint checkpoint)
        {
            Room room = Room.Of(checkpoint);
            if (room != null) CheckpointRoom = room;
            safePoint = checkpoint.RespawnPoint;
        }

        /// <summary>Where a death respawns the player (room + point). Boss arenas move it for quick retries.</summary>
        public Vector2 RespawnPoint => player != null && player.Health != null ? player.Health.SpawnPoint : safePoint;

        /// <summary>Makes deaths respawn at this point in this room (e.g. a boss arena's retry spot).</summary>
        public void SetRespawn(Room room, Vector2 point)
        {
            if (room != null) CheckpointRoom = room;
            if (player != null && player.Health != null) player.Health.SpawnPoint = point;
        }

        // ---------------- tick ----------------

        public void Tick()
        {
            if (player == null || CurrentRoom == null) return;

            if (respawnPending)
            {
                FinishRespawn();
                return;
            }

            if (timer.Active)
            {
                if (timer.Tick()) OnScreenCovered();
            }

            if (entryWalkLeft > 0 && --entryWalkLeft == 0) ReleaseControls();
            if (InTransition) return;

            if (player.CurrentState is DefeatedState) return;
            CheckDoors();
            if (!timer.Active) CheckPit();
        }

        private void CheckDoors()
        {
            Rect body = PlayerRect();
            foreach (RoomDoor door in CurrentRoom.Doors)
            {
                if (door == null || door.Target == null || !door.isActiveAndEnabled) continue;
                if (!door.Area.Overlaps(body)) continue;
                LeaveThrough(door);
                return;
            }
        }

        private void CheckPit()
        {
            if (player.Body.Position.y >= CurrentRoom.WorldBounds.yMin - Settings.killPlaneMargin) return;

            bool died = player.Health != null && player.Health.TakeHazardDamage(Settings.pitDamage, "a pit");
            if (died) return;   // the defeat fade and respawn take over
            pitPending = true;
            Protect(true);
            Script(Vector2.zero, false);
            timer.Start(Settings.pitFadeOutFrames, Settings.fadeInFrames);
        }

        private void LeaveThrough(RoomDoor door)
        {
            arrivalDoor = door.Target;
            Protect(true);
            // Keep walking out (or keep rising through a hole in the ceiling) while the screen fades.
            switch (door.DoorSide)
            {
                case RoomDoor.Side.Left: Script(Vector2.left, false); break;
                case RoomDoor.Side.Right: Script(Vector2.right, false); break;
                case RoomDoor.Side.Top: Script(new Vector2(0f, 1f), true); break;
                default: Script(Vector2.zero, false); break;
            }
            timer.Start(Settings.fadeOutFrames, Settings.fadeInFrames);
        }

        /// <summary>The screen is fully covered: move the player and switch rooms unseen.</summary>
        private void OnScreenCovered()
        {
            if (arrivalDoor != null)
            {
                RoomDoor door = arrivalDoor;
                arrivalDoor = null;
                if (door.Room != null && door.Room != CurrentRoom) EnterRoom(door.Room, resetContents: true);
                Arrive(door);
            }
            else if (pitPending)
            {
                pitPending = false;
                player.ResetTo(safePoint);
                ReleaseControls();
                LevelEvents.RaiseCameraCut();
            }
        }

        private void Arrive(RoomDoor door)
        {
            MovementData data = player.Data;
            player.ResetTo(door.ArrivalPoint);
            safePoint = door.SafePoint;
            int walk = Settings.entryWalkFrames;

            switch (door.DoorSide)
            {
                case RoomDoor.Side.Left:
                case RoomDoor.Side.Right:
                    int dir = door.InwardDirection;
                    player.Facing = dir;
                    player.Velocity = new Vector2(dir * data.runSpeed, 0f);
                    Script(new Vector2(dir, 0f), false);
                    break;
                case RoomDoor.Side.Bottom:
                    // Coming up through a hole in the floor: pop up and drift onto the ledge beside it.
                    int nudge = door.ArrivalNudge;
                    if (nudge != 0) player.Facing = nudge;
                    player.Velocity = new Vector2(nudge * data.runSpeed * 0.6f, data.JumpVelocity * Settings.upwardEntryBoost);
                    Script(new Vector2(nudge, 0f), true);
                    walk = Mathf.Max(walk, data.framesToApex);
                    break;
                default:
                    // Dropping in from above: just fall.
                    player.Velocity = Vector2.zero;
                    Script(Vector2.zero, false);
                    break;
            }

            entryWalkLeft = Mathf.Max(1, walk);
            LevelEvents.RaiseCameraCut();
        }

        // ---------------- respawn ----------------

        private void OnRespawned(PlayerController who)
        {
            if (who != player) return;
            // PlayerHealth has already moved the player. Switch rooms on the next tick, outside this event.
            respawnPending = true;
            arrivalDoor = null;
            pitPending = false;
            entryWalkLeft = 0;
            ReleaseControls();
            timer.Cancel();
        }

        private void FinishRespawn()
        {
            respawnPending = false;
            Room target = CheckpointRoom != null ? CheckpointRoom : CurrentRoom;
            if (target != CurrentRoom) EnterRoom(target, resetContents: true);
            if (player.Health != null) safePoint = player.Health.SpawnPoint;
            timer.StartIn(Settings.respawnFadeInFrames);
            LevelEvents.RaiseCameraCut();
        }

        // ---------------- helpers ----------------

        private void EnterRoom(Room next, bool resetContents)
        {
            if (CurrentRoom != null && CurrentRoom != next) CurrentRoom.gameObject.SetActive(false);
            next.gameObject.SetActive(true);
            if (resetContents) next.ResetContents();
            CurrentRoom = next;
            LevelEvents.RaiseRoomEntered(next);
            LevelEvents.RaiseCameraCut();
        }

        private Rect PlayerRect()
        {
            Vector2 size = player.Body.Size;
            return new Rect(player.Body.Position - size * 0.5f, size);
        }

        private void Script(Vector2 move, bool jumpHeld)
        {
            if (player.Controls is IScriptedInput scripted) scripted.Script(move, jumpHeld);
        }

        private void ReleaseControls()
        {
            if (player.Controls is IScriptedInput scripted) scripted.ClearScript();
            if (!timer.Active || timer.Phase == TransitionPhase.In) Protect(false);
        }

        private void Protect(bool on)
        {
            if (player.Health != null) player.Health.Protected = on;
        }
    }
}
