using System.Collections.Generic;
using Margin.Level;
using Margin.Rendering;
using UnityEngine;
using Pt = Margin.Rendering.WeaponShape.Pt;

namespace Margin.Bosses
{
    /// <summary>
    /// The Stapler Titan's procedural look (spec 4: no sprite sheets): a giant desk stapler on two stick legs. A dark
    /// base, and a steel-blue top arm hinged at the back that opens like a jaw; the face is on the arm's nose.
    /// Red is kept for the unparryable telegraph flash (spec 6.5). Also draws the fight's world-space cues: Staple
    /// Rain's dashed lanes and the title card. Posed once per tick by StaplerTitanBoss. Origin = between the feet.
    /// </summary>
    public sealed class StaplerVisual : MonoBehaviour
    {
        [SerializeField] private Material lineMaterial;
        [SerializeField] private Color ink = new Color32(0x1A, 0x1A, 0x1A, 0xFF);
        [SerializeField] private Color baseColor = new Color32(0x3D, 0x3F, 0x45, 0xFF);
        [SerializeField] private Color armColor = new Color32(0x56, 0x70, 0x8F, 0xFF);
        [SerializeField] private Color steel = new Color32(0xA8, 0xAD, 0xB5, 0xFF);
        [SerializeField] private Color cue = new Color(0.35f, 0.42f, 0.55f, 0.8f);
        [SerializeField, Min(0.005f)] private float lineWidth = 0.075f;
        [SerializeField] private int sortingOrder = 8;

        // Measurements in the figure's space (facing right, y = 0 at the feet).
        public const float HipHeight = 0.5f, BaseTop = 0.95f, HalfLength = 1.7f;
        private const float ArmLength = 3.35f, ArmThickness = 0.6f;
        private static readonly Vector3 Hinge = new Vector3(-1.45f, 1.02f, 0f);

        private Transform figure, arm;
        private LineRenderer baseFill, baseOutline, armFill, armOutline, strip, eyeL, eyeR, mouth, legL, legR, sparkle;
        private TextMesh title;
        private readonly List<LineRenderer> dashPool = new List<LineRenderer>();
        private readonly List<LineRenderer> inkParts = new List<LineRenderer>();
        private int dashesUsed;

        private void Awake() => Build();

        public void Build()
        {
            if (figure != null) return;
            figure = new GameObject("Figure").transform;
            figure.SetParent(transform, false);

            baseFill = Line(figure, "Base Fill", baseColor, BaseTop - HipHeight - 0.06f, sortingOrder);
            Set(baseFill, new Vector3(-HalfLength + 0.12f, (HipHeight + BaseTop) * 0.5f), new Vector3(HalfLength - 0.12f, (HipHeight + BaseTop) * 0.5f));
            baseFill.numCapVertices = 0;
            baseOutline = InkLine(figure, "Base");
            baseOutline.loop = true;
            SetPoints(baseOutline, RoundedRect(-HalfLength, HipHeight, HalfLength, BaseTop, 0.14f));
            strip = Line(figure, "Anvil", steel, 0.1f, sortingOrder + 1);
            Set(strip, new Vector3(HalfLength - 0.75f, BaseTop + 0.03f), new Vector3(HalfLength - 0.2f, BaseTop + 0.03f));

            arm = new GameObject("Arm").transform;
            arm.SetParent(figure, false);
            arm.localPosition = Hinge;
            armFill = Line(arm, "Arm Fill", armColor, ArmThickness - 0.08f, sortingOrder);
            Set(armFill, new Vector3(0.05f, ArmThickness * 0.5f), new Vector3(ArmLength - 0.12f, ArmThickness * 0.5f));
            armFill.numCapVertices = 0;
            armOutline = InkLine(arm, "Arm");
            armOutline.loop = true;
            SetPoints(armOutline, RoundedRect(-0.2f, 0f, ArmLength, ArmThickness, 0.22f));
            eyeL = InkLine(arm, "Eye L");
            eyeR = InkLine(arm, "Eye R");
            mouth = InkLine(arm, "Mouth");
            sparkle = InkLine(arm, "Sparkle");
            sparkle.startColor = sparkle.endColor = steel;

            legL = InkLine(figure, "Leg L");
            legR = InkLine(figure, "Leg R");

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
        /// Poses the stapler. jaw: degrees the arm is open (0 shut). squash: 1 normal, below 1 crouched, above 1
        /// stretched (leaping). stride: leg swing for walking (-1..1). tint: telegraph colour, or null.
        /// </summary>
        public void Pose(int facing, float jaw, float squash, float stride, HighlighterEyes eyes, bool sparkleOn, Color? tint, bool hurtFlash)
        {
            if (figure == null) return;
            float s = Mathf.Clamp(squash, 0.5f, 1.5f);
            figure.localScale = new Vector3((facing < 0 ? -1f : 1f) * (1f + (1f - s) * 0.4f), s, 1f);
            arm.localRotation = Quaternion.Euler(0f, 0f, Mathf.Clamp(jaw, -5f, 120f));

            // Stick legs under the base: knees out when crouched, swinging when walking.
            float knee = Mathf.Max(0f, 1f - s) * 0.7f + 0.05f;
            float swing = stride * 0.25f;
            SetPoints(legL, new[] { new Vector3(-0.9f, HipHeight), new Vector3(-0.9f - knee + swing * 0.5f, HipHeight * 0.5f),
                                    new Vector3(-0.9f + swing, 0f), new Vector3(-0.66f + swing, 0f) });
            SetPoints(legR, new[] { new Vector3(0.9f, HipHeight), new Vector3(0.9f + knee - swing * 0.5f, HipHeight * 0.5f),
                                    new Vector3(0.9f - swing, 0f), new Vector3(1.14f - swing, 0f) });

            DrawFace(eyes);
            sparkle.enabled = sparkleOn;
            if (sparkleOn) DrawSparkle(new Vector2(ArmLength + 0.2f, ArmThickness + 0.2f));

            Color line = tint ?? ink;
            foreach (LineRenderer part in inkParts)
                if (part != sparkle) part.startColor = part.endColor = line;
            Color armNow = hurtFlash ? Color.white : tint.HasValue ? Color.Lerp(armColor, tint.Value, 0.55f) : armColor;
            armFill.startColor = armFill.endColor = armNow;
            Color baseNow = hurtFlash ? Color.white : tint.HasValue ? Color.Lerp(baseColor, tint.Value, 0.4f) : baseColor;
            baseFill.startColor = baseFill.endColor = baseNow;
        }

        public void SetVisible(bool visible)
        {
            if (figure != null) figure.gameObject.SetActive(visible);
        }

        /// <summary>World position of the nose (where staples come out).</summary>
        public Vector2 Nose => arm != null ? (Vector2)arm.TransformPoint(new Vector3(ArmLength, ArmThickness * 0.35f, 0f)) : (Vector2)transform.position;

        // ---------------- fight cues (world space) ----------------

        /// <summary>Starts a frame of dashed cue lines (Staple Rain's lanes).</summary>
        public void BeginDashes() => dashesUsed = 0;

        public void Dashed(Vector2 a, Vector2 b, float alpha, float dash = 0.45f, float gap = 0.45f)
        {
            if (alpha <= 0.01f) return;
            Vector2 d = b - a;
            float length = d.magnitude;
            if (length < 0.01f) return;
            Vector2 u = d / length;
            Color c = cue;
            c.a *= Mathf.Clamp01(alpha);
            for (float t = 0f; t < length; t += dash + gap)
            {
                LineRenderer seg = NextDash();
                Set(seg, a + u * t, a + u * Mathf.Min(length, t + dash));
                seg.startColor = seg.endColor = c;
            }
        }

        public void EndDashes()
        {
            for (int i = dashesUsed; i < dashPool.Count; i++) dashPool[i].enabled = false;
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
            const float y = 0.36f;
            Vector3 l = new Vector3(ArmLength - 0.95f, y), r = new Vector3(ArmLength - 0.5f, y);
            Vector3 m = new Vector3(ArmLength - 0.3f, 0.1f);
            switch (eyes)
            {
                case HighlighterEyes.Closed:
                    SetPoints(eyeL, new[] { l + new Vector3(-0.09f, 0f), l + new Vector3(0.09f, 0f) });
                    SetPoints(eyeR, new[] { r + new Vector3(-0.09f, 0f), r + new Vector3(0.09f, 0f) });
                    SetPoints(mouth, new[] { m + new Vector3(-0.25f, 0f), m });
                    break;
                case HighlighterEyes.Angry:
                    SetPoints(eyeL, new[] { l + new Vector3(-0.1f, 0.1f), l + new Vector3(0.09f, 0f), l + new Vector3(0.02f, -0.08f) });
                    SetPoints(eyeR, new[] { r + new Vector3(-0.1f, 0.12f), r + new Vector3(0.09f, 0.02f), r + new Vector3(0.02f, -0.08f) });
                    SetPoints(mouth, new[] { m + new Vector3(-0.3f, 0.02f), m + new Vector3(-0.15f, -0.03f), m + new Vector3(0f, 0.02f) });
                    break;
                case HighlighterEyes.Dizzy:
                    SetPoints(eyeL, Spiral(l, 0.11f));
                    SetPoints(eyeR, Spiral(r, 0.11f));
                    SetPoints(mouth, new[] { m + new Vector3(-0.3f, 0f), m + new Vector3(-0.2f, 0.05f), m + new Vector3(-0.1f, -0.03f), m + new Vector3(0f, 0.04f) });
                    break;
                case HighlighterEyes.Crossed:
                    SetPoints(eyeL, Cross(l));
                    SetPoints(eyeR, Cross(r));
                    SetPoints(mouth, new[] { m + new Vector3(-0.25f, 0f), m });
                    break;
                default:
                    SetPoints(eyeL, new[] { l + new Vector3(0f, -0.08f), l + new Vector3(0f, 0.08f) });
                    SetPoints(eyeR, new[] { r + new Vector3(0f, -0.08f), r + new Vector3(0f, 0.08f) });
                    SetPoints(mouth, new[] { m + new Vector3(-0.25f, 0.02f), m + new Vector3(-0.12f, -0.02f), m + new Vector3(0f, 0.02f) });
                    break;
            }
        }

        private static Vector3[] Cross(Vector3 c) => new[]
        {
            c + new Vector3(-0.09f, -0.09f), c + new Vector3(0.09f, 0.09f), c, c + new Vector3(-0.09f, 0.09f), c + new Vector3(0.09f, -0.09f),
        };

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
                LineRenderer d = Line(transform, "Dash", cue, 0.12f, sortingOrder - 2);
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
