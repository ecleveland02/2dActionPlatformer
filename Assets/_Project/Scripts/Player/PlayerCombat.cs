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

        public WeaponData Weapon => weapon;
        public CombatSettings Settings => settings != null ? settings : CombatSettings.Defaults;

        /// <summary>The attack chosen by the last successful Check (read by AttackState.Enter).</summary>
        public AttackData PendingAttack { get; private set; }
        /// <summary>Increments every time an attack starts, so the animator can restart a repeated move.</summary>
        public int AttackSerial { get; private set; }
        /// <summary>Remaining frames of the player's own hitstop. While above 0 the player doesn't tick.</summary>
        public int HitstopFrames { get; set; }
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
                if (attack == null) continue;

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
            hitThisAttack.Clear();
            lastWindow = null;
            activeBoxes.Clear();
            AttackSerial++;
        }

        public void EndAttack() => activeBoxes.Clear();

        /// <summary>
        /// Tests this frame's hitboxes against every enemy hurtbox. Each target is hit once per attack
        /// (or once per hitbox window for multi-hit moves). Returns true if anything was hit.
        /// </summary>
        public bool ResolveHits(AttackData attack, int frame)
        {
            activeBoxes.Clear();
            Vector2 origin = player.Body.Position;
            bool anyHit = false;
            int stop = 0;

            foreach (HitboxWindow window in attack.WindowsAt(frame))
            {
                if (attack.multiHit && window != lastWindow) hitThisAttack.Clear();
                lastWindow = window;

                foreach (HitboxShape shape in window.boxes)
                {
                    AabbBox box = HitboxMath.ToWorld(origin.x, origin.y, player.Facing, shape.offset.x, shape.offset.y, shape.size.x, shape.size.y);
                    activeBoxes.Add(box);

                    // Copy: a hit can disable or destroy a hurtbox, which changes the list.
                    Hurtbox[] targets = new Hurtbox[Hurtbox.Active.Count];
                    for (int i = 0; i < targets.Length; i++) targets[i] = Hurtbox.Active[i];

                    foreach (Hurtbox hurtbox in targets)
                    {
                        if (hurtbox == null || hurtbox.Faction == Faction.Player) continue;
                        IHitReceiver target = hurtbox.Receiver;
                        if (target == null || !target.CanBeHit || hitThisAttack.Contains(target)) continue;

                        AabbBox hurt = hurtbox.WorldBox;
                        if (!HitboxMath.Overlaps(box, hurt)) continue;

                        HitboxMath.OverlapCenter(box, hurt, out float px, out float py);
                        bool global = attack.hitstopFrames >= Settings.globalHitstopThreshold;
                        var knockback = new Vector2(attack.knockback.x * player.Facing, attack.knockback.y);
                        var hit = new HitInfo(this, attack, knockback, new Vector2(px, py), global);

                        hitThisAttack.Add(target);
                        target.ReceiveHit(hit);
                        CombatEvents.RaiseHit(hit, target);
                        anyHit = true;
                        stop = Mathf.Max(stop, attack.hitstopFrames);
                    }
                }
            }

            if (anyHit) ApplyHitstop(stop);
            return anyHit;
        }

        private void ApplyHitstop(int frames)
        {
            if (frames <= 0) return;
            if (frames >= Settings.globalHitstopThreshold) GameLoop.Freeze(frames);
            else HitstopFrames = Mathf.Max(HitstopFrames, frames);
        }
    }
}
