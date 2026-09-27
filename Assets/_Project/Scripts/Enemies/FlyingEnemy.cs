using Margin.Rendering;
using UnityEngine;

namespace Margin.Enemies
{
    /// <summary>
    /// A flying enemy (spec 9: Scribble Bat, teaches air attacks). Same health, hits, attack slots and reset as
    /// EnemyBase, but it hovers instead of walking: it drifts around its home, then takes up a spot above and to
    /// the side of the player (EnemyData.hoverHeight / hoverSide) and dives at where the player was when its
    /// wind-up ended. It only falls while stunned (juggle gravity) or dead. Drawn by a ScribbleBatVisual.
    /// </summary>
    public sealed class FlyingEnemy : EnemyBase
    {
        [SerializeField] private ScribbleBatVisual visual;

        public override bool Flies => true;
        protected override bool GravityApplies => CurrentState == Hitstun || CurrentState == Dead;
        public ScribbleBatVisual Visual => visual;

        protected override void Awake()
        {
            base.Awake();
            Patrol = new FlyPatrolState(this);
            Approach = new FlyApproachState(this);
            Attack = new DiveAttackState(this);
        }

        public void ConfigureVisual(ScribbleBatVisual batVisual) => visual = batVisual;

        /// <summary>Steers toward a point, slowing down as it arrives, at up to <paramref name="maxSpeed"/>.</summary>
        public void FlyToward(Vector2 point, float maxSpeed)
        {
            Vector2 offset = point - Position;
            float speed = Mathf.Min(maxSpeed, offset.magnitude * 4f);   // arrive gently instead of overshooting
            Vector2 desired = offset.sqrMagnitude > 0.0001f ? offset.normalized * speed : Vector2.zero;
            SteerTo(desired);
        }

        /// <summary>Changes velocity toward <paramref name="desired"/> at the flying acceleration.</summary>
        public void SteerTo(Vector2 desired, float accelerationScale = 1f)
        {
            float step = Data.flySpeed / Data.flyAccelerationFrames * accelerationScale;
            Velocity = Vector2.MoveTowards(Velocity, desired, step);
        }

        public override void Hold() => SteerTo(Vector2.zero);

        protected override void UpdateVisual()
        {
            if (visual == null) return;
            visual.transform.localScale = new Vector3(Facing, 1f, 1f);
            visual.Tint = FlashColor();
            visual.Pose(WingsFor());
        }

        private BatWings WingsFor()
        {
            if (CurrentState == Dead || CurrentState == Hitstun) return BatWings.Folded;
            if (CurrentState != Attack || Runner.Current == null) return BatWings.Flap;
            var t = Runner.Current.Timing;
            if (Runner.Frame <= t.Startup) return BatWings.Raised;          // wind-up: wings up (the telegraph)
            return Runner.Frame <= t.LastActiveFrame ? BatWings.Folded : BatWings.Flap;   // dive, then flap away
        }

        protected override void ShakeVisual(bool shaking)
        {
            if (visual == null) return;
            float x = shaking ? (HitstopRemaining % 2 == 0 ? 1f : -1f) * Settings.hitShakeDistance : 0f;
            visual.transform.localPosition = new Vector3(x, 0f, 0f);
        }

        protected override void SetVisible(bool visible)
        {
            if (visual != null) visual.gameObject.SetActive(visible);
        }
    }
}
