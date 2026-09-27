using System.Collections.Generic;
using Margin.Combat;
using Margin.Player;
using UnityEngine;

namespace Margin.Enemies
{
    /// <summary>
    /// Runs one enemy attack frame by frame (spec 6.2 frame data): hitboxes on active frames, the attacker's own
    /// hitstop after a hit, and enemy combos: after a hit, it chains into the first attack in the "Cancels into"
    /// list once the cancel window opens, but only while the player is still in hitstun (a true combo).
    /// A missed attack never chains. Plain C# class owned by EnemyBase.
    /// </summary>
    public sealed class EnemyAttackRunner
    {
        private readonly HashSet<IHitReceiver> hitThisAttack = new HashSet<IHitReceiver>();
        private readonly List<AabbBox> activeBoxes = new List<AabbBox>();
        private readonly System.Action<AttackData> started;

        /// <param name="onStarted">Called whenever an attack starts, including chained follow-ups (to play its clip).</param>
        public EnemyAttackRunner(System.Action<AttackData> onStarted) => started = onStarted;

        public AttackData Current { get; private set; }
        /// <summary>Frame of the current attack; 1 on its first tick.</summary>
        public int Frame { get; private set; }
        public bool LandedHit { get; private set; }
        /// <summary>Remaining frames of the attacker's own hitstop after landing a hit.</summary>
        public int Hitstop { get; set; }
        /// <summary>Attacks run so far in the current string (1 = the opener).</summary>
        public int StringLength { get; private set; }
        public bool IsAttacking => Current != null;
        public IReadOnlyList<AabbBox> ActiveHitboxes => activeBoxes;

        public void Start(AttackData attack)
        {
            StringLength = 0;
            Begin(attack);
        }

        private void Begin(AttackData attack)
        {
            Current = attack;
            Frame = 0;
            LandedHit = false;
            StringLength++;
            hitThisAttack.Clear();
            activeBoxes.Clear();
            started?.Invoke(attack);
        }

        public void Cancel()
        {
            Current = null;
            Hitstop = 0;
            activeBoxes.Clear();
        }

        /// <summary>
        /// Advances one tick (call only when not in hitstop). Returns false once the attack or string is over.
        /// </summary>
        public bool Tick(Component attacker, Vector2 origin, int facing, PlayerController target, CombatSettings settings)
        {
            if (Current == null) return false;

            Frame++;
            activeBoxes.Clear();
            AttackTiming timing = Current.Timing;

            if (timing.IsActive(Frame)) ResolveHits(attacker, origin, facing, settings);
            if (Current == null) return false;   // parried: the attacker cancelled us

            if (LandedHit && TryChain(timing, target)) return true;
            if (Frame >= timing.TotalFrames)
            {
                Cancel();
                return false;
            }
            return true;
        }

        private void ResolveHits(Component attacker, Vector2 origin, int facing, CombatSettings settings)
        {
            foreach (HitboxWindow window in Current.WindowsAt(Frame))
            foreach (HitboxShape shape in window.boxes)
            {
                if (Current == null) return;   // parried by an earlier box this frame
                AabbBox box = HitboxMath.ToWorld(origin.x, origin.y, facing, shape.offset.x, shape.offset.y, shape.size.x, shape.size.y);
                activeBoxes.Add(box);
                int hits = HitResolver.Resolve(attacker, Faction.Enemy, Current, box, facing, hitThisAttack, settings);
                if (hits > 0 && Current != null)
                {
                    LandedHit = true;
                    // Heavy hits already froze the whole game in HitResolver.
                    if (Current.hitstopFrames < settings.globalHitstopThreshold) Hitstop = Current.hitstopFrames;
                }
            }
        }

        private bool TryChain(AttackTiming timing, PlayerController target)
        {
            if (Current.cancelsInto == null || Current.cancelsInto.Count == 0) return false;
            AttackData follow = Current.cancelsInto[0];
            if (follow == null || !timing.AllowsAttackCancel(Frame, hasHit: true)) return false;
            if (target == null || !(target.CurrentState is HitstunState)) return false;

            Begin(follow);
            return true;
        }
    }
}
