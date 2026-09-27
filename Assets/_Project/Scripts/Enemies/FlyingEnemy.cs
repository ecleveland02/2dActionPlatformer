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
        protected override bool GravityApplies => CurrentState == Hitstun || CurrentState == Dead || CurrentState == Knockdown;
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

        private float tumble;

        protected override void UpdateVisual()
        {
            if (visual == null) return;
            visual.transform.localScale = new Vector3(Facing, 1f, 1f);
            visual.Tint = FlashColor();
            BatWings wings = WingsFor();
            visual.Pose(wings);

            // Body motion (visual only; the collider doesn't move):
            //   on the ground after a knockdown or death: belly up, wings splayed;
            //   stunned in the air: tumbling end over end;
            //   flying: bobbing with each wing beat and banking into its movement.
            float tilt, bob = 0f;
            if (CurrentState == Knockdown && FramesInState > Settings.knockdownFrames)
            {
                // Getting up: flip back over while the wings start beating.
                float t = (FramesInState - Settings.knockdownFrames) / (float)Settings.getUpFrames;
                tilt = 180f * (1f - Mathf.Clamp01(t));
            }
            else if (wings == BatWings.Splayed)
            {
                tilt = 180f;
                tumble = 0f;
            }
            else if (CurrentState == Hitstun || CurrentState == Dead || CurrentState == Knockdown)
            {
                tumble += 24f;
                tilt = tumble;
            }
            else
            {
                tumble = 0f;
                float beat = Mathf.Sin(visual.FlapPhase * 2f * Mathf.PI);
                bob = 0.05f * beat;
                tilt = Mathf.Clamp(-Velocity.x * 2.5f, -25f, 25f) + 4f * beat;
            }
            visual.transform.localRotation = Quaternion.Euler(0f, 0f, tilt);
            Vector3 p = visual.transform.localPosition;
            visual.transform.localPosition = new Vector3(p.x, bob, 0f);
        }

        private BatWings WingsFor()
        {
            if (CurrentState == Knockdown && FramesInState > Settings.knockdownFrames) return BatWings.Flap;
            if ((CurrentState == Dead || CurrentState == Knockdown) && Grounded) return BatWings.Splayed;
            if (CurrentState == Dead || CurrentState == Hitstun || CurrentState == Knockdown) return BatWings.Folded;
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
