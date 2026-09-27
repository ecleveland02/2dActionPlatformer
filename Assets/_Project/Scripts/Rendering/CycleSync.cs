using System;
using System.Collections.Generic;

namespace Margin.Rendering
{
    /// <summary>
    /// Keeps feet from sliding (pure C# for testing). A run cycle moves a planted foot backward at a fixed speed;
    /// if the body moves faster or slower than that, the foot skates. So the cycle's playback rate follows the
    /// movement speed: rate = speed / (stride per cycle) cycles per second, converted to ticks per tick.
    /// </summary>
    public static class CycleSync
    {
        /// <summary>
        /// Ticks of the clip to advance per game tick (1 = normal). Returns 1 when the clip has no stride length.
        /// </summary>
        public static float Rate(float speed, float strideLength, int totalFrames, float minRate, float maxRate)
        {
            if (strideLength <= 0f || totalFrames <= 0) return 1f;
            float rate = Math.Abs(speed) * totalFrames / (60f * strideLength);
            return Math.Max(minRate, Math.Min(maxRate, rate));
        }

        /// <summary>
        /// Position of a foot relative to the neutral hips position (units, right-facing), from the pose's angles
        /// and the leg bone lengths. Includes the root offset and whole-body tilt.
        /// </summary>
        public static (float x, float y) FootPosition(FigurePose pose, bool front, float thigh, float shin)
        {
            float hip = front ? pose.hipFront : pose.hipBack;
            float knee = front ? pose.kneeFront : pose.kneeBack;
            // Angles from straight down, positive toward facing (+x). The tilt adds to both bones.
            float a = (pose.rootRotation + hip) * (float)Math.PI / 180f;
            float b = (pose.rootRotation + hip + knee) * (float)Math.PI / 180f;
            float x = pose.rootOffsetX + thigh * (float)Math.Sin(a) + shin * (float)Math.Sin(b);
            float y = pose.rootOffsetY - thigh * (float)Math.Cos(a) - shin * (float)Math.Cos(b);
            return (x, y);
        }

        /// <summary>
        /// Distance the body travels in one full cycle when the planted foot doesn't slide. Samples every tick
        /// of the loop; wherever a foot stays on the ground (its lowest height, within tolerance) from one tick to
        /// the next, its backward movement is the ground speed. Returns 0 if no foot is ever planted.
        /// </summary>
        public static float StrideLength(PoseTimeline cycle, float thigh, float shin, float tolerance = 0.015f)
        {
            int n = cycle.TotalFrames;
            var front = new List<(float x, float y)>(n + 1);
            var back = new List<(float x, float y)>(n + 1);
            float ground = float.MaxValue;
            for (int i = 0; i <= n; i++)
            {
                FigurePose p = cycle.Sample(i);
                front.Add(FootPosition(p, true, thigh, shin));
                back.Add(FootPosition(p, false, thigh, shin));
                ground = Math.Min(ground, Math.Min(front[i].y, back[i].y));
            }

            float travel = 0f;
            int samples = 0;
            foreach (List<(float x, float y)> foot in new[] { front, back })
            {
                for (int i = 0; i < n; i++)
                {
                    bool planted = foot[i].y <= ground + tolerance && foot[i + 1].y <= ground + tolerance;
                    if (!planted) continue;
                    travel += foot[i].x - foot[i + 1].x;   // a planted foot slides backward as the body goes forward
                    samples++;
                }
            }
            return samples == 0 ? 0f : travel / samples * n;
        }
    }
}
