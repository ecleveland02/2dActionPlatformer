using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using Pt = Margin.Rendering.WeaponShape.Pt;

namespace Margin.UI
{
    /// <summary>
    /// Draws InkLines shapes with UI Toolkit's vector painter (Painter2D): wobbly paper fills, and ink strokes whose
    /// width changes along the line like pen pressure. Used by the ink elements' generateVisualContent callbacks.
    /// </summary>
    public static class InkPainter
    {
        /// <summary>
        /// Which "boil" frame we're on: changes every settings.boilFrames frames of real time (so it keeps moving on
        /// the pause menu), or stays 0 when boil is off. Mixed into every seed.
        /// </summary>
        public static int BoilSeed(UISettings s) =>
            s.boil ? (int)(Time.unscaledTime * 60f / Mathf.Max(1, s.boilFrames)) : 0;

        public static Vector2 V(Pt p) => new Vector2(p.X, p.Y);

        /// <summary>Fills a closed outline.</summary>
        public static void Fill(Painter2D painter, List<Pt> outline, Color color)
        {
            if (outline.Count < 3 || color.a <= 0f) return;
            painter.fillColor = color;
            painter.BeginPath();
            painter.MoveTo(V(outline[0]));
            for (int i = 1; i < outline.Count; i++) painter.LineTo(V(outline[i]));
            painter.ClosePath();
            painter.Fill();
        }

        /// <summary>
        /// Strokes an open line segment by segment so each piece can have its own width (pen pressure).
        /// Round caps hide the joins between pieces.
        /// </summary>
        public static void Stroke(Painter2D painter, List<Pt> line, Color color, UISettings s, int seed, float widthScale = 1f)
        {
            if (line.Count < 2 || color.a <= 0f) return;
            painter.strokeColor = color;
            painter.lineCap = LineCap.Round;
            painter.lineJoin = LineJoin.Round;
            for (int i = 0; i < line.Count - 1; i++)
            {
                float t = (i + 0.5f) / (line.Count - 1);
                painter.lineWidth = InkLines.StrokeWidth(s.lineWidth * widthScale, s.lineWidthVariation, seed, t);
                painter.BeginPath();
                painter.MoveTo(V(line[i]));
                painter.LineTo(V(line[i + 1]));
                painter.Stroke();
            }
        }

        /// <summary>A sketched box: four wobbly strokes that cross at the corners.</summary>
        public static void SketchBox(Painter2D painter, Rect r, Color color, UISettings s, int seed, float widthScale = 1f)
        {
            List<List<Pt>> sides = InkLines.SketchBox(r.x, r.y, r.width, r.height, s.wobble, s.overshoot, s.wobbleStep, seed);
            for (int i = 0; i < sides.Count; i++) Stroke(painter, sides[i], color, s, seed * 5 + i, widthScale);
        }

        /// <summary>A wobbly filled box (paper cards, bar fills). Skipped when too thin to see.</summary>
        public static void FillBox(Painter2D painter, Rect r, Color color, UISettings s, int seed, float wobbleScale = 1f)
        {
            if (r.width < 0.5f || r.height < 0.5f) return;
            Fill(painter, InkLines.WobblyOutline(r.x, r.y, r.width, r.height, s.wobble * wobbleScale, s.wobbleStep, seed), color);
        }

        /// <summary>One hand-drawn line.</summary>
        public static void Line(Painter2D painter, Vector2 a, Vector2 b, Color color, UISettings s, int seed, float widthScale = 1f)
        {
            Stroke(painter, InkLines.WobblyLine(new Pt(a.x, a.y), new Pt(b.x, b.y), s.wobble, s.wobbleStep, seed),
                   color, s, seed, widthScale);
        }
    }
}
