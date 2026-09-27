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
    /// Reaching 0 health: DefeatedState for CombatSettings.playerDeathFrames, then a respawn at SpawnPoint with
    /// full health and empty ink, and PlayerEvents.Respawned (enemies reset). Ink pot checkpoints move SpawnPoint.
    /// Level hazards (pits, the Highlighter's ink flood) go through TakeHazardDamage.
    /// </summary>
    [RequireComponent(typeof(PlayerController))]
    public sealed class PlayerHealth : MonoBehaviour, IHitReceiver, ITickable
    {
        private PlayerController player;
        private Health health;
        private Renderer[] flickerRenderers;
        private readonly ComboScaling comboTaken = new ComboScaling();

        public int TickOrder => 12;

        /// <summary>Where the player respawns: the start position, then the last ink pot touched.</summary>
        public Vector2 SpawnPoint { get; set; }

        /// <summary>Can't be hit at all, without the hurt flicker (room transitions set this while the screen fades).</summary>
        public bool Protected { get; set; }

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

        private void Awake()
        {
            player = GetComponent<PlayerController>();
            SpawnPoint = transform.position;
        }
        private void OnEnable() => GameLoop.Register(this);
        private void OnDisable() => GameLoop.Unregister(this);

        public bool CanBeHit => !Protected && !player.IsInvulnerable && !Health.IsInvulnerable && !Health.IsDepleted;

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
            if (Health.IsDepleted) Die(hit.Knockback, hit.Attack.name);
            else player.EnterHitstun(hit.Knockback, hit.HitstunFrames);
            return true;
        }

        /// <summary>
        /// Damage from the level itself (a pit): no knockback or hitstun, can't be parried, ignored while
        /// invulnerable. Starts the usual hurt invulnerability. Returns true if it was fatal (the defeat starts).
        /// </summary>
        public bool TakeHazardDamage(int amount, string cause)
        {
            if (Health.IsDepleted || Protected) return false;
            int taken = Health.TakeDamage(amount, 0);
            if (taken <= 0) return false;
            Debug.Log($"[Player] {cause}: -{taken} ({Health.Current}/{Health.Max})", this);
            if (Health.IsDepleted)
            {
                Die(Vector2.zero, cause);
                return true;
            }
            Health.StartInvulnerability(Settings.hurtInvulnerableFrames);
            return false;
        }

        private void Die(Vector2 knockback, string cause)
        {
            comboTaken.End();
            player.EnterDefeated(knockback);
            Debug.Log($"[Player] DEFEATED by {cause}", this);
            PlayerEvents.RaiseDied(player);
        }

        /// <summary>Back to the spawn point with full health, empty ink and a moment of invulnerability.</summary>
        public void Respawn()
        {
            CombatSettings s = Settings;
            player.ResetTo(SpawnPoint);
            Protected = false;
            Health.Refill();
            Health.StartInvulnerability(s.respawnInvulnerableFrames);
            comboTaken.End();
            if (player.Combat != null)
            {
                player.Combat.Ink.Reset();
                player.Combat.HitstopFrames = 0;
            }
            Debug.Log("[Player] respawned", this);
            PlayerEvents.RaiseRespawned(player);
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
            if (player.CurrentState is DefeatedState && player.FramesInState >= Settings.playerDeathFrames)
            {
                Respawn();
                return;
            }

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
