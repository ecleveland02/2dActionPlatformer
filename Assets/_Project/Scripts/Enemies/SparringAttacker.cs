using System.Collections.Generic;
using Margin.Combat;
using Margin.Core;
using Margin.Player;
using UnityEngine;

namespace Margin.Enemies
{
    /// <summary>
    /// Makes a TrainingDummy fight back, for practicing parries (spec 6.5) and reading telegraphs (spec 9):
    /// when the player is in range it turns to face them and cycles through its attacks with a pause between.
    /// Unparryable attacks flash red during their startup. Every attack should have 12+ startup frames.
    /// Being hit or parried cancels the attack in progress; a parry staggers the dummy for a punish.
    /// Enemy combos: when an attack hits, it chains into the first attack in its "Cancels into" list at the
    /// start of its cancel window, but only while the player is still in hitstun (a true combo). A miss never
    /// chains, so only a string's first attack needs the long telegraph; follow-ups may be faster.
    /// </summary>
    [RequireComponent(typeof(TrainingDummy))]
    public sealed class SparringAttacker : MonoBehaviour, ITickable, IParryable
    {
        [SerializeField] private List<AttackData> attacks = new List<AttackData>();
        [SerializeField] private CombatSettings settings;
        [Tooltip("Frames between the end of one attack and the start of the next.")]
        [SerializeField, Min(1)] private int pauseFrames = 60;
        [Tooltip("Starts attacking when the player is this close (units, horizontal).")]
        [SerializeField, Min(0f)] private float range = 2.2f;

        private static readonly Color FlashA = new Color(0.85f, 0.1f, 0.1f);
        private static readonly Color FlashB = new Color(0.45f, 0.05f, 0.05f);

        private TrainingDummy dummy;
        private PlayerController target;
        private readonly HashSet<IHitReceiver> hitThisAttack = new HashSet<IHitReceiver>();
        private int next, cooldown, hitstop;
        private bool landedHit;

        public int TickOrder => 21;
        public AttackData Current { get; private set; }
        public int Frame { get; private set; }

        private CombatSettings Settings => settings != null ? settings : CombatSettings.Defaults;

        private void Awake() => dummy = GetComponent<TrainingDummy>();
        private void OnEnable() => GameLoop.Register(this);
        private void OnDisable() => GameLoop.Unregister(this);

        public void Configure(List<AttackData> moveList, CombatSettings combatSettings, int pause, float attackRange)
        {
            attacks = moveList;
            settings = combatSettings;
            pauseFrames = pause;
            range = attackRange;
        }

        /// <summary>Starts an attack immediately, facing the player (used by tests and the pattern).</summary>
        public void StartAttack(AttackData attack)
        {
            FindTarget();
            if (target != null) dummy.SetFacing(target.Body.Position.x >= dummy.Position.x ? 1 : -1);
            Current = attack;
            Frame = 0;
            landedHit = false;
            hitThisAttack.Clear();
        }

        public void OnParried(in HitInfo hit, int staggerFrames)
        {
            Cancel();
            dummy.Stagger(staggerFrames);
        }

        public void Tick()
        {
            if (dummy.IsStunned)
            {
                if (Current != null) Cancel();
                return;
            }
            if (dummy.HitstopRemaining > 0) return;
            if (hitstop > 0)
            {
                hitstop--;
                return;
            }

            if (Current == null)
            {
                if (cooldown > 0) cooldown--;
                else if (attacks.Count > 0 && PlayerInRange())
                {
                    StartAttack(attacks[next]);
                    next = (next + 1) % attacks.Count;
                }
                return;
            }

            Frame++;
            AttackTiming timing = Current.Timing;

            // Pose from the attack's clip; unparryable attacks flash red while winding up (spec 6.5).
            if (Current.poseClip != null && Current.poseClip.Timeline != null)
                dummy.PoseOverride = Current.poseClip.Timeline.Sample(Frame - 1);
            if (dummy.Rig != null)
                dummy.Rig.Tint = !Current.parryable && Frame <= timing.Startup ? (Frame / 3 % 2 == 0 ? FlashA : FlashB) : (Color?)null;

            if (timing.IsActive(Frame)) ResolveHits();
            if (Current == null) return;   // parried
            if (landedHit && TryChain(timing)) return;

            if (Frame >= timing.TotalFrames) Finish();
        }

        private void ResolveHits()
        {
            Vector2 origin = dummy.Position;
            foreach (HitboxWindow window in Current.WindowsAt(Frame))
            foreach (HitboxShape shape in window.boxes)
            {
                AabbBox box = HitboxMath.ToWorld(origin.x, origin.y, dummy.Facing, shape.offset.x, shape.offset.y, shape.size.x, shape.size.y);
                if (Current == null) return;   // parried by an earlier box this frame
                int hits = HitResolver.Resolve(this, Faction.Enemy, Current, box, dummy.Facing, hitThisAttack, Settings);
                if (hits > 0 && Current != null)
                {
                    landedHit = true;
                    if (Current.hitstopFrames < Settings.globalHitstopThreshold) hitstop = Current.hitstopFrames;
                }
            }
        }

        /// <summary>Continues the combo into the next attack of the string if the player is still in hitstun.</summary>
        private bool TryChain(AttackTiming timing)
        {
            if (Current.cancelsInto == null || Current.cancelsInto.Count == 0) return false;
            AttackData follow = Current.cancelsInto[0];
            if (follow == null || !timing.AllowsAttackCancel(Frame, hasHit: true)) return false;
            if (target == null || !(target.CurrentState is HitstunState)) return false;

            StartAttack(follow);
            return true;
        }

        private void Finish()
        {
            Current = null;
            cooldown = pauseFrames;
            dummy.PoseOverride = null;
            if (dummy.Rig != null) dummy.Rig.Tint = null;
        }

        private void Cancel() => Finish();

        private bool PlayerInRange()
        {
            FindTarget();
            return target != null && Mathf.Abs(target.Body.Position.x - dummy.Position.x) <= range &&
                   Mathf.Abs(target.Body.Position.y - dummy.Position.y) <= 2f;
        }

        private void FindTarget()
        {
            if (target == null) target = SceneQuery.FindFirst<PlayerController>();
        }

        /// <summary>For tests: aim at a specific player.</summary>
        public void SetTarget(PlayerController player) => target = player;
    }
}
