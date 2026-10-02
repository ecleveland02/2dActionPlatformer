using System;
using System.Collections.Generic;

namespace Margin.Bosses
{
    /// <summary>
    /// The Stapler Titan's aiming as pure math (no UnityEngine, unit tested): where a Hop Slam lands and how fast it
    /// has to leave the ground to get there, and where Staple Rain's lanes fall.
    /// </summary>
    public static class StaplerMath
    {
        /// <summary>
        /// A hop from <paramref name="fromX"/> toward <paramref name="targetX"/> that peaks <paramref name="height"/>
        /// above the take-off and lands back at that height after <paramref name="airFrames"/> ticks. The hop is at
        /// most <paramref name="maxDistance"/> long and lands between <paramref name="minX"/> and <paramref name="maxX"/>.
        /// Gives the launch speeds and the gravity to use (its own, so the arc doesn't depend on the player's).
        /// </summary>
        public static void HopVelocity(float fromX, float targetX, float minX, float maxX, float maxDistance, int airFrames,
                                       float height, float tickDelta, out float vx, out float vy, out float gravity, out float landX)
        {
            float dx = Clamp(targetX - fromX, -maxDistance, maxDistance);
            landX = Clamp(fromX + dx, minX, maxX);
            float time = Math.Max(1, airFrames) * tickDelta;
            vx = (landX - fromX) / time;
            // Up to the peak in half the time and back down: h = g (t/2)^2 / 2, so g = 8h / t^2 and v = g t / 2.
            gravity = 8f * height / (time * time);
            vy = gravity * time * 0.5f;
        }

        /// <summary>
        /// Staple Rain lanes: one right on the player, the rest spread across [minX, maxX] at least
        /// <paramref name="spacing"/> apart (fewer if they don't fit), placed by a seeded shuffle.
        /// </summary>
        public static List<float> RainLanes(float playerX, float minX, float maxX, int count, float spacing, uint seed)
        {
            var lanes = new List<float>();
            if (count <= 0 || maxX < minX) return lanes;
            lanes.Add(Clamp(playerX, minX, maxX));
            // Candidate spots every `spacing` across the arena, then a seeded shuffle picks from them.
            var spots = new List<float>();
            for (float x = minX; x <= maxX + 0.0001f; x += Math.Max(0.1f, spacing)) spots.Add(x);
            uint state = seed == 0 ? 1u : seed;
            for (int i = spots.Count - 1; i > 0; i--)
            {
                state ^= state << 13;
                state ^= state >> 17;
                state ^= state << 5;
                int j = (int)(state % (uint)(i + 1));
                float t = spots[i];
                spots[i] = spots[j];
                spots[j] = t;
            }
            foreach (float x in spots)
            {
                if (lanes.Count >= count) break;
                bool clear = true;
                foreach (float l in lanes) clear &= Math.Abs(l - x) >= spacing - 0.0001f;
                if (clear) lanes.Add(x);
            }
            return lanes;
        }

        private static float Clamp(float v, float lo, float hi) => v < lo ? lo : v > hi ? hi : v;
    }
}
