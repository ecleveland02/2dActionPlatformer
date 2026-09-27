using Margin.Core;
using Margin.FX;
using Margin.Level;
using Margin.Player;
using UnityEngine;

namespace Margin.Bosses
{
    /// <summary>
    /// A boss room's rules (spec 10, 11). The fight starts when the player walks past triggerX: the entrance
    /// seals and the boss wakes. Dying during the fight respawns the player just inside the arena, so a retry
    /// takes under 3 seconds (spec 10: instant retry) and the intro is short the second time. Beating the boss
    /// opens the exit seal, shows the reward (the Grapple Line pickup) and gives back the ink pot respawn.
    /// Put it on the arena's Room object. Positions are relative to this object.
    /// </summary>
    public sealed class BossArena : MonoBehaviour, ITickable, IRoomReset
    {
        [SerializeField] private BossBase boss;
        [Tooltip("The fight starts when the player's center passes this x (relative to this object).")]
        [SerializeField] private float triggerX = 5f;
        [Tooltip("A wall that closes the way in while the fight is on (starts switched off).")]
        [SerializeField] private GameObject entranceSeal;
        [Tooltip("A wall that blocks the way on until the boss is beaten.")]
        [SerializeField] private GameObject exitSeal;
        [Tooltip("Appears when the boss is beaten (e.g. the ability pickup).")]
        [SerializeField] private GameObject reward;
        [Tooltip("Where a death during the fight respawns the player (relative), before triggerX.")]
        [SerializeField] private Vector2 retryPoint = new Vector2(2f, 0.95f);

        private bool fighting, beaten, holdingRespawn;
        private Room savedRoom;
        private Vector2 savedPoint;

        /// <summary>After the boss (22) and the LevelDirector (30).</summary>
        public int TickOrder => 32;
        public bool Fighting => fighting;
        public bool Beaten => beaten;
        public BossBase Boss => boss;

        public void Configure(BossBase arenaBoss, float trigger, GameObject entrance, GameObject exit, GameObject prize, Vector2 retry)
        {
            boss = arenaBoss;
            triggerX = trigger;
            entranceSeal = entrance;
            exitSeal = exit;
            reward = prize;
            retryPoint = retry;
        }

        private void OnEnable()
        {
            GameLoop.Register(this);
            PlayerEvents.Respawned += OnRespawned;
            if (boss != null) boss.Beaten += OnBeaten;
            ApplySeals();
        }

        private void OnDisable()
        {
            GameLoop.Unregister(this);
            PlayerEvents.Respawned -= OnRespawned;
            if (boss != null) boss.Beaten -= OnBeaten;
            // Walked out (between attempts): deaths elsewhere go back to the ink pot.
            if (!fighting) RestoreRespawn();
        }

        public void Tick()
        {
            if (fighting || beaten || boss == null) return;
            LevelDirector director = LevelDirector.Instance;
            PlayerController player = director != null ? director.Player : null;
            if (player == null || player.CurrentState is DefeatedState) return;
            if (director.InTransition) return;
            if (player.Body.Position.x > transform.position.x + triggerX) StartFight(director);
        }

        private void StartFight(LevelDirector director)
        {
            fighting = true;
            if (!holdingRespawn)
            {
                savedRoom = director.CheckpointRoom;
                savedPoint = director.RespawnPoint;
                holdingRespawn = true;
            }
            director.SetRespawn(Room.Of(this), (Vector2)transform.position + retryPoint);
            ApplySeals();
            if (entranceSeal != null && InkSplatter.Instance != null)
                InkSplatter.Instance.Burst(entranceSeal.transform.position, Vector2.right, 14);
            CameraShake.Shake(0.1f);
            boss.Engage();
        }

        private void OnRespawned(PlayerController player)
        {
            // Died mid-fight: the boss resets itself; open the way so the player can walk back in (or leave).
            if (!fighting) return;
            fighting = false;
            ApplySeals();
        }

        private void OnBeaten(BossBase beatenBoss)
        {
            fighting = false;
            beaten = true;
            ApplySeals();
            RestoreRespawn();
            if (exitSeal != null && InkSplatter.Instance != null)
                InkSplatter.Instance.Burst(exitSeal.transform.position, Vector2.left, 20);
        }

        public void ResetForRoom()
        {
            if (beaten) return;
            fighting = false;
            ApplySeals();
        }

        private void RestoreRespawn()
        {
            if (!holdingRespawn) return;
            holdingRespawn = false;
            LevelDirector director = LevelDirector.Instance;
            if (director != null) director.SetRespawn(savedRoom, savedPoint);
        }

        private void ApplySeals()
        {
            if (entranceSeal != null) entranceSeal.SetActive(fighting);
            if (exitSeal != null) exitSeal.SetActive(!beaten);
            if (reward != null) reward.SetActive(beaten);
        }

        private void OnDrawGizmos()
        {
            Vector3 p = transform.position;
            Gizmos.color = new Color(1f, 0.3f, 0.3f, 0.8f);
            Gizmos.DrawLine(p + new Vector3(triggerX, -1f), p + new Vector3(triggerX, 8f));
            Gizmos.DrawWireSphere(p + (Vector3)retryPoint, 0.25f);
        }
    }
}
