using UnityEngine;

namespace Margin.Rendering
{
    /// <summary>
    /// One saved keyframe (spec 4.2). Create poses with the pose editor: select a StickFigureRig
    /// (Margin > Open Pose Studio), drag the joint rings, then Save As New in the Inspector.
    /// </summary>
    [CreateAssetMenu(fileName = "Pose", menuName = "Margin/Pose")]
    public sealed class PoseData : ScriptableObject
    {
        [Tooltip("Degrees relative to the parent bone. Positive swings the bone forward (toward facing). " +
                 "Knees bend negative, elbows positive. Root offset shifts the hips in units.")]
        public FigurePose pose;
    }
}
