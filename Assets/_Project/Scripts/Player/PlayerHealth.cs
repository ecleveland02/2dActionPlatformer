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
    ///   - otherwise the hit deals damage and knocks the player back into hitstun. Hits that land while the player
    ///     is still in hitstun continue the combo (enemies can combo the player), with damage scaled down per hit
    ///     (ComboScaling). When hitstun ends, the combo is over and 45 frames of flickering invulnerability start.
    ///     Dash i-frames and the combo breaker also prevent hits.
    /// Reaching 0 health refills it for now; death and respawn arrive in Milestone 4.
    /// </summary>
    [RequireComponent(typeof(PlayerController))]
    public sealed class PlayerHealth : MonoBehaviour, IHitReceiver, ITickable
    {
        private PlayerController player;
        private Health health;
        private Renderer[] flickerRenderers;
        private readonly ComboScaling comboTaken = new ComboScaling();

        public int TickOrder => 12;

        public Health Health
        {
            get
            {
                if (health == null) health = new Health(Settings.playerMaxHealth);
                return health;
            }
        }

        /// <summary>The combo currently being taken (hit count and damage). Empty when not in hitstun.</summary>
        public ComboScaling ComboTaken => comboTaken;

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
            // No invulnerability per hit: it starts when the combo ends (see Tick).
            int damage = comboTaken.Register(hit.Damage, s.comboDamageStep, s.comboDamageFloor);
            int taken = Health.TakeDamage(damage, 0);
            Debug.Log($"[Player] hit by {hit.Attack.name}: -{taken} ({Health.Current}/{Health.Max})" +
                      (comboTaken.Hits > 1 ? $"  combo taken: {comboTaken.Hits} hits, {comboTaken.Damage} dmg" : ""), this);

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

            // Runs after PlayerController, so the tick hitstun ends is frame 1 of the invulnerability.
            if (comboTaken.Active && !(player.CurrentState is HitstunState))
            {
                comboTaken.End();
                Health.StartInvulnerability(Settings.hurtInvulnerableFrames);
            }
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
