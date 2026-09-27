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

        /// <summary>
        /// Catmull-Rom spline between p1 (t = 0) and p2 (t = 1), shaped by the poses before (p0) and after (p3).
        /// The curve passes exactly through every keyframe and its speed changes smoothly across them, so a cycle
        /// of keyframes plays as one flowing motion. Angles are unwrapped first so they take the short way round.
        /// </summary>
        public static FigurePose CatmullRom(FigurePose p0, FigurePose p1, FigurePose p2, FigurePose p3, float t)
        {
            if (t <= 0f) return p1;
            if (t >= 1f) return p2;

            var result = new FigurePose
            {
                rootOffsetX = Spline(p0.rootOffsetX, p1.rootOffsetX, p2.rootOffsetX, p3.rootOffsetX, t),
                rootOffsetY = Spline(p0.rootOffsetY, p1.rootOffsetY, p2.rootOffsetY, p3.rootOffsetY, t),
            };
            foreach (PoseJoint joint in AllJoints)
            {
                float a1 = p1.Get(joint);
                float a0 = a1 + DeltaAngle(a1, p0.Get(joint));
                float a2 = a1 + DeltaAngle(a1, p2.Get(joint));
                float a3 = a2 + DeltaAngle(p2.Get(joint), p3.Get(joint));
                result.Set(joint, NormalizeAngle(Spline(a0, a1, a2, a3, t)));
            }
            return result;
        }

        /// <summary>Uniform Catmull-Rom for one value.</summary>
        private static float Spline(float v0, float v1, float v2, float v3, float t)
        {
            float t2 = t * t, t3 = t2 * t;
            return 0.5f * (2f * v1 + (v2 - v0) * t + (2f * v0 - 5f * v1 + 4f * v2 - v3) * t2 + (3f * v1 - v0 - 3f * v2 + v3) * t3);
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

        /// <summary>The difference a - b per joint (shortest angles). Used for additive layers such as breathing.</summary>
        public static FigurePose Subtract(FigurePose a, FigurePose b)
        {
            var d = new FigurePose { rootOffsetX = a.rootOffsetX - b.rootOffsetX, rootOffsetY = a.rootOffsetY - b.rootOffsetY };
            foreach (PoseJoint joint in AllJoints) d.Set(joint, DeltaAngle(b.Get(joint), a.Get(joint)));
            return d;
        }

        /// <summary>Adds a difference (from Subtract) on top of a pose, scaled by weight.</summary>
        public static FigurePose Add(FigurePose pose, FigurePose delta, float weight)
        {
            var r = new FigurePose
            {
                rootOffsetX = pose.rootOffsetX + delta.rootOffsetX * weight,
                rootOffsetY = pose.rootOffsetY + delta.rootOffsetY * weight,
            };
            foreach (PoseJoint joint in AllJoints) r.Set(joint, pose.Get(joint) + delta.Get(joint) * weight);
            return r;
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
