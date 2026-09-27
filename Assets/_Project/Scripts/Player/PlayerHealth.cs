using Margin.Combat;
using Margin.Core;
using Margin.FX;
using UnityEngine;

namespace Margin.Player
{
    /// <summary>
    /// The player's health and how the player takes hits (spec 6.5, 6.7):
    ///   - during an active parry, a parryable attack is parried: no damage, the attacker staggers, the game
    ///     freezes 12 frames, +20 ink, and the player can act again immediately;
    ///   - otherwise the hit deals damage, knocks the player back into hitstun, and gives 45 frames of flickering
    ///     invulnerability. Dash i-frames also prevent hits.
    /// Reaching 0 health refills it for now; death and respawn arrive in Milestone 4.
    /// </summary>
    [RequireComponent(typeof(PlayerController))]
    public sealed class PlayerHealth : MonoBehaviour, IHitReceiver, ITickable
    {
        private PlayerController player;
        private Health health;
        private Renderer[] flickerRenderers;

        public int TickOrder => 12;

        public Health Health
        {
            get
            {
                if (health == null) health = new Health(Settings.playerMaxHealth);
                return health;
            }
        }

        private CombatSettings Settings =>
            player != null && player.Combat != null ? player.Combat.Settings : CombatSettings.Defaults;

        private void Awake() => player = GetComponent<PlayerController>();
        private void OnEnable() => GameLoop.Register(this);
        private void OnDisable() => GameLoop.Unregister(this);

        public bool CanBeHit => !player.IsInvulnerable && !Health.IsInvulnerable;

        public bool ReceiveHit(in HitInfo hit)
        {
            if (player.CurrentState is ParryState parry && parry.IsActive && hit.Attack.parryable)
            {
                Parry(parry, hit);
                return false;   // not a hit: no damage, no hit effects
            }

            CombatSettings s = Settings;
            int taken = Health.TakeDamage(hit.Damage, s.hurtInvulnerableFrames);
            Debug.Log($"[Player] hit by {hit.Attack.name}: -{taken} ({Health.Current}/{Health.Max})", this);

            // Target side of hitstop: the player freezes too (unless the whole game already froze).
            if (!hit.GlobalHitstop && player.Combat != null)
                player.Combat.HitstopFrames = Mathf.Max(player.Combat.HitstopFrames, hit.HitstopFrames);
            player.EnterHitstun(hit.Knockback, hit.HitstunFrames);

            if (Health.IsDepleted)
            {
                Debug.Log("[Player] defeated. (Death and respawn arrive in Milestone 4; refilling health.)", this);
                Health.Refill();
            }
            return true;
        }

        private void Parry(ParryState parry, in HitInfo hit)
        {
            CombatSettings s = Settings;
            parry.Succeed();
            GameLoop.Freeze(s.parryHitstopFrames);
            if (player.Combat != null) player.Combat.Ink.Gain(s.parryInkGain);
            if (hit.Attacker is IParryable attacker) attacker.OnParried(hit, s.parryStaggerFrames);

            // Ink bursts back toward the attacker.
            Vector2 back = new Vector2(-Mathf.Sign(hit.Knockback.x == 0f ? player.Facing : hit.Knockback.x), 0.4f).normalized;
            if (InkSplatter.Instance != null) InkSplatter.Instance.Burst(hit.Point, back, 20);
            CameraShake.Shake(0.12f);
            CombatEvents.RaiseParry(hit);
            Debug.Log($"[Player] PARRIED {hit.Attack.name}", this);
        }

        public void Tick()
        {
            Health.Tick();
            Flicker(Health.IsInvulnerable && (Health.InvulnerableFramesLeft / 4) % 2 == 1);
        }

        /// <summary>Hurt invulnerability flicker (spec 6.7): hide the visual every other 4 frames.</summary>
        private void Flicker(bool hidden)
        {
            if (player.VisualRoot == null) return;
            if (flickerRenderers == null) flickerRenderers = player.VisualRoot.GetComponentsInChildren<Renderer>(true);
            foreach (Renderer r in flickerRenderers)
                if (r != null) r.enabled = !hidden;
        }
    }
}
