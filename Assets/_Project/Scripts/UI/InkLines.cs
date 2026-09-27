using System;
using System.Collections.Generic;
using Pt = Margin.Rendering.WeaponShape.Pt;

namespace Margin.UI
{
    /// <summary>
    /// Hand-drawn line shapes for the UI (spec 14: "hand-drawn boxes, uneven lines"), as pure math so it can be
    /// unit tested. Everything is seeded: the same seed always gives the same wobble, so a box only changes shape
    /// when the caller picks a new seed (the HUD does that a few times a second for a subtle "boil").
    /// Coordinates are whatever the caller uses (UI Toolkit: pixels, y down).
    /// </summary>
    public static class InkLines
    {
        /// <summary>Repeatable pseudo-random number in [-1, 1] for (seed, index). No allocations, no state.</summary>
        public static float Noise(int seed, int index)
        {
            unchecked
            {
                uint h = (uint)seed * 0x9E3779B1u ^ (uint)index * 0x85EBCA77u;
                h ^= h >> 15;
                h *= 0x2C1B3C6Du;
                h ^= h >> 12;
                h *= 0x297A2D39u;
                h ^= h >> 15;
                return (h & 0xFFFFFF) / (float)0xFFFFFF * 2f - 1f;
            }
        }

        /// <summary>Smooth noise: blends between the random values at whole numbers, so wobbles are gentle curves.</summary>
        public static float SmoothNoise(int seed, float t)
        {
            int i = (int)Math.Floor(t);
            float f = t - i;
            f = f * f * (3f - 2f * f);   // smoothstep
            return Noise(seed, i) + (Noise(seed, i + 1) - Noise(seed, i)) * f;
        }

        /// <summary>
        /// A line from a to b drawn by an unsteady hand: points every <paramref name="step"/> units, each pushed
        /// sideways by up to <paramref name="amplitude"/>. The ends move too (half as much), like a real pen stroke.
        /// </summary>
        public static List<Pt> WobblyLine(Pt a, Pt b, float amplitude, float step, int seed)
        {
            float dx = b.X - a.X, dy = b.Y - a.Y;
            float length = (float)Math.Sqrt(dx * dx + dy * dy);
            int segments = Math.Max(1, (int)Math.Ceiling(length / Math.Max(0.01f, step)));
            // Unit vector across the line: the direction the wobble pushes.
            float nx = length > 0f ? -dy / length : 0f, ny = length > 0f ? dx / length : 1f;

            var points = new List<Pt>(segments + 1);
            for (int i = 0; i <= segments; i++)
            {
                float t = i / (float)segments;
                float end = i == 0 || i == segments ? 0.5f : 1f;
                float push = SmoothNoise(seed, t * segments * 0.5f) * amplitude * end;
                points.Add(new Pt(a.X + dx * t + nx * push, a.Y + dy * t + ny * push));
            }
            return points;
        }

        /// <summary>
        /// The four sides of a box as separate strokes, each overshooting its corners a little so the lines cross
        /// like a quick sketch. Order: top, right, bottom, left.
        /// </summary>
        public static List<List<Pt>> SketchBox(float x, float y, float width, float height, float amplitude,
                                               float overshoot, float step, int seed)
        {
            Pt tl = new Pt(x, y), tr = new Pt(x + width, y), br = new Pt(x + width, y + height), bl = new Pt(x, y + height);
            return new List<List<Pt>>
            {
                Side(tl, tr, amplitude, overshoot, step, seed * 4 + 1),
                Side(tr, br, amplitude, overshoot, step, seed * 4 + 2),
                Side(br, bl, amplitude, overshoot, step, seed * 4 + 3),
                Side(bl, tl, amplitude, overshoot, step, seed * 4 + 4),
            };
        }

        /// <summary>
        /// A closed outline around a box (for paper fills and bar fills): the edge wobbles but the corners stay put,
        /// so the fill always covers the box it was asked for.
        /// </summary>
        public static List<Pt> WobblyOutline(float x, float y, float width, float height, float amplitude, float step, int seed)
        {
            Pt tl = new Pt(x, y), tr = new Pt(x + width, y), br = new Pt(x + width, y + height), bl = new Pt(x, y + height);
            var outline = new List<Pt>();
            AddEdge(outline, tl, tr, amplitude, step, seed * 4 + 1);
            AddEdge(outline, tr, br, amplitude, step, seed * 4 + 2);
            AddEdge(outline, br, bl, amplitude, step, seed * 4 + 3);
            AddEdge(outline, bl, tl, amplitude, step, seed * 4 + 4);
            return outline;
        }

        /// <summary>Pen pressure: a stroke width that varies a little along the line (0 to 1 along it).</summary>
        public static float StrokeWidth(float baseWidth, float variation, int seed, float t) =>
            Math.Max(0.1f, baseWidth * (1f + variation * SmoothNoise(seed, t * 3f)));

        private static List<Pt> Side(Pt a, Pt b, float amplitude, float overshoot, float step, int seed)
        {
            float dx = b.X - a.X, dy = b.Y - a.Y;
            float length = (float)Math.Sqrt(dx * dx + dy * dy);
            if (length <= 0f) return WobblyLine(a, b, amplitude, step, seed);
            float ux = dx / length, uy = dy / length;
            // Each end overshoots by a different random amount (0.3x to 1x the overshoot).
            float o1 = overshoot * (0.65f + 0.35f * Noise(seed, 1000));
            float o2 = overshoot * (0.65f + 0.35f * Noise(seed, 2000));
            return WobblyLine(new Pt(a.X - ux * o1, a.Y - uy * o1), new Pt(b.X + ux * o2, b.Y + uy * o2), amplitude, step, seed);
        }

        private static void AddEdge(List<Pt> outline, Pt a, Pt b, float amplitude, float step, int seed)
        {
            List<Pt> edge = WobblyLine(a, b, amplitude, step, seed);
            // Pin the corners, and skip the last point: it's the first point of the next edge.
            edge[0] = a;
            for (int i = 0; i < edge.Count - 1; i++) outline.Add(edge[i]);
        }
    }
}
