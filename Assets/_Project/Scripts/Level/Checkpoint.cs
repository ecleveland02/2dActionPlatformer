using Margin.Core;
using Margin.FX;
using Margin.Player;
using UnityEngine;

namespace Margin.Level
{
    /// <summary>
    /// An ink pot checkpoint (spec 11.4): touching it makes it your respawn point and restores your health.
    /// Saving and weapon swapping come with the save system (Milestone 6). Place one before every boss and roughly
    /// every 3 to 4 rooms. The object sits on the floor; the player respawns beside it (respawnOffset).
    /// </summary>
    public sealed class Checkpoint : MonoBehaviour, ITickable
    {
        [Tooltip("Saved games remember the last ink pot by this. Empty = the room's title (fine with one pot per room).")]
        [SerializeField] private string id;
        [Tooltip("Touch area: this wide and tall, standing on the object's position.")]
        [SerializeField] private Vector2 size = new Vector2(1.6f, 2.2f);
        [Tooltip("Where the player's center respawns, relative to this object. Outside the touch area, so " +
                 "respawning doesn't count as touching it again.")]
        [SerializeField] private Vector2 respawnOffset = new Vector2(1.3f, 0.95f);
        [SerializeField] private InkPotVisual visual;
        [Tooltip("Frames the 'health restored' note stays up.")]
        [SerializeField, Min(0)] private int messageFrames = 90;

        private static Checkpoint current;
        private PlayerController player;
        private bool touching;
        private int messageLeft;

        /// <summary>After the LevelDirector (30).</summary>
        public int TickOrder => 31;

        /// <summary>Stable id for save files.</summary>
        public string Id
        {
            get
            {
                if (!string.IsNullOrEmpty(id)) return id;
                Room room = Room.Of(this);
                return room != null ? room.Title : name;
            }
        }

        public Vector2 RespawnPoint => (Vector2)transform.position + respawnOffset;
        public bool IsCurrent => current == this;

        public Rect Area
        {
            get
            {
                Vector2 p = transform.position;
                return new Rect(p.x - size.x * 0.5f, p.y, size.x, size.y);
            }
        }

        public InkPotVisual Visual
        {
            get => visual;
            set => visual = value;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => current = null;   // statics survive between plays without domain reload

        private void OnEnable()
        {
            GameLoop.Register(this);
            if (visual != null) visual.SetLit(IsCurrent);
        }

        private void OnDisable()
        {
            GameLoop.Unregister(this);
            touching = false;
            messageLeft = 0;
            if (visual != null) visual.ShowMessage(false);
        }

        public void Tick()
        {
            if (player == null)
            {
                LevelDirector director = LevelDirector.Instance;
                player = director != null && director.Player != null ? director.Player : SceneQuery.FindFirst<PlayerController>();
                if (player == null) return;
            }

            Vector2 bodySize = player.Body.Size;
            var body = new Rect(player.Body.Position - bodySize * 0.5f, bodySize);
            bool inside = Area.Overlaps(body) && !(player.CurrentState is DefeatedState);
            if (inside && !touching) Activate();
            touching = inside;

            if (messageLeft > 0 && --messageLeft == 0 && visual != null) visual.ShowMessage(false);
        }

        /// <summary>Becomes the respawn point and refills the player's health.</summary>
        public void Activate()
        {
            PlayerHealth health = player != null ? player.Health : null;
            if (health != null)
            {
                health.SpawnPoint = RespawnPoint;
                health.Health.Heal(health.Health.Max);
            }

            Checkpoint previous = current;
            current = this;
            if (previous != null && previous != this && previous.visual != null) previous.visual.SetLit(false);
            if (visual != null)
            {
                visual.SetLit(true);
                visual.ShowMessage(messageFrames > 0);
            }
            messageLeft = messageFrames;

            if (LevelDirector.Instance != null) LevelDirector.Instance.SetCheckpoint(this);
            if (InkSplatter.Instance != null) InkSplatter.Instance.Burst((Vector2)transform.position + new Vector2(0f, 0.8f), Vector2.up, 12);
            LevelEvents.RaiseCheckpointReached(this);
        }

        /// <summary>
        /// Continuing a saved game: this is the current ink pot again, quietly (no heal note, no splash, no save).
        /// </summary>
        public void Restore(PlayerController target)
        {
            player = target;
            touching = true;   // the player starts inside it; don't count that as touching it again
            Checkpoint previous = current;
            current = this;
            if (previous != null && previous != this && previous.visual != null) previous.visual.SetLit(false);
            if (visual != null) visual.SetLit(true);
            if (target != null && target.Health != null) target.Health.SpawnPoint = RespawnPoint;
            if (LevelDirector.Instance != null) LevelDirector.Instance.SetCheckpoint(this);
        }

        private void OnDrawGizmos()
        {
            // The jar's outline, so the pot can be placed in the editor before its lines exist.
            Gizmos.color = new Color(0.1f, 0.1f, 0.1f, 0.9f);
            Vector3 p = transform.position;
            Vector2[] half = InkPotVisual.JarHalf;
            for (int i = 0; i < half.Length - 1; i++)
            {
                Gizmos.DrawLine(p + (Vector3)half[i], p + (Vector3)half[i + 1]);
                Gizmos.DrawLine(p + new Vector3(-half[i].x, half[i].y), p + new Vector3(-half[i + 1].x, half[i + 1].y));
            }
            Gizmos.color = new Color(0.2f, 0.9f, 0.3f, 0.6f);
            Rect a = Area;
            Gizmos.DrawWireCube(a.center, a.size);
            Gizmos.DrawWireSphere(RespawnPoint, 0.2f);
        }
    }
}
