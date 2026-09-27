using Margin.Core;
using Margin.Rendering;
using UnityEngine;

namespace Margin.Player
{
    /// <summary>
    /// Picks the animation for the player's current state and advances the PoseAnimator each tick.
    /// Ticks right after the PlayerController (TickOrder 10), so the pose always matches this tick's state,
    /// and it freezes and frame-steps with the rest of the game.
    /// </summary>
    [RequireComponent(typeof(PlayerController))]
    public sealed class PlayerAnimator : MonoBehaviour, ITickable
    {
        [SerializeField] private PlayerAnimationSet animations;
        [SerializeField] private PoseAnimator poseAnimator;

        private PlayerController player;
        private bool reportedMissing;
        private int lastAttackSerial = -1;

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

            PoseClip current = poseAnimator.CurrentClip;
            PoseClip next = ChooseClip();

            if (player.CurrentState is AttackState && player.Combat.AttackSerial != lastAttackSerial)
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
            poseAnimator.Tick();
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
                case RedrawState _: return a.redraw;
                case WallJumpState _: return a.wallJump;
                case JumpState _: return a.jump;
                case FastFallState _: return a.fastFall;
                case FallState _: return a.fall;
                case LandState _: return a.land;
                case DashState _: return a.dash;
                case SkidState _: return a.skid;
                case WallSlideState _: return a.wallSlide;
                case RunState _: return player.IsSprinting && a.sprint != null ? a.sprint : a.run;
                default: return a.idle;
            }
        }

        private bool IsCycle(PoseClip clip) => clip != null && (clip == animations.run || clip == animations.sprint);
    }
}
