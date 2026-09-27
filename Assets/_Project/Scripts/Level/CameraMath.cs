using System;

namespace Margin.Level
{
    /// <summary>
    /// The camera's rules (spec 12) as plain math on one axis at a time, so they can be unit tested without Unity.
    /// CameraFollow runs them each rendered frame: look-ahead, dead zone, grounded recentering, then the room confiner.
    /// </summary>
    public static class CameraMath
    {
        /// <summary>
        /// Dead zone: the camera only moves when the target leaves the zone around it, and then only as far as needed
        /// to bring the target back to the zone's edge. <paramref name="below"/> and <paramref name="above"/> are the
        /// zone's reach on each side of the camera (use the same value twice for a centered zone).
        /// </summary>
        public static float DeadZone(float camera, float target, float below, float above)
        {
            if (target > camera + above) return target - above;
            if (target < camera - below) return target + below;
            return camera;
        }

        /// <summary>
        /// Keeps the view inside a room: the view is 2 x <paramref name="halfView"/> wide and may not show anything
        /// outside [min, max]. A room smaller than the view is centered instead.
        /// </summary>
        public static float Confine(float center, float halfView, float min, float max)
        {
            if (max - min <= halfView * 2f) return (min + max) * 0.5f;
            return Math.Max(min + halfView, Math.Min(max - halfView, center));
        }

        /// <summary>
        /// Eases toward a target, covering about 63% of the distance every <paramref name="frames"/> frames
        /// (exponential, so it gives the same motion at any frame rate). frames &lt;= 0 snaps.
        /// </summary>
        public static float Approach(float current, float target, float frames, float elapsedFrames)
        {
            if (frames <= 0f) return target;
            if (elapsedFrames <= 0f) return current;
            float t = 1f - (float)Math.Exp(-elapsedFrames / frames);
            return current + (target - current) * t;
        }
    }
}
