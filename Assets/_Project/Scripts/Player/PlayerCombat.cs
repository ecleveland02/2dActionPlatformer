using System.Collections.Generic;
using Margin.Combat;
using Margin.Core;
using Margin.Input;
using Margin.Rendering;
using Margin.Weapons;
using UnityEngine;

namespace Margin.Player
{
    /// <summary>
    /// The player's side of combat: picks which attack a button press means, checks cancels (spec 6.3),
    /// detects hits against enemy hurtboxes, and runs the player's own hitstop (spec 6.4).
    /// PlayerController and AttackState call into this; it has no Update of its own.
    /// </summary>
    [RequireComponent(typeof(PlayerController))]
    public sealed class PlayerCombat : MonoBehaviour
    {
        [SerializeField] private WeaponData weapon;
        [SerializeField] private CombatSettings settings;
        [Tooltip("Optional blade line drawn from the hand.")]
        [SerializeField] private WeaponLine weaponLine;

        private PlayerController player;
        private readonly HashSet<IHitReceiver> hitThisAttack = new HashSet<IHitReceiver>();
        private readonly List<AabbBox> activeBoxes = new List<AabbBox>();
        private HitboxWindow lastWindow;
        private InkMeter ink;
        private AttackData breakerPush;

        public WeaponData Weapon => weapon;
        public CombatSettings Settings => settings != null ? settings : CombatSettings.Defaults;

        /// <summary>The attack chosen by the last successful Check (read by AttackState.Enter).</summary>
        public AttackData PendingAttack { get; private set; }
        /// <summary>Increments every time an attack starts, so the animator can restart a repeated move.</summary>
        public int AttackSerial { get; private set; }
        /// <summary>Remaining frames of the player's own hitstop. While above 0 the player doesn't tick.</summary>
        public int HitstopFrames { get; set; }
        /// <summary>The ink meter (spec 6.6). Created on first use from CombatSettings.</summary>
        public InkMeter Ink
        {
            get
            {
                if (ink == null)
                {
                    CombatSettings s = Settings;
                    ink = new InkMeter(s.inkMax, s.inkDecayDelayFrames, s.inkDecayIntervalFrames);
                }
                return ink;
            }
        }

        /// <summary>World hitboxes out on the current tick, for the F1 debug view.</summary>
        public IReadOnlyList<AabbBox> ActiveHitboxes => activeBoxes;

        private void Awake()
        {
            player = GetComponent<PlayerController>();
            if (weaponLine != null && weapon != null) weaponLine.Length = weapon.bladeLength;
        }

        public void Configure(WeaponData weaponData, CombatSettings combatSettings)
        {
            weapon = weaponData;
            settings = combatSettings;
        }

        // ---------------- choosing attacks ----------------

        /// <summary>From neutral states: starts an attack if Light/Heavy is buffered and the weapon has a matching move.</summary>
        public bool TryStartAttack() => weapon != null && TrySelect(weapon.Starters());

        /// <summary>Mid-attack: picks a buffered follow-up from the current attack's "Cancels into" list.</summary>
        public bool TryCancelInto(AttackData current) => TrySelect(current.cancelsInto);

        private bool TrySelect(IEnumerable<AttackData> candidates)
        {
            IPlayerInput input = player.Controls;
            AttackDirection dir = input.UpHeld ? AttackDirection.Up : input.DownHeld ? AttackDirection.Down : AttackDirection.Neutral;
            bool airborne = !player.Grounded;

            foreach ((AttackButton button, BufferedAction action) in ButtonActions)
            {
                if (!input.Buffer.IsBuffered(action)) continue;
                AttackData attack = Select(candidates, button, dir, airborne);
                if (attack == null || !Ink.CanSpend(attack.inkCost)) continue;

                input.Buffer.Consume(action);
                PendingAttack = attack;
                return true;
            }
            return false;
        }

        private static readonly (AttackButton, BufferedAction)[] ButtonActions =
        {
            (AttackButton.Light, BufferedAction.LightAttack),
            (AttackButton.Heavy, BufferedAction.HeavyAttack),
            (AttackButton.Special, BufferedAction.Special),
        };

        /// <summary>
        /// The candidate matching button, ground/air and held direction. A directional move wins over a neutral
        /// one; if no directional move exists, the neutral one is used.
        /// </summary>
        public static AttackData Select(IEnumerable<AttackData> candidates, AttackButton button, AttackDirection dir, bool airborne)
        {
            AttackData neutral = null;
            foreach (AttackData a in candidates)
            {
                if (a == null || a.button != button || a.airborne != airborne) continue;
                if (a.direction == dir) return a;
                if (a.direction == AttackDirection.Neutral && neutral == null) neutral = a;
            }
            return neutral;
        }

        // ---------------- running an attack ----------------

        public void BeginAttack(AttackData attack)
        {
            Ink.TrySpend(attack.inkCost);
            hitThisAttack.Clear();
            lastWindow = null;
            activeBoxes.Clear();
            AttackSerial++;
        }

        public void EndAttack() => activeBoxes.Clear();

        /// <summary>Called once per player tick (not during hitstop): ink decay.</summary>
        public void Tick() => Ink.Tick();

        /// <summary>
        /// Tests this frame's hitboxes against every enemy hurtbox (via HitResolver). Each target is hit once per
        /// attack (or once per hitbox window for multi-hit moves). Returns true if anything was hit.
        /// </summary>
        public bool ResolveHits(AttackData attack, int frame)
        {
            activeBoxes.Clear();
            Vector2 origin = player.Body.Position;
            int hits = 0;

            foreach (HitboxWindow window in attack.WindowsAt(frame))
            {
                if (attack.multiHit && window != lastWindow) hitThisAttack.Clear();
                lastWindow = window;

                foreach (HitboxShape shape in window.boxes)
                {
                    AabbBox box = HitboxMath.ToWorld(origin.x, origin.y, player.Facing, shape.offset.x, shape.offset.y, shape.size.x, shape.size.y);
                    activeBoxes.Add(box);
                    hits += HitResolver.Resolve(this, Faction.Player, attack, box, player.Facing, hitThisAttack, Settings);
                }
            }

            if (hits == 0) return false;

            Ink.Gain(attack.inkGain);
            // Air combos: stay level with a juggled target.
            if (!player.Grounded && attack.hoverOnHit > 0f)
                player.Velocity.y = Mathf.Max(player.Velocity.y, attack.hoverOnHit);
            ApplyHitstop(attack.hitstopFrames);
            return true;
        }

        /// <summary>Spawns this attack's projectile (e.g. the Ink Wave special) in front of the player.</summary>
        public void SpawnProjectile(AttackData attack)
        {
            Vector2 origin = player.Body.Position;
            var at = new Vector2(origin.x + attack.projectileOffset.x * player.Facing, origin.y + attack.projectileOffset.y);
            InkWaveProjectile.Spawn(this, attack, at, player.Facing, Settings);
        }

        /// <summary>
        /// The combo breaker's shockwave: pushes every enemy within CombatSettings.comboBreakerSize away from the
        /// player and stuns them (which cancels their attack). Deals no damage. Returns the number pushed.
        /// </summary>
        public int ComboBreakerPush()
        {
            CombatSettings s = Settings;
            AttackData push = BreakerPushAttack(s);
            Vector2 origin = player.Body.Position;
            float halfWidth = s.comboBreakerSize.x * 0.5f;
            hitThisAttack.Clear();

            // Two half boxes, so enemies on each side are knocked away from the player.
            int hits = 0;
            foreach (int side in new[] { player.Facing, -player.Facing })
            {
                AabbBox box = HitboxMath.ToWorld(origin.x, origin.y, side, halfWidth * 0.5f, 0f, halfWidth, s.comboBreakerSize.y);
                hits += HitResolver.Resolve(this, Faction.Player, push, box, side, hitThisAttack, s);
            }
            return hits;
        }

        /// <summary>A hidden AttackData built from the settings, so the push goes through the normal hit rules.</summary>
        private AttackData BreakerPushAttack(CombatSettings s)
        {
            if (breakerPush == null)
            {
                breakerPush = ScriptableObject.CreateInstance<AttackData>();
                breakerPush.name = "ComboBreaker";
                breakerPush.hideFlags = HideFlags.DontSave;
            }
            breakerPush.damage = 0;
            breakerPush.inkGain = 0;
            breakerPush.hitstopFrames = 0;   // the breaker freezes the game itself
            breakerPush.hitstunFrames = s.comboBreakerStunFrames;
            breakerPush.knockback = s.comboBreakerKnockback;
            breakerPush.parryable = false;
            breakerPush.screenShake = 0f;
            return breakerPush;
        }

        private void OnDestroy()
        {
            if (breakerPush != null) Destroy(breakerPush);
        }

        private void ApplyHitstop(int frames)
        {
            // Heavy hits already froze the whole game in HitResolver; lighter ones freeze just the attacker.
            if (frames <= 0 || frames >= Settings.globalHitstopThreshold) return;
            HitstopFrames = Mathf.Max(HitstopFrames, frames);
        }
    }
}
