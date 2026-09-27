using Margin.Rendering;
using UnityEngine;

namespace Margin.Player
{
    /// <summary>
    /// Which PoseClip plays for each player state. Create via Margin > Create Starter Animations,
    /// or Assets > Create > Margin > Player Animation Set. Empty slots keep the previous animation.
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
        public PoseClip hitstun;
        public PoseClip parry;
        public PoseClip redraw;
    }
}
