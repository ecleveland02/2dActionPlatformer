using UnityEngine;

namespace Margin.Rendering
{
    /// <summary>
    /// The Eraser Crawler's procedural look (spec 4: no sprite sheets): a pink rubber eraser with a paper sleeve,
    /// two dot eyes and four stubby legs that scurry with its speed. Origin = between its feet; faces right
    /// (flip the X scale to face left). Posed once per tick by EraserCrawler.
    /// </summary>
    public sealed class EraserVisual : MonoBehaviour
    {
        [SerializeField] private Material lineMaterial;
        [SerializeField] private Color ink = new Color32(0x1A, 0x1A, 0x1A, 0xFF);
        [SerializeField] private Color rubber = new Color32(0xE8, 0x9C, 0xA8, 0xFF);
        [SerializeField] private Color sleeve = new Color32(0x6E, 0x8F, 0xC9, 0xFF);
        [SerializeField] private int sortingOrder = 9;

        private const float Width = 0.9f, Height = 0.5f, LegHeight = 0.12f;
        private Transform body;
        private LineRenderer fill, sleeveFill, outline, eyes;
        private readonly LineRenderer[] legs = new LineRenderer[4];
        private float walkPhase;

        private void Awake() => Build();

        public void Build()
        {
            if (body != null) return;
            body = new GameObject("Body").transform;
            body.SetParent(transform, false);
            fill = Line(body, "Rubber", rubber, Height - 0.06f, sortingOrder);
            fill.numCapVertices = 0;
            Set(fill, new Vector3(-Width * 0.5f + 0.04f, Height * 0.5f), new Vector3(Width * 0.5f - 0.04f, Height * 0.5f));
            sleeveFill = Line(body, "Sleeve", sleeve, Height - 0.04f, sortingOrder + 1);
            sleeveFill.numCapVertices = 0;
            Set(sleeveFill, new Vector3(-Width * 0.5f + 0.04f, Height * 0.5f), new Vector3(-0.05f, Height * 0.5f));
            outline = Line(body, "Outline", ink, 0.06f, sortingOrder + 2);
            outline.loop = true;
            outline.positionCount = 4;
            outline.SetPositions(new[]
            {
                new Vector3(-Width * 0.5f, 0f), new Vector3(Width * 0.5f, 0f),
                new Vector3(Width * 0.5f, Height), new Vector3(-Width * 0.5f, Height),
            });
            eyes = Line(body, "Eyes", ink, 0.07f, sortingOrder + 3);
            for (int i = 0; i < legs.Length; i++) legs[i] = Line(transform, "Leg " + i, ink, 0.05f, sortingOrder + 2);
        }

        /// <summary>speed: units/s (leg scurry). lunge: 0..1 nibble. stunned: squashed eyes.</summary>
        public void Pose(float speed, float lunge, Color? tint, bool dead, bool stunned)
        {
            if (body == null) return;
            walkPhase += speed * 0.9f;
            float bob = speed > 0.1f ? 0.03f * Mathf.Abs(Mathf.Sin(walkPhase)) : 0f;
            body.localPosition = new Vector3(lunge * 0.15f, LegHeight + bob, 0f);
            body.localRotation = Quaternion.Euler(0f, 0f, dead ? 180f : -lunge * 12f);

            float[] xs = { -0.32f, -0.12f, 0.12f, 0.32f };
            for (int i = 0; i < legs.Length; i++)
            {
                float swing = speed > 0.1f ? 0.07f * Mathf.Sin(walkPhase + i * Mathf.PI * 0.5f) : 0f;
                Set(legs[i], new Vector3(xs[i], LegHeight + bob), new Vector3(xs[i] + swing, 0f));
                legs[i].enabled = !dead;
            }

            if (stunned || dead) Set(eyes, new Vector3(0.18f, Height * 0.62f), new Vector3(0.34f, Height * 0.62f));
            else
            {
                eyes.positionCount = 4;
                eyes.SetPositions(new[]
                {
                    new Vector3(0.2f, Height * 0.55f), new Vector3(0.2f, Height * 0.72f),
                    new Vector3(0.33f, Height * 0.72f), new Vector3(0.33f, Height * 0.55f),
                });
            }

            Color c = tint ?? ink;
            outline.startColor = outline.endColor = c;
            eyes.startColor = eyes.endColor = c;
            foreach (LineRenderer leg in legs) leg.startColor = leg.endColor = c;
            fill.startColor = fill.endColor = tint.HasValue ? Color.Lerp(rubber, tint.Value, 0.5f) : rubber;
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
    }
}
