using System.Collections.Generic;
using Margin.Level;
using Margin.Rendering;
using UnityEngine;
using Pt = Margin.Rendering.WeaponShape.Pt;

namespace Margin.Bosses
{
    public enum HighlighterEyes { Open, Closed, Angry, Dizzy, Crossed }

    /// <summary>
    /// The Highlighter's procedural look (spec 4: no sprite sheets): a big highlighter marker on two stick legs,
    /// yellow barrel with an ink outline, a grip band, a cap with a clip (or the glowing chisel tip once the cap is
    /// thrown), and a face. The whole marker tilts around its base to swing, and lies flat to dash and sweep.
    /// Also draws the fight's cues in world space: the dash streak, the sweep's guide line, the drip lanes and
    /// drops, and the title card. Posed once per tick by HighlighterBoss. Origin = between the feet.
    /// </summary>
    public sealed class HighlighterVisual : MonoBehaviour
    {
        [SerializeField] private Material lineMaterial;
        [SerializeField] private Color ink = new Color32(0x1A, 0x1A, 0x1A, 0xFF);
        [SerializeField] private Color marker = new Color32(0xF4, 0xE2, 0x4A, 0xFF);
        [SerializeField] private Color capColor = new Color32(0xE6, 0xC5, 0x2C, 0xFF);
        [SerializeField] private Color glow = new Color(1f, 0.95f, 0.45f, 0.55f);
        [SerializeField, Min(0.005f)] private float lineWidth = 0.075f;
        [SerializeField] private int sortingOrder = 8;

        // Marker measurements, in the body's space (y = 0 is the bottom of the barrel).
        public const float HipHeight = 0.5f;
        private const float BarrelHalf = 0.55f, BarrelTop = 2.4f, CapTop = 3.05f;

        private Transform figure, body;
        private LineRenderer fill, outline, band, capFill, capOutline, clip, tipFill, tipOutline, eyeL, eyeR, mouth, legL, legR, sparkle;
        private LineRenderer trail;
        private TextMesh title;
        private readonly List<LineRenderer> dashPool = new List<LineRenderer>();
        private readonly List<LineRenderer> dropPool = new List<LineRenderer>();
        private int dashesUsed;
        private readonly List<LineRenderer> inkParts = new List<LineRenderer>();

        private void Awake() => Build();

        public void Build()
        {
            if (figure != null) return;
            figure = new GameObject("Figure").transform;
            figure.SetParent(transform, false);
            body = new GameObject("Body").transform;
            body.SetParent(figure, false);
            body.localPosition = new Vector3(0f, HipHeight, 0f);

            // Barrel: a thick yellow stroke is the fill, an ink rounded rectangle the outline.
            fill = Line(body, "Fill", marker, BarrelHalf * 2f - 0.08f, sortingOrder);
            Set(fill, new Vector3(0f, 0.06f), new Vector3(0f, BarrelTop - 0.06f));
            fill.numCapVertices = 0;
            outline = InkLine(body, "Outline");
            outline.loop = true;
            SetPoints(outline, RoundedRect(-BarrelHalf, 0f, BarrelHalf, BarrelTop, 0.16f));
            band = InkLine(body, "Grip");
            SetPoints(band, new[] { new Vector3(-BarrelHalf, 0.32f), new Vector3(BarrelHalf, 0.32f),
                                    new Vector3(BarrelHalf, 0.52f), new Vector3(-BarrelHalf, 0.52f) });

            capFill = Line(body, "Cap Fill", capColor, BarrelHalf * 2f + 0.02f, sortingOrder);
            Set(capFill, new Vector3(0f, BarrelTop + 0.04f), new Vector3(0f, CapTop - 0.08f));
            capFill.numCapVertices = 0;
            capOutline = InkLine(body, "Cap");
            capOutline.loop = true;
            SetPoints(capOutline, RoundedRect(-BarrelHalf - 0.06f, BarrelTop - 0.02f, BarrelHalf + 0.06f, CapTop, 0.2f));
            clip = InkLine(body, "Clip");
            SetPoints(clip, new[] { new Vector3(-0.5f, 2.85f), new Vector3(-0.72f, 2.8f), new Vector3(-0.72f, 1.9f), new Vector3(-0.58f, 1.8f) });

            // Chisel tip, shown once the cap is thrown.
            tipFill = Line(body, "Tip Fill", glow, 0.62f, sortingOrder);
            Set(tipFill, new Vector3(0f, BarrelTop + 0.02f), new Vector3(0f, BarrelTop + 0.34f));
            tipFill.numCapVertices = 0;
            tipOutline = InkLine(body, "Tip");
            SetPoints(tipOutline, new[] { new Vector3(-0.34f, BarrelTop), new Vector3(-0.34f, 2.72f), new Vector3(0.12f, 2.98f),
                                          new Vector3(0.34f, 2.86f), new Vector3(0.34f, BarrelTop) });

            eyeL = InkLine(body, "Eye L");
            eyeR = InkLine(body, "Eye R");
            mouth = InkLine(body, "Mouth");
            sparkle = InkLine(body, "Sparkle");
            sparkle.startColor = sparkle.endColor = new Color32(0xE0, 0xA8, 0x10, 0xFF);

            legL = InkLine(figure, "Leg L");
            legR = InkLine(figure, "Leg R");

            // World-space cues (not flipped with the figure).
            trail = Line(transform, "Streak", glow, 0.28f, sortingOrder - 2);
            trail.useWorldSpace = true;
            trail.enabled = false;

            var titleObject = new GameObject("Title");
            titleObject.transform.SetParent(transform, false);
            title = titleObject.AddComponent<TextMesh>();
            title.text = "";
            title.fontSize = 64;
            title.characterSize = 0.09f;
            title.anchor = TextAnchor.MiddleCenter;
            title.alignment = TextAlignment.Center;
            title.color = ink;
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font != null)
            {
                title.font = font;
                titleObject.GetComponent<MeshRenderer>().sharedMaterial = font.material;
            }
            titleObject.SetActive(false);
        }

        /// <summary>
        /// Poses the marker. tilt: degrees leaning toward facing (negative = back, 90 = lying flat forward).
        /// squash: 1 normal, below 1 crouched. lying: shift the pivot back so the flat marker is centered.
        /// </summary>
        public void Pose(int facing, float tilt, float squash, bool lying, bool capOn, float capShake, HighlighterEyes eyes,
                         bool sparkleOn, Color? tint, bool hurtFlash)
        {
            if (figure == null) return;
            figure.localScale = new Vector3(facing < 0 ? -1f : 1f, 1f, 1f);
            body.localRotation = Quaternion.Euler(0f, 0f, -tilt);
            float s = Mathf.Clamp(squash, 0.5f, 1.5f);
            body.localScale = new Vector3(1f + (1f - s) * 0.6f, s, 1f);
            body.localPosition = lying ? new Vector3(-1.25f, HipHeight + 0.15f, 0f) : new Vector3(0f, HipHeight * s, 0f);

            capFill.enabled = capOutline.enabled = clip.enabled = capOn;
            tipFill.enabled = tipOutline.enabled = !capOn;
            if (capOn && capShake != 0f)
            {
                capOutline.transform.localRotation = Quaternion.Euler(0f, 0f, capShake);
                capFill.transform.localRotation = capOutline.transform.localRotation;
            }
            else capOutline.transform.localRotation = capFill.transform.localRotation = Quaternion.identity;

            DrawFace(eyes);
            sparkle.enabled = sparkleOn;
            if (sparkleOn) DrawSparkle(new Vector2(0.25f, (capOn ? CapTop : 2.98f) + 0.25f));

            // Stick legs from the bottom of the barrel to the floor; bent when crouched, tucked when lying.
            legL.enabled = legR.enabled = !lying;
            if (!lying)
            {
                float hip = HipHeight * s;
                float knee = (1f - s) * 0.5f + 0.04f;
                SetPoints(legL, new[] { new Vector3(-0.28f, hip), new Vector3(-0.28f - knee, hip * 0.5f), new Vector3(-0.3f, 0f), new Vector3(-0.08f, 0f) });
                SetPoints(legR, new[] { new Vector3(0.28f, hip), new Vector3(0.28f + knee, hip * 0.5f), new Vector3(0.3f, 0f), new Vector3(0.52f, 0f) });
            }

            Color line = tint ?? ink;
            foreach (LineRenderer part in inkParts)
                if (part != sparkle) part.startColor = part.endColor = line;
            Color body1 = hurtFlash ? Color.white : tint.HasValue ? Color.Lerp(marker, tint.Value, 0.35f) : marker;
            fill.startColor = fill.endColor = body1;
            capFill.startColor = capFill.endColor = hurtFlash ? Color.white : capColor;
        }

        public void SetVisible(bool visible)
        {
            if (figure != null) figure.gameObject.SetActive(visible);
        }

        // ---------------- fight cues (world space) ----------------

        /// <summary>The highlighted streak a dash leaves on the floor.</summary>
        public void SetStreak(Vector2 from, Vector2 to, float alpha)
        {
            trail.enabled = alpha > 0.01f;
            if (!trail.enabled) return;
            Set(trail, from, to);
            Color c = glow;
            c.a *= alpha;
            trail.startColor = trail.endColor = c;
        }

        /// <summary>Starts a frame of dashed cue lines (sweep guide, drip lanes).</summary>
        public void BeginDashes() => dashesUsed = 0;

        /// <summary>A dashed line from a to b.</summary>
        public void Dashed(Vector2 a, Vector2 b, float alpha, float dash = 0.5f, float gap = 0.35f)
        {
            if (alpha <= 0.01f) return;
            Vector2 d = b - a;
            float length = d.magnitude;
            if (length < 0.01f) return;
            Vector2 u = d / length;
            Color c = glow;
            c.a = Mathf.Clamp01(alpha) * 0.9f;
            for (float t = 0f; t < length; t += dash + gap)
            {
                LineRenderer seg = NextDash();
                Set(seg, a + u * t, a + u * Mathf.Min(length, t + dash));
                seg.startColor = seg.endColor = c;
            }
        }

        /// <summary>Hides the dashed lines not used this frame.</summary>
        public void EndDashes()
        {
            for (int i = dashesUsed; i < dashPool.Count; i++) dashPool[i].enabled = false;
        }

        /// <summary>Falling ink drops (Drip Rain), as small teardrops.</summary>
        public void SetDrops(IReadOnlyList<Vector2> drops)
        {
            while (dropPool.Count < drops.Count)
            {
                LineRenderer d = Line(transform, "Drop", ink, 0.07f, sortingOrder + 1);
                d.useWorldSpace = true;
                d.loop = true;
                dropPool.Add(d);
            }
            for (int i = 0; i < dropPool.Count; i++)
            {
                bool on = i < drops.Count;
                dropPool[i].enabled = on;
                if (!on) continue;
                Vector2 p = drops[i];
                var pts = new Vector3[10];
                for (int k = 0; k < 9; k++)
                {
                    float a = Mathf.PI * (1.1f + 0.8f * k / 8f);
                    pts[k] = new Vector3(p.x + Mathf.Cos(a) * 0.2f, p.y - 0.1f + Mathf.Sin(a) * 0.2f, 0f);
                }
                pts[9] = new Vector3(p.x, p.y + 0.35f, 0f);
                dropPool[i].positionCount = pts.Length;
                dropPool[i].SetPositions(pts);
            }
        }

        public void ShowTitle(string text, Vector2 at, float alpha)
        {
            bool on = alpha > 0.01f;
            title.gameObject.SetActive(on);
            if (!on) return;
            title.text = text;
            title.transform.position = new Vector3(at.x, at.y, 0f);
            Color c = ink;
            c.a = Mathf.Clamp01(alpha);
            title.color = c;
        }

        // ---------------- drawing helpers ----------------

        private void DrawFace(HighlighterEyes eyes)
        {
            const float y = 1.78f, ex = 0.12f, spread = 0.2f;
            Vector3 l = new Vector3(ex - spread, y), r = new Vector3(ex + spread, y);
            switch (eyes)
            {
                case HighlighterEyes.Closed:
                    SetPoints(eyeL, new[] { l + new Vector3(-0.08f, 0f), l + new Vector3(0.08f, 0f) });
                    SetPoints(eyeR, new[] { r + new Vector3(-0.08f, 0f), r + new Vector3(0.08f, 0f) });
                    SetPoints(mouth, new[] { new Vector3(-0.05f, 1.42f), new Vector3(0.25f, 1.42f) });
                    break;
                case HighlighterEyes.Angry:
                    SetPoints(eyeL, new[] { l + new Vector3(-0.1f, 0.1f), l + new Vector3(0.08f, -0.02f), l + new Vector3(0.02f, -0.1f) });
                    SetPoints(eyeR, new[] { r + new Vector3(0.1f, 0.1f), r + new Vector3(-0.08f, -0.02f), r + new Vector3(-0.02f, -0.1f) });
                    SetPoints(mouth, new[] { new Vector3(-0.08f, 1.36f), new Vector3(0.1f, 1.44f), new Vector3(0.3f, 1.36f) });
                    break;
                case HighlighterEyes.Dizzy:
                    SetPoints(eyeL, Spiral(l, 0.11f));
                    SetPoints(eyeR, Spiral(r, 0.11f));
                    SetPoints(mouth, new[] { new Vector3(-0.08f, 1.4f), new Vector3(0.02f, 1.46f), new Vector3(0.12f, 1.38f), new Vector3(0.22f, 1.46f), new Vector3(0.3f, 1.4f) });
                    break;
                case HighlighterEyes.Crossed:
                    SetPoints(eyeL, new[] { l + new Vector3(-0.09f, -0.09f), l + new Vector3(0.09f, 0.09f), l, l + new Vector3(-0.09f, 0.09f), l + new Vector3(0.09f, -0.09f) });
                    SetPoints(eyeR, new[] { r + new Vector3(-0.09f, -0.09f), r + new Vector3(0.09f, 0.09f), r, r + new Vector3(-0.09f, 0.09f), r + new Vector3(0.09f, -0.09f) });
                    SetPoints(mouth, new[] { new Vector3(-0.05f, 1.36f), new Vector3(0.25f, 1.36f) });
                    break;
                default:
                    SetPoints(eyeL, new[] { l + new Vector3(0f, -0.08f), l + new Vector3(0f, 0.08f) });
                    SetPoints(eyeR, new[] { r + new Vector3(0f, -0.08f), r + new Vector3(0f, 0.08f) });
                    SetPoints(mouth, new[] { new Vector3(-0.02f, 1.42f), new Vector3(0.1f, 1.38f), new Vector3(0.24f, 1.42f) });
                    break;
            }
        }

        private void DrawSparkle(Vector2 c)
        {
            const float r = 0.28f, k = 0.07f;
            SetPoints(sparkle, new[]
            {
                new Vector3(c.x, c.y + r), new Vector3(c.x + k, c.y + k), new Vector3(c.x + r, c.y), new Vector3(c.x + k, c.y - k),
                new Vector3(c.x, c.y - r), new Vector3(c.x - k, c.y - k), new Vector3(c.x - r, c.y), new Vector3(c.x - k, c.y + k),
                new Vector3(c.x, c.y + r),
            });
        }

        private static Vector3[] Spiral(Vector3 center, float radius)
        {
            List<Pt> s = LevelArt.Doodle(DoodleKind.Spiral, radius * 2f)[0];
            var pts = new Vector3[s.Count];
            for (int i = 0; i < s.Count; i++) pts[i] = center + new Vector3(s[i].X, s[i].Y);
            return pts;
        }

        private static Vector3[] RoundedRect(float x0, float y0, float x1, float y1, float r)
        {
            var pts = new List<Vector3>();
            void Corner(float cx, float cy, float start)
            {
                for (int i = 0; i <= 4; i++)
                {
                    float a = (start + 90f * i / 4f) * Mathf.Deg2Rad;
                    pts.Add(new Vector3(cx + Mathf.Cos(a) * r, cy + Mathf.Sin(a) * r));
                }
            }
            Corner(x1 - r, y0 + r, 270f);
            Corner(x1 - r, y1 - r, 0f);
            Corner(x0 + r, y1 - r, 90f);
            Corner(x0 + r, y0 + r, 180f);
            return pts.ToArray();
        }

        private LineRenderer NextDash()
        {
            if (dashesUsed >= dashPool.Count)
            {
                LineRenderer d = Line(transform, "Dash", glow, 0.12f, sortingOrder - 2);
                d.useWorldSpace = true;
                dashPool.Add(d);
            }
            LineRenderer seg = dashPool[dashesUsed++];
            seg.enabled = true;
            return seg;
        }

        private LineRenderer InkLine(Transform parent, string lineName)
        {
            LineRenderer line = Line(parent, lineName, ink, lineWidth, sortingOrder + 1);
            inkParts.Add(line);
            return line;
        }

        private LineRenderer Line(Transform parent, string lineName, Color color, float width, int order)
        {
            var go = new GameObject(lineName);
            go.transform.SetParent(parent, false);
            var line = go.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.sharedMaterial = lineMaterial != null ? lineMaterial : InkMaterial.Runtime;
            line.widthMultiplier = width;
            line.startColor = line.endColor = color;
            line.numCapVertices = 3;
            line.numCornerVertices = 2;
            line.sortingOrder = order;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            return line;
        }

        private static void Set(LineRenderer line, Vector3 a, Vector3 b)
        {
            line.positionCount = 2;
            line.SetPosition(0, a);
            line.SetPosition(1, b);
        }

        private static void SetPoints(LineRenderer line, Vector3[] points)
        {
            line.positionCount = points.Length;
            line.SetPositions(points);
        }
    }
}
