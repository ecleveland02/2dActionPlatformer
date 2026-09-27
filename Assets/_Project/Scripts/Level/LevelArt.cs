using System;
using System.Collections.Generic;
using Margin.UI;
using Pt = Margin.Rendering.WeaponShape.Pt;

namespace Margin.Level
{
    /// <summary>The background doodles drawn in the margins of World 1's pages (spec 11.3 midground).</summary>
    public enum DoodleKind { Spiral, Star, Cube, Arrow, Cloud, Flower, Heart, Bolt, Smiley, Wave }

    /// <summary>
    /// The level's hand-drawn shapes as pure math (spec 4: everything procedural, no sprites): hatching that fills
    /// solid ground, margin doodles, and eraser smudges. Each shape is a list of pen strokes (point lists) centered
    /// on (0, 0), in units. The level builder and LevelBlock turn strokes into LineRenderers.
    /// </summary>
    public static class LevelArt
    {
        private const float Root2 = 1.41421356f;

        /// <summary>
        /// 45 degree hatching across a <paramref name="width"/> x <paramref name="height"/> box centered on 0, drawn as
        /// one back-and-forth pen stroke (the turns run along the box's edges). <paramref name="spacing"/> is the
        /// gap between hatch lines.
        /// </summary>
        public static List<Pt> Hatch(float width, float height, float spacing)
        {
            var points = new List<Pt>();
            float hw = width * 0.5f, hh = height * 0.5f;
            float step = Math.Max(0.02f, spacing) * Root2;   // lines x - y = c, measured along x
            bool reverse = false;
            for (float c = -hw - hh + step * 0.5f; c < hw + hh; c += step)
            {
                // The part of the line y = x - c inside the box.
                float x0 = Math.Max(-hw, c - hh), x1 = Math.Min(hw, c + hh);
                if (x1 - x0 < 0.0001f) continue;
                var a = new Pt(x0, x0 - c);
                var b = new Pt(x1, x1 - c);
                if (reverse)
                {
                    points.Add(b);
                    points.Add(a);
                }
                else
                {
                    points.Add(a);
                    points.Add(b);
                }
                reverse = !reverse;
            }
            return points;
        }

        /// <summary>A doodle about <paramref name="size"/> across, as pen strokes.</summary>
        public static List<List<Pt>> Doodle(DoodleKind kind, float size)
        {
            float s = size * 0.5f;
            var strokes = new List<List<Pt>>();
            switch (kind)
            {
                case DoodleKind.Spiral:
                {
                    var spiral = new List<Pt>();
                    const float turns = 2.5f;
                    for (float t = 0f; t <= turns * 2f * (float)Math.PI; t += 0.25f)
                    {
                        float r = s * t / (turns * 2f * (float)Math.PI);
                        spiral.Add(new Pt(r * (float)Math.Cos(t), r * (float)Math.Sin(t)));
                    }
                    strokes.Add(spiral);
                    break;
                }
                case DoodleKind.Star:
                {
                    var star = new List<Pt>();
                    for (int i = 0; i <= 10; i++)
                    {
                        float a = (float)Math.PI * 0.5f + i * (float)Math.PI / 5f;
                        float r = i % 2 == 0 ? s : s * 0.42f;
                        star.Add(new Pt(r * (float)Math.Cos(a), r * (float)Math.Sin(a)));
                    }
                    strokes.Add(star);
                    break;
                }
                case DoodleKind.Cube:
                {
                    float c = s * 0.62f, d = s * 0.36f;
                    strokes.Add(Square(-d * 0.5f, -d * 0.5f, c));
                    strokes.Add(Square(d * 0.5f, d * 0.5f, c));
                    foreach (var corner in new[] { new Pt(-1f, -1f), new Pt(1f, -1f), new Pt(1f, 1f), new Pt(-1f, 1f) })
                        strokes.Add(new List<Pt>
                        {
                            new Pt(-d * 0.5f + corner.X * c, -d * 0.5f + corner.Y * c),
                            new Pt(d * 0.5f + corner.X * c, d * 0.5f + corner.Y * c),
                        });
                    break;
                }
                case DoodleKind.Arrow:
                {
                    var shaft = new List<Pt>();
                    for (int i = 0; i <= 8; i++)
                    {
                        float t = i / 8f;
                        shaft.Add(new Pt(-s + 2f * s * t, s * 0.25f * (float)Math.Sin(t * Math.PI)));
                    }
                    strokes.Add(shaft);
                    strokes.Add(new List<Pt> { new Pt(s * 0.55f, s * 0.35f), new Pt(s, 0f), new Pt(s * 0.55f, -s * 0.3f) });
                    break;
                }
                case DoodleKind.Cloud:
                {
                    var cloud = new List<Pt>();
                    float[] cx = { -0.55f, -0.1f, 0.45f }, cr = { 0.35f, 0.5f, 0.38f };
                    for (int b = 0; b < 3; b++)
                        for (int i = 0; i <= 8; i++)
                        {
                            float a = (float)Math.PI * (1f - i / 8f);
                            cloud.Add(new Pt(s * (cx[b] + cr[b] * (float)Math.Cos(a)), s * (cr[b] * (float)Math.Sin(a) - 0.1f)));
                        }
                    cloud.Add(new Pt(s * (cx[0] - cr[0]), -s * 0.1f));
                    strokes.Add(cloud);
                    break;
                }
                case DoodleKind.Flower:
                {
                    // A five-petal rose curve r = cos(5t) over the top of a stem.
                    var petals = new List<Pt>();
                    float r0 = s * 0.55f, cy = s * 0.35f;
                    for (float t = 0f; t <= (float)Math.PI + 0.001f; t += (float)Math.PI / 60f)
                    {
                        float r = r0 * (float)Math.Cos(5f * t);
                        petals.Add(new Pt(r * (float)Math.Cos(t), cy + r * (float)Math.Sin(t)));
                    }
                    strokes.Add(petals);
                    strokes.Add(new List<Pt> { new Pt(0f, cy - s * 0.1f), new Pt(s * 0.05f, -s * 0.4f), new Pt(-s * 0.02f, -s) });
                    strokes.Add(new List<Pt> { new Pt(s * 0.03f, -s * 0.6f), new Pt(s * 0.35f, -s * 0.45f), new Pt(s * 0.04f, -s * 0.72f) });
                    break;
                }
                case DoodleKind.Heart:
                {
                    var heart = new List<Pt>();
                    for (int i = 0; i <= 40; i++)
                    {
                        double t = i / 40.0 * 2.0 * Math.PI;
                        double x = 16.0 * Math.Pow(Math.Sin(t), 3);
                        double y = 13.0 * Math.Cos(t) - 5.0 * Math.Cos(2 * t) - 2.0 * Math.Cos(3 * t) - Math.Cos(4 * t);
                        heart.Add(new Pt((float)(x / 17.0) * s, (float)((y + 2.0) / 17.0) * s));
                    }
                    strokes.Add(heart);
                    break;
                }
                case DoodleKind.Bolt:
                    strokes.Add(new List<Pt>
                    {
                        new Pt(s * 0.2f, s), new Pt(-s * 0.35f, s * 0.05f), new Pt(s * 0.1f, s * 0.05f),
                        new Pt(-s * 0.25f, -s), new Pt(s * 0.4f, s * 0.2f), new Pt(-s * 0.05f, s * 0.2f), new Pt(s * 0.2f, s),
                    });
                    break;
                case DoodleKind.Smiley:
                {
                    strokes.Add(Circle(0f, 0f, s, 24));
                    strokes.Add(new List<Pt> { new Pt(-s * 0.35f, s * 0.2f), new Pt(-s * 0.35f, s * 0.42f) });
                    strokes.Add(new List<Pt> { new Pt(s * 0.35f, s * 0.2f), new Pt(s * 0.35f, s * 0.42f) });
                    var mouth = new List<Pt>();
                    for (int i = 0; i <= 10; i++)
                    {
                        float a = (float)Math.PI * (1.15f + 0.7f * i / 10f);
                        mouth.Add(new Pt(s * 0.55f * (float)Math.Cos(a), s * 0.55f * (float)Math.Sin(a)));
                    }
                    strokes.Add(mouth);
                    break;
                }
                default:
                {
                    var wave = new List<Pt>();
                    for (int i = 0; i <= 24; i++)
                    {
                        float t = i / 24f;
                        wave.Add(new Pt(-s + 2f * s * t, s * 0.35f * (float)Math.Sin(t * 4f * Math.PI)));
                    }
                    strokes.Add(wave);
                    break;
                }
            }
            return strokes;
        }

        /// <summary>
        /// An eraser smudge (spec 11.3 foreground): a loose back-and-forth scribble filling roughly an ellipse
        /// <paramref name="width"/> x <paramref name="height"/>.
        /// </summary>
        public static List<Pt> Smudge(float width, float height, int seed)
        {
            var points = new List<Pt>();
            int passes = 14;
            for (int i = 0; i <= passes; i++)
            {
                float y = -1f + 2f * i / passes;
                float half = (float)Math.Sqrt(Math.Max(0f, 1f - y * y));
                float side = i % 2 == 0 ? -1f : 1f;
                float jitterX = 0.15f * InkLines.Noise(seed, i);
                float jitterY = 0.08f * InkLines.Noise(seed + 7, i);
                points.Add(new Pt((side * half + jitterX) * width * 0.5f, (y + jitterY) * height * 0.5f));
            }
            return points;
        }

        private static List<Pt> Square(float cx, float cy, float half) => new List<Pt>
        {
            new Pt(cx - half, cy - half), new Pt(cx + half, cy - half), new Pt(cx + half, cy + half),
            new Pt(cx - half, cy + half), new Pt(cx - half, cy - half),
        };

        private static List<Pt> Circle(float cx, float cy, float r, int segments)
        {
            var points = new List<Pt>(segments + 1);
            for (int i = 0; i <= segments; i++)
            {
                float a = i / (float)segments * 2f * (float)Math.PI;
                points.Add(new Pt(cx + r * (float)Math.Cos(a), cy + r * (float)Math.Sin(a)));
            }
            return points;
        }
    }
}
