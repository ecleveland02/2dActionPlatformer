using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Margin.UI
{
    /// <summary>
    /// Base for elements drawn in ink. Each has its own seed so no two boxes wobble alike, mixed with the boil seed
    /// so the lines are gently redrawn a few times a second. Call Refresh() once per frame; it only repaints when
    /// something changed.
    /// </summary>
    public abstract class InkElement : VisualElement
    {
        private static int nextSeed = 1;
        private int drawnBoil = int.MinValue;

        protected readonly UISettings Settings;
        protected readonly int BaseSeed;

        protected InkElement(UISettings settings)
        {
            Settings = settings;
            BaseSeed = nextSeed++ * 7919;
            pickingMode = PickingMode.Ignore;
            generateVisualContent += ctx => Draw(ctx.painter2D, contentRect, BaseSeed + drawnBoil * 131);
        }

        /// <summary>Repaints if the boil moved on (values changed? call MarkDirtyRepaint yourself).</summary>
        public void Refresh()
        {
            int boil = InkPainter.BoilSeed(Settings);
            if (boil == drawnBoil) return;
            drawnBoil = boil;
            MarkDirtyRepaint();
        }

        protected abstract void Draw(Painter2D painter, Rect rect, int seed);
    }

    /// <summary>A sheet of paper with a sketched ink border (HUD cards, the pause menu).</summary>
    public sealed class InkPanel : InkElement
    {
        public InkPanel(UISettings settings) : base(settings) { }

        protected override void Draw(Painter2D painter, Rect rect, int seed)
        {
            float inset = Settings.overshoot + Settings.wobble;
            Rect inner = new Rect(rect.x + inset, rect.y + inset, rect.width - inset * 2f, rect.height - inset * 2f);
            InkPainter.FillBox(painter, inner, Settings.paper, Settings, seed);
            InkPainter.SketchBox(painter, inner, Settings.ink, Settings, seed + 1);
        }
    }

    /// <summary>
    /// A meter: faint empty track, a red damage chip (Trail), the ink fill (Value), notch marks (e.g. the 50 ink
    /// needed for a special), and a sketched outline that can pulse red.
    /// </summary>
    public sealed class InkBar : InkElement
    {
        private float value = 1f, trail = 1f, alert;
        private float[] notches = Array.Empty<float>();

        public InkBar(UISettings settings) : base(settings) { }

        /// <summary>Sets the fill (0..1), the chip trail (0..1, at or above the fill) and how red the outline is (0..1).</summary>
        public void SetValues(float fill, float chip, float outlineAlert)
        {
            if (Mathf.Approximately(fill, value) && Mathf.Approximately(chip, trail) && Mathf.Approximately(outlineAlert, alert)) return;
            value = fill;
            trail = Mathf.Max(fill, chip);
            alert = outlineAlert;
            MarkDirtyRepaint();
        }

        /// <summary>Tick marks across the bar at these fractions.</summary>
        public void SetNotches(params float[] marks)
        {
            notches = marks ?? Array.Empty<float>();
            MarkDirtyRepaint();
        }

        protected override void Draw(Painter2D painter, Rect rect, int seed)
        {
            UISettings s = Settings;
            float inset = s.overshoot * 0.5f + s.wobble;
            Rect track = new Rect(rect.x + inset, rect.y + inset * 0.5f, rect.width - inset * 2f, rect.height - inset);

            InkPainter.FillBox(painter, track, s.faint, s, seed, 0.5f);
            InkPainter.FillBox(painter, new Rect(track.x, track.y, track.width * trail, track.height), s.accent, s, seed + 1, 0.5f);
            InkPainter.FillBox(painter, new Rect(track.x, track.y, track.width * value, track.height), s.ink, s, seed + 2, 0.5f);

            for (int i = 0; i < notches.Length; i++)
            {
                float x = track.x + track.width * notches[i];
                InkPainter.Line(painter, new Vector2(x, track.y - 4f), new Vector2(x, track.yMax + 4f), s.paper, s, seed + 10 + i, 0.8f);
            }

            Color outline = Color.Lerp(s.ink, s.accent, alert);
            InkPainter.SketchBox(painter, track, outline, s, seed + 3, alert > 0f ? 1f + 0.4f * alert : 1f);
        }
    }

    /// <summary>
    /// A whole notebook page (title screen): paper, pale blue ruled lines and a red margin, drawn in the same
    /// hand-inked style as the rest of the UI and gently boiling.
    /// </summary>
    public sealed class RuledPaper : InkElement
    {
        private static readonly Color Rule = new Color32(0xA9, 0xBB, 0xCC, 0xB0);
        private static readonly Color Margin = new Color32(0xD9, 0x8C, 0x86, 0xC0);

        public RuledPaper(UISettings settings) : base(settings)
        {
            Color paper = settings.paper;
            paper.a = 1f;
            style.backgroundColor = paper;
        }

        protected override void Draw(Painter2D painter, Rect rect, int seed)
        {
            const float spacing = 54f;
            int i = 0;
            for (float y = rect.y + spacing * 1.5f; y < rect.yMax; y += spacing, i++)
                InkPainter.Line(painter, new Vector2(rect.x - 10f, y), new Vector2(rect.xMax + 10f, y), Rule, Settings, seed + i, 0.55f);
            float x = rect.x + rect.width * 0.12f;
            InkPainter.Line(painter, new Vector2(x, rect.y - 10f), new Vector2(x, rect.yMax + 10f), Margin, Settings, seed + 500, 0.7f);
        }
    }

    /// <summary>A full-screen wash of paper color that dims the game behind a menu.</summary>
    public sealed class InkDim : VisualElement
    {
        public InkDim(UISettings settings)
        {
            style.position = Position.Absolute;
            style.left = style.top = style.right = style.bottom = 0f;
            style.backgroundColor = settings.pauseDim;
        }
    }
}
