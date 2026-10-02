using Margin.Rendering;
using UnityEngine;

namespace Margin.Player
{
    /// <summary>
    /// Which PoseClip plays for each player state, plus one-shot transition clips (visual only: they never delay
    /// gameplay, and any state change except into Idle cancels them). Create via Margin > Create Starter
    /// Animations, or Assets > Create > Margin > Player Animation Set. Empty slots keep the previous animation.
    /// </summary>
    [CreateAssetMenu(fileName = "PlayerAnimationSet", menuName = "Margin/Player Animation Set")]
    public sealed class PlayerAnimationSet : ScriptableObject
    {
        public PoseClip idle;
        [Tooltip("Additive layer played on top of idle (subtle breathing).")]
        public PoseClip idleBreathing;
        public PoseClip run;
        public PoseClip sprint;
        public PoseClip skid;
        public PoseClip jump;
        public PoseClip fall;
        public PoseClip fastFall;
        public PoseClip land;
        public PoseClip dash;
        public PoseClip wallSlide;
        public PoseClip wallJump;
        [Tooltip("Frames of the front flip at the start of a double jump. 0 = no flip.")]
        [Min(0)] public int doubleJumpFlipFrames = 18;
        [Tooltip("Hanging from the Grapple Line (free arm up the line; the body tilts along it). Falls back to the jump apex.")]
        public PoseClip grapple;
        public PoseClip hitstun;
        public PoseClip parry;
        public PoseClip redraw;
        [Tooltip("Combo breaker burst. Falls back to the parry clip.")]
        public PoseClip comboBreaker;
        [Tooltip("Defeated (health 0). Falls back to the hitstun clip.")]
        public PoseClip defeated;
        [Tooltip("Top of a jump (vertical speed within Apex Velocity Band). Empty = jump/fall clips only.")]
        public PoseClip jumpApex;

        [Header("One-shot transitions (visual only)")]
        [Tooltip("Turning around while running (not sprinting; sprint turns skid).")]
        public PoseClip turn;
        [Tooltip("Stopping from a run: plant and settle. Plays when the direction is released at speed.")]
        public PoseClip runStop;
        [Tooltip("After standing still for Fidget After Frames.")]
        public PoseClip idleFidget;
        [Tooltip("Landing from a high fall (more than Hard Landing Height). Replaces the land clip; lag is unchanged.")]
        public PoseClip hardLand;
        [Tooltip("Blade flourish after a successful parry.")]
        public PoseClip parrySuccess;

        [Header("Transition thresholds")]
        [Tooltip("Vertical speed (units/s) around the top of a jump that shows the apex pose. Only used when the " +
                 "air blend is off (both speeds 0).")]
        [Min(0f)] public float apexVelocityBand = 2.5f;
        [Tooltip("N+-style air posing: rising faster than this (units/s) is the pure jump pose; slower blends toward " +
                 "the apex pose. 0 = off (jump/apex/fall switch at thresholds).")]
        [Min(0f)] public float airBlendRiseSpeed = 12f;
        [Tooltip("Falling faster than this (units/s) is the pure fall loop; slower blends from the apex pose.")]
        [Min(0f)] public float airBlendFallSpeed = 10f;
        [Tooltip("Falling farther than this (units, from the top of the airtime) plays the hard landing. " +
                 "A full jump is 3.2, so 4.5 means drops from ledges, not ordinary jumps.")]
        [Min(0f)] public float hardLandingHeight = 4.5f;
        [Tooltip("Idle this many frames before a fidget (300 = 5 s).")]
        [Min(1)] public int fidgetAfterFrames = 300;
        [Tooltip("Stopping from at least this speed (units/s) plays the run stop.")]
        [Min(0f)] public float runStopMinSpeed = 6f;
    }
}
