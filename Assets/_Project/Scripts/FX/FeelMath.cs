using System;

namespace Margin.FX
{
    /// <summary>Pure math for game-feel effects, kept free of UnityEngine so it can be unit tested.</summary>
    public static class FeelMath
    {
        /// <summary>Signed smallest rotation from a to b, in degrees, in [-180, 180).</summary>
        public static float DeltaAngle(float a, float b)
        {
            float d = (b - a) % 360f;
            if (d >= 180f) d -= 360f;
            if (d < -180f) d += 360f;
            return d;
        }

        /// <summary>
        /// Screen shake strength remaining: quadratic falloff so hits snap hard then settle quickly.
        /// remaining and duration are in frames.
        /// </summary>
        public static float ShakeFalloff(float remaining, float duration)
        {
            if (duration <= 0f || remaining <= 0f) return 0f;
            float t = Math.Min(1f, remaining / duration);
            return t * t;
        }

        /// <summary>
        /// Inner radius of the smear crescent along its sweep (t = 0 oldest edge, 1 newest edge):
        /// a thin sliver at the old edge thickening toward the blade's current position.
        /// </summary>
        public static float SmearInnerRadius(float outerRadius, float thickInnerRatio, float t)
        {
            const float thinInnerRatio = 0.92f;
            float ratio = thinInnerRatio + (thickInnerRatio - thinInnerRatio) * Math.Max(0f, Math.Min(1f, t));
            return outerRadius * ratio;
        }

        /// <summary>Number of mesh segments for a smear sweep: about one per 8 degrees, 3 to 24.</summary>
        public static int SmearSegments(float sweepDegrees)
        {
            int n = (int)Math.Ceiling(Math.Abs(sweepDegrees) / 8f);
            return Math.Max(3, Math.Min(24, n));
        }

        /// <summary>Ink splatter particle count for a hit: bigger hits throw more ink.</summary>
        public static int SplatterCount(int baseCount, float perDamage, int damage)
        {
            return Math.Max(0, baseCount + (int)Math.Round(perDamage * damage));
        }
    }
}
