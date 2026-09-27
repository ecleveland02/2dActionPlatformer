using System;

namespace Margin.Player
{
    /// <summary>
    /// N+-style air posing (pure C# for testing): instead of switching jump / apex / fall clips at thresholds, the
    /// pose flows with vertical speed. Rising fast = the jump pose; slowing toward the top blends into the apex pose;
    /// falling blends from apex into the fall loop as downward speed builds. Continuous at every speed, so there
    /// are no pops at the top of a jump.
    /// </summary>
    public static class AirPoseBlend
    {
        /// <summary>
        /// For vertical speed vy (units/s, + = up): whether the base is the jump clip (rising) or the apex pose
        /// (falling), and how much of the next pose to blend in (0..1): apex while rising, fall loop while falling.
        /// riseSpeed: speed at which the jump pose is pure; fallSpeed: downward speed at which the fall pose is pure.
        /// </summary>
        public static (bool rising, float weight) Weights(float vy, float riseSpeed, float fallSpeed)
        {
            if (vy > 0f)
            {
                float t = riseSpeed > 0f ? 1f - Math.Min(1f, vy / riseSpeed) : 1f;
                return (true, Smooth(t));
            }
            float f = fallSpeed > 0f ? Math.Min(1f, -vy / fallSpeed) : 1f;
            return (false, Smooth(f));
        }

        private static float Smooth(float t) => t * t * (3f - 2f * t);
    }
}
