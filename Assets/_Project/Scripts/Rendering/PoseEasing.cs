namespace Margin.Rendering
{
    /// <summary>How a clip entry blends toward the next pose over its frames (spec 4.2).</summary>
    public enum PoseEasing
    {
        /// <summary>Hold this pose, then jump to the next. Use for crisp attack active frames.</summary>
        Snap,
        Linear,
        /// <summary>Starts slow, ends fast (wind-up into a strike).</summary>
        EaseIn,
        /// <summary>Starts fast, ends slow (settling into a pose).</summary>
        EaseOut,
        /// <summary>Slow at both ends (idle, breathing, anticipation).</summary>
        EaseInOut,
    }

    public static class PoseEasingMath
    {
        /// <summary>Maps linear progress u (0..1) through the easing curve.</summary>
        public static float Apply(PoseEasing easing, float u)
        {
            if (u <= 0f) return 0f;
            if (u >= 1f) return 1f;
            switch (easing)
            {
                case PoseEasing.Snap: return 0f;
                case PoseEasing.EaseIn: return u * u;
                case PoseEasing.EaseOut: return 1f - (1f - u) * (1f - u);
                case PoseEasing.EaseInOut: return u * u * (3f - 2f * u);
                default: return u;
            }
        }
    }
}
