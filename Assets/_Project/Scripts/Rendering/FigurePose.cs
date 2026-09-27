using System;

namespace Margin.Rendering
{
    /// <summary>
    /// One keyframe of the stick figure: an angle (degrees, relative to the parent bone) for each
    /// PoseJoint, plus a root offset that shifts the hips (crouch, bob). Pure C# so it can be unit tested.
    /// Angle rule: positive = toward the facing direction (see PoseJoint).
    /// </summary>
    [Serializable]
    public struct FigurePose
    {
        public float rootOffsetX;
        public float rootOffsetY;
        public float spine;
        public float neck;
        public float shoulderFront;
        public float elbowFront;
        public float shoulderBack;
        public float elbowBack;
        public float hipFront;
        public float kneeFront;
        public float hipBack;
        public float kneeBack;

        /// <summary>Standing straight, arms hanging, legs straight, hips at the default height.</summary>
        public static FigurePose Neutral => default;

        public float Get(PoseJoint joint)
        {
            switch (joint)
            {
                case PoseJoint.Spine: return spine;
                case PoseJoint.Neck: return neck;
                case PoseJoint.ShoulderFront: return shoulderFront;
                case PoseJoint.ElbowFront: return elbowFront;
                case PoseJoint.ShoulderBack: return shoulderBack;
                case PoseJoint.ElbowBack: return elbowBack;
                case PoseJoint.HipFront: return hipFront;
                case PoseJoint.KneeFront: return kneeFront;
                case PoseJoint.HipBack: return hipBack;
                case PoseJoint.KneeBack: return kneeBack;
                default: throw new ArgumentOutOfRangeException(nameof(joint), joint, null);
            }
        }

        public void Set(PoseJoint joint, float degrees)
        {
            switch (joint)
            {
                case PoseJoint.Spine: spine = degrees; break;
                case PoseJoint.Neck: neck = degrees; break;
                case PoseJoint.ShoulderFront: shoulderFront = degrees; break;
                case PoseJoint.ElbowFront: elbowFront = degrees; break;
                case PoseJoint.ShoulderBack: shoulderBack = degrees; break;
                case PoseJoint.ElbowBack: elbowBack = degrees; break;
                case PoseJoint.HipFront: hipFront = degrees; break;
                case PoseJoint.KneeFront: kneeFront = degrees; break;
                case PoseJoint.HipBack: hipBack = degrees; break;
                case PoseJoint.KneeBack: kneeBack = degrees; break;
                default: throw new ArgumentOutOfRangeException(nameof(joint), joint, null);
            }
        }

        /// <summary>
        /// Blends two poses. t = 0 gives a, t = 1 gives b exactly. Angles take the shortest way around,
        /// so blending 170° to -170° passes through 180°, not through 0°.
        /// </summary>
        public static FigurePose Lerp(FigurePose a, FigurePose b, float t)
        {
            if (t <= 0f) return a;
            if (t >= 1f) return b;

            var result = new FigurePose
            {
                rootOffsetX = a.rootOffsetX + (b.rootOffsetX - a.rootOffsetX) * t,
                rootOffsetY = a.rootOffsetY + (b.rootOffsetY - a.rootOffsetY) * t,
            };
            foreach (PoseJoint joint in AllJoints)
                result.Set(joint, LerpAngle(a.Get(joint), b.Get(joint), t));
            return result;
        }

        /// <summary>Swaps front and back limbs (e.g. turns run pose 1 into its opposite-leg twin).</summary>
        public FigurePose Mirrored()
        {
            FigurePose m = this;
            m.shoulderFront = shoulderBack; m.shoulderBack = shoulderFront;
            m.elbowFront = elbowBack;       m.elbowBack = elbowFront;
            m.hipFront = hipBack;           m.hipBack = hipFront;
            m.kneeFront = kneeBack;         m.kneeBack = kneeFront;
            return m;
        }

        public static readonly PoseJoint[] AllJoints = (PoseJoint[])Enum.GetValues(typeof(PoseJoint));

        /// <summary>Signed difference b - a wrapped into [-180, 180).</summary>
        public static float DeltaAngle(float a, float b)
        {
            float d = (b - a) % 360f;
            if (d >= 180f) d -= 360f;
            if (d < -180f) d += 360f;
            return d;
        }

        /// <summary>Wraps an angle into [-180, 180).</summary>
        public static float NormalizeAngle(float degrees) => DeltaAngle(0f, degrees);

        public static float LerpAngle(float a, float b, float t) => NormalizeAngle(a + DeltaAngle(a, b) * t);
    }
}
