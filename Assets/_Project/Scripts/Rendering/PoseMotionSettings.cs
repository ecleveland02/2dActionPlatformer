using UnityEngine;

namespace Margin.Rendering
{
    /// <summary>
    /// Tuning for procedural follow-through and lean (SecondaryMotion). Create via Assets > Create > Margin >
    /// Pose Motion Settings; animators without one use these defaults.
    /// </summary>
    [CreateAssetMenu(fileName = "PoseMotionSettings", menuName = "Margin/Pose Motion Settings")]
    public sealed class PoseMotionSettings : ScriptableObject
    {
        [Tooltip("Follow-through on/off.")]
        public bool enabled = true;
        [Tooltip("How fast the loose joints catch up (Hz). Lower = floppier and more delayed.")]
        [Range(1f, 20f)] public float frequency = 5f;
        [Tooltip("Below 1 the joints overshoot and settle (bouncy); 1 = no overshoot.")]
        [Range(0.1f, 1.5f)] public float damping = 0.5f;

        [Header("How sprung each joint is (0 = exact animation, 1 = fully sprung)")]
        [Tooltip("Small: the torso sways a little behind the hips.")]
        [Range(0f, 1f)] public float spine = 0.4f;
        [Range(0f, 1f)] public float neck = 1f;
        [Tooltip("Keep 0: the sword arm must hit its pose on the exact frame (attacks, hitboxes).")]
        [Range(0f, 1f)] public float shoulderFront = 0f;
        [Range(0f, 1f)] public float elbowFront = 0f;
        [Range(0f, 1f)] public float shoulderBack = 0.6f;
        [Range(0f, 1f)] public float elbowBack = 1f;
        [Tooltip("Keep legs at 0: sprung legs make the feet slide on the ground.")]
        [Range(0f, 1f)] public float legs = 0f;

        [Header("Lean into acceleration (player on the ground)")]
        [Tooltip("Degrees of forward lean per unit/s² of acceleration. Speeding up leans forward, stopping leans back.")]
        [Min(0f)] public float leanPerAcceleration = 0.12f;
        [Min(0f)] public float maxLeanDegrees = 10f;
        [Tooltip("How quickly the lean follows (fraction per tick).")]
        [Range(0.01f, 1f)] public float leanSmoothing = 0.2f;

        private float[] weights;

        /// <summary>Per-joint weights in FigurePose.AllJoints order.</summary>
        public float[] Weights
        {
            get
            {
                if (weights == null) weights = new float[FigurePose.AllJoints.Length];
                for (int i = 0; i < weights.Length; i++)
                {
                    switch (FigurePose.AllJoints[i])
                    {
                        case PoseJoint.Spine: weights[i] = spine; break;
                        case PoseJoint.Neck: weights[i] = neck; break;
                        case PoseJoint.ShoulderFront: weights[i] = shoulderFront; break;
                        case PoseJoint.ElbowFront: weights[i] = elbowFront; break;
                        case PoseJoint.ShoulderBack: weights[i] = shoulderBack; break;
                        case PoseJoint.ElbowBack: weights[i] = elbowBack; break;
                        default: weights[i] = legs; break;
                    }
                }
                return weights;
            }
        }

        private static PoseMotionSettings defaults;

        public static PoseMotionSettings Defaults
        {
            get
            {
                if (defaults == null)
                {
                    defaults = CreateInstance<PoseMotionSettings>();
                    defaults.hideFlags = HideFlags.DontSave;
                }
                return defaults;
            }
        }
    }
}
