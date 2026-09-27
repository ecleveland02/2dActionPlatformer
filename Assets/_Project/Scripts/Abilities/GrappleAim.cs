using System;

namespace Margin.Abilities
{
    /// <summary>
    /// Which grapple target the line goes to (spec 8), in pure C# for testing. Angles are measured from straight
    /// up, positive toward the facing direction: 0 = overhead, 90 = straight ahead, 180 = straight down.
    /// A lower score is a better target; -1 means out of reach or outside the aim window.
    /// </summary>
    public static class GrappleAim
    {
        /// <summary>The target's angle from straight up toward facing, in degrees (-180..180).</summary>
        public static float AngleFromUp(float dx, float dy, int facing) =>
            (float)(Math.Atan2(dx * (facing < 0 ? -1f : 1f), dy) * 180.0 / Math.PI);

        /// <summary>
        /// Scores a target at (dx, dy) from the hand. Nearer is better, and so is up-and-ahead (45 degrees), which is
        /// where a swing works best. Targets up to 15 degrees behind straight up still count.
        /// </summary>
        public static float Score(float dx, float dy, int facing, float range, float minAngle, float maxAngle)
        {
            float distance = (float)Math.Sqrt(dx * dx + dy * dy);
            if (distance > range || distance < 0.01f) return -1f;
            float angle = AngleFromUp(dx, dy, facing);
            if (angle < minAngle || angle > maxAngle) return -1f;
            return distance + Math.Abs(angle - 45f) * 0.03f;
        }
    }
}
