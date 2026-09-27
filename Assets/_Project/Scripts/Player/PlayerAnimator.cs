using Margin.Core;
using Margin.Rendering;
using UnityEngine;

namespace Margin.Player
{
    /// <summary>
    /// Picks the animation for the player's current state and advances the PoseAnimator each tick.
    /// Ticks right after the PlayerController (TickOrder 10), so the pose always matches this tick's state,
    /// and it freezes and frame-steps with the rest of the game.
    ///
    /// Also plays one-shot transition clips (run turn, run stop, hard landing, parry flourish, idle fidget).
    /// These are visual only: gameplay has already moved on, and any state change except into Idle cancels
    /// them, so they can never delay an input. Run and sprint cycles play faster or slower with speed so the
    /// planted foot doesn't slide (CycleSync).
    /// </summary>
    [RequireComponent(typeof(PlayerController))]
    public sealed class PlayerAnimator : MonoBehaviour, ITickable
    {
        [SerializeField] private PlayerAnimationSet animations;
        [SerializeField] private PoseAnimator poseAnimator;

        private PlayerController player;
        private bool reportedMissing;
        private int lastAttackSerial = -1;
        private float lastVelocityX, lean;

        private PoseClip oneShot;          // transition clip playing now, or null
        private PlayerState lastState;
        private int lastFacing, lastInputX;
        private int idleTicks;

        /// <summary>The transition clip playing, or null (for tests and the debug overlay).</summary>
        public PoseClip OneShot => oneShot;

        public int TickOrder => 10;
        public PoseAnimator PoseAnimator => poseAnimator;

        private void Awake() => player = GetComponent<PlayerController>();
        private void OnEnable() => GameLoop.Register(this);
        private void OnDisable() => GameLoop.Unregister(this);

        public void Configure(PlayerAnimationSet set, PoseAnimator animator)
        {
            animations = set;
            poseAnimator = animator;
        }

        public void Tick()
        {
            if (animations == null || poseAnimator == null || player.CurrentState == null)
            {
                if (!reportedMissing)
                {
                    Debug.LogError("PlayerAnimator: 'Animations' or 'Pose Animator' is empty. Run Margin > Wire Player References.", this);
                    reportedMissing = true;
                }
                return;
            }

            // Hitstop freezes the pose along with the player.
            if (player.InHitstop) return;

            UpdateOneShot();

            PoseClip current = poseAnimator.CurrentClip;
            PoseClip next = oneShot != null ? oneShot : ChooseClip();

            if (oneShot != null)
            {
                if (current != oneShot) poseAnimator.Restart(oneShot);
            }
            else if (player.CurrentState is AttackState && player.Combat.AttackSerial != lastAttackSerial)
            {
                // A new attack always starts its clip from frame 1, even if it's the same move again.
                lastAttackSerial = player.Combat.AttackSerial;
                poseAnimator.Restart(next != null ? next : current);
            }
            else
            {
                // Switching between run and sprint continues the stride instead of restarting it.
                bool cycleSwap = IsCycle(current) && IsCycle(next);
                poseAnimator.Play(next, keepPhase: cycleSwap);
            }
            poseAnimator.SetAdditive(player.CurrentState is IdleState ? animations.idleBreathing : null);
            poseAnimator.Lean = UpdateLean();
            // Hang along the grapple line; ease back upright after letting go.
            float tiltGoal = player.CurrentState is GrappleState grapple ? Mathf.Clamp(grapple.RopeAngle, -85f, 85f) : 0f;
            ropeTilt = Mathf.MoveTowards(ropeTilt, tiltGoal, player.CurrentState is GrappleState ? 25f : 8f);
            poseAnimator.Tilt = ropeTilt;
            poseAnimator.PlaybackRate = CycleRate(poseAnimator.CurrentClip);
            UpdateAirBlend();
            poseAnimator.KeepFeetOnFloor = player.Grounded;
            poseAnimator.Tick();
        }

        /// <summary>Starts, keeps or ends the one-shot transition clip for this tick.</summary>
        private void UpdateOneShot()
        {
            PlayerState state = player.CurrentState;
            PlayerAnimationSet a = animations;

            // A finished one-shot hands back to the state's clip.
            if (oneShot != null && poseAnimator.CurrentClip == oneShot && poseAnimator.Finished) oneShot = null;

            if (state != lastState)
            {
                // Leaving for anything but Idle cancels (a new move always wins); into Idle, a landing or
                // stop may finish playing.
                if (!(state is IdleState)) oneShot = null;

                if (state is LandState && player.LastFallHeight >= a.hardLandingHeight) Begin(a.hardLand);
                else if (lastState is ParryState parry && parry.Succeeded) Begin(a.parrySuccess);
            }
            else if (state is RunState)
            {
                if (player.Facing != lastFacing && !player.IsSprinting) Begin(a.turn);
                // Let go of the direction at speed: plant and slide to a stop (Run lasts until velocity is 0).
                else if (player.InputX == 0 && lastInputX != 0 && Mathf.Abs(lastVelocityX) >= a.runStopMinSpeed) Begin(a.runStop);
                // Pressed a direction again: back to running.
                else if (player.InputX != 0 && oneShot == a.runStop) oneShot = null;
            }

            idleTicks = state is IdleState ? idleTicks + 1 : 0;
            if (idleTicks >= a.fidgetAfterFrames && oneShot == null)
            {
                Begin(a.idleFidget);
                idleTicks = 0;
            }

            lastState = state;
            lastFacing = player.Facing;
            lastInputX = player.InputX;
        }

        private void Begin(PoseClip clip)
        {
            if (clip == null) return;
            oneShot = clip;
            poseAnimator.Restart(clip);
        }

        /// <summary>Cycles with a stride length play in step with movement speed so feet don't slide.</summary>
        private float CycleRate(PoseClip clip)
        {
            if (clip == null || clip.strideLength <= 0f || clip.Timeline == null) return 1f;
            PoseMotionSettings m = poseAnimator.Motion;
            return CycleSync.Rate(player.Velocity.x, clip.strideLength, clip.Timeline.TotalFrames, m.minCycleRate, m.maxCycleRate);
        }

        /// <summary>
        /// Leans into acceleration while running or standing on the ground: speeding up tips the body forward,
        /// stopping tips it back before it settles. Smoothed, and off in every other state (attacks, air, hurt)
        /// so authored poses stay exact.
        /// </summary>
        private float ropeTilt;

        private float UpdateLean()
        {
            PoseMotionSettings m = poseAnimator.Motion;
            float acceleration = (player.Velocity.x - lastVelocityX) / GameTime.TickDelta;
            lastVelocityX = player.Velocity.x;

            bool allowed = player.Grounded && (player.CurrentState is RunState || player.CurrentState is IdleState);
            float target = allowed
                ? Mathf.Clamp(acceleration * player.Facing * m.leanPerAcceleration, -m.maxLeanDegrees, m.maxLeanDegrees)
                : 0f;
            lean = Mathf.Lerp(lean, target, m.leanSmoothing);
            return lean;
        }

        /// <summary>The clip for the current state. Subclasses are checked before their base (WallJump before Jump).</summary>
        public PoseClip ChooseClip()
        {
            PlayerAnimationSet a = animations;
            switch (player.CurrentState)
            {
                case AttackState attack: return attack.Attack.poseClip != null ? attack.Attack.poseClip : a.idle;
                case HitstunState _: return a.hitstun != null ? a.hitstun : a.fall;
                case ParryState _: return a.parry;
                case DefeatedState _: return a.defeated != null ? a.defeated : a.hitstun;
                case ComboBreakerState _: return a.comboBreaker != null ? a.comboBreaker : a.parry;
                case RedrawState _: return a.redraw;
                case WallJumpState _: return a.wallJump;
                case GrappleState _: return a.grapple != null ? a.grapple : a.jumpApex != null ? a.jumpApex : a.fall;
                case JumpState _: return AirBlendOn ? AirBase() : NearApex() ? a.jumpApex : a.jump;
                case FastFallState _: return a.fastFall;
                case FallState _: return AirBlendOn ? AirBase() : NearApex() ? a.jumpApex : a.fall;
                case LandState _: return a.land;
                case DashState _: return a.dash;
                case SkidState _: return a.skid;
                case WallSlideState _: return a.wallSlide;
                case RunState _: return player.IsSprinting && a.sprint != null ? a.sprint : a.run;
                default: return a.idle;
            }
        }

        /// <summary>Near the top of a jump (and an apex clip exists).</summary>
        // ---------------- N+-style air posing ----------------

        private bool AirBlendOn => animations.jumpApex != null && animations.fall != null &&
                                   animations.airBlendRiseSpeed > 0f && animations.airBlendFallSpeed > 0f;

        private bool InAirBlendState => AirBlendOn && oneShot == null &&
                                        (player.CurrentState is JumpState || player.CurrentState is FallState);

        /// <summary>Base clip in the air: the jump while rising, the apex pose while falling (blended in AirBlend).</summary>
        private PoseClip AirBase() =>
            AirPoseBlend.Weights(player.Velocity.y, animations.airBlendRiseSpeed, animations.airBlendFallSpeed).rising
                ? animations.jump : animations.jumpApex;

        /// <summary>How much apex (rising) or fall loop (falling) to blend over the base, from vertical speed.</summary>
        private void UpdateAirBlend()
        {
            if (!InAirBlendState)
            {
                poseAnimator.SetBlend(null, 0f);
                return;
            }
            var (rising, weight) = AirPoseBlend.Weights(player.Velocity.y, animations.airBlendRiseSpeed, animations.airBlendFallSpeed);
            poseAnimator.SetBlend(rising ? animations.jumpApex : animations.fall, weight);
        }

        private bool NearApex() => animations.jumpApex != null && Mathf.Abs(player.Velocity.y) <= animations.apexVelocityBand;

        private bool IsCycle(PoseClip clip) => clip != null && (clip == animations.run || clip == animations.sprint);
    }
}
