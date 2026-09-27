using System;

namespace Margin.Rendering
{
    /// <summary>
    /// Geometry of a held weapon, in pure C# so it can be unit tested. Everything is measured from the hand along
    /// the weapon's direction (the forearm's direction), so the tip is always exactly hand + direction * length:
    /// the point hitboxes, the tip trail and smears were tuned against.
    ///
    /// "Side" picks which perpendicular is the back of the blade (the side a katana curves toward). WeaponLine
    /// flips it with the figure's facing so the sword looks the same both ways.
    /// </summary>
    public static class WeaponShape
    {
        /// <summary>A 2D point (x, y).</summary>
        public readonly struct Pt
        {
            public readonly float X, Y;
            public Pt(float x, float y) { X = x; Y = y; }
            public static Pt operator +(Pt a, Pt b) => new Pt(a.X + b.X, a.Y + b.Y);
            public static Pt operator -(Pt a, Pt b) => new Pt(a.X - b.X, a.Y - b.Y);
            public static Pt operator *(Pt a, float s) => new Pt(a.X * s, a.Y * s);
        }

        /// <summary>Unit vector 90 degrees counter-clockwise from dir, times side (+1 or -1).</summary>
        public static Pt Perpendicular(Pt dir, float side) => new Pt(-dir.Y * side, dir.X * side);

        /// <summary>Where the curve bows most, as a fraction of the length (a katana's curve peaks past the middle).</summary>
        public const float CurvePeak = 0.6f;

        /// <summary>
        /// Blade centerline from the hand to the tip: <paramref name="points"/> points, bowed sideways by up to
        /// <paramref name="curve"/> (0 at both ends, most at CurvePeak). First point = hand, last = exact tip.
        /// </summary>
        public static Pt[] Blade(Pt hand, Pt dir, float length, float curve, float side, int points = 12)
        {
            points = Math.Max(2, points);
            Pt perp = Perpendicular(dir, side);
            // Bow shape sin(pi * t^p): 0 at the hilt and tip, 1 at t = CurvePeak (p chosen so CurvePeak^p = 0.5).
            double p = Math.Log(0.5) / Math.Log(CurvePeak);
            var result = new Pt[points];
            for (int i = 0; i < points; i++)
            {
                float t = i / (float)(points - 1);
                float bow = i == points - 1 ? 0f : (float)Math.Sin(Math.PI * Math.Pow(t, p));
                result[i] = hand + dir * (length * t) + perp * (curve * bow);
            }
            return result;
        }

        /// <summary>Guard (tsuba): a short bar across the weapon, centered on the hand.</summary>
        public static Pt[] Guard(Pt hand, Pt dir, float guardLength)
        {
            Pt half = Perpendicular(dir, 1f) * (guardLength * 0.5f);
            return new[] { hand - half, hand + half };
        }

        /// <summary>Handle: from the hand straight back (behind the fist).</summary>
        public static Pt[] Handle(Pt hand, Pt dir, float handleLength) => new[] { hand, hand - dir * handleLength };

        /// <summary>The pieces of a doodled pencil held at the hand, pointing along dir.</summary>
        public sealed class Pencil
        {
            public Pt[] TopEdge, BottomEdge, Facet, Cone, Graphite, Eraser, Band;
        }

        /// <summary>
        /// A pencil from <paramref name="backLength"/> behind the hand to the tip at hand + dir * length:
        /// eraser (a box) and metal band at the back, a six-sided body (two edges and a facet line), a sharpened
        /// cone and a short graphite point ending exactly at the tip.
        /// </summary>
        public static Pencil PencilShape(Pt hand, Pt dir, float length, float backLength, float bodyWidth,
                                         float coneLength, float eraserLength)
        {
            Pt perp = Perpendicular(dir, 1f);
            Pt half = perp * (bodyWidth * 0.5f);
            Pt back = hand - dir * backLength;
            Pt band = back + dir * eraserLength;
            Pt coneStart = hand + dir * (length - coneLength);
            Pt tip = hand + dir * length;
            float graphite = coneLength * 0.3f;
            Pt graphiteStart = tip - dir * graphite;

            return new Pencil
            {
                TopEdge = new[] { band + half, coneStart + half },
                BottomEdge = new[] { band - half, coneStart - half },
                Facet = new[] { band + half * 0.3f, coneStart + half * 0.3f },
                Cone = new[] { coneStart + half, graphiteStart + half * 0.3f, graphiteStart - half * 0.3f, coneStart - half },
                Graphite = new[] { graphiteStart, tip },
                Eraser = new[] { back + half, band + half, band - half, back - half },
                Band = new[] { band + dir * (eraserLength * 0.35f) + half, band + dir * (eraserLength * 0.35f) - half },
            };
        }
    }
}
