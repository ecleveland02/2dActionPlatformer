using UnityEngine;

namespace Margin.Rendering
{
    /// <summary>How the bat's wings are held.</summary>
    public enum BatWings { Flap, Raised, Folded, Splayed }

    /// <summary>
    /// The Scribble Bat's procedural look (spec 4: no sprite sheets): a scribbled ink loop for the body, two
    /// ears, a dot eye, and two scalloped wings that flap. Drawn with LineRenderers in local space, so flipping
    /// the transform's X scale turns it around. Posed once per gameplay tick by its owner (FlyingEnemy).
    /// </summary>
    public sealed class ScribbleBatVisual : MonoBehaviour
    {
        [SerializeField] private Material lineMaterial;
        [SerializeField, Min(0.01f)] private float bodyRadius = 0.2f;
        [SerializeField, Min(0.01f)] private float wingSpan = 0.55f;
        [SerializeField, Min(0.001f)] private float lineWidth = 0.05f;
        [Tooltip("Frames per wing beat while flapping.")]
        [SerializeField, Min(2)] private int flapFrames = 12;
        [SerializeField] private Color ink = new Color32(0x1A, 0x1A, 0x1A, 0xFF);
        [SerializeField] private int sortingOrder = 10;

        private LineRenderer body, ears, eye, wingFront, wingBack;
        private int tick;

        /// <summary>Where the wings are in their beat, 0..1 (0 = level on the way up).</summary>
        public float FlapPhase => (tick % flapFrames) / (float)flapFrames;

        /// <summary>Overrides the ink color (e.g. a red flash). Null = normal ink.</summary>
        public Color? Tint { get; set; }

        private void Awake() => Build();

        /// <summary>Creates the line renderers (safe to call again).</summary>
        public void Build()
        {
            if (body != null) return;
            body = Line("Body", loop: true);
            ears = Line("Ears", loop: false);
            eye = Line("Eye", loop: false);
            wingFront = Line("Wing Front", loop: false);
            wingBack = Line("Wing Back", loop: false);

            // Scribbled body: a wobbly circle drawn one and a half times round, like a quick pen loop.
            const int points = 24;
            body.loop = false;
            body.positionCount = points;
            for (int i = 0; i < points; i++)
            {
                float a = i / 16f * Mathf.PI * 2f;
                float r = bodyRadius * (1f + 0.12f * Mathf.Sin(i * 2.7f));
                body.SetPosition(i, new Vector3(Mathf.Cos(a) * r, Mathf.Sin(a) * r * 0.85f, 0f));
            }

            // Two pointed ears.
            float e = bodyRadius;
            ears.positionCount = 5;
            ears.SetPositions(new[]
            {
                new Vector3(-0.55f * e, 0.7f * e), new Vector3(-0.35f * e, 1.35f * e), new Vector3(0f, 0.8f * e),
                new Vector3(0.35f * e, 1.35f * e), new Vector3(0.55f * e, 0.7f * e),
            });

            // Eye: a short dash toward the facing side.
            eye.positionCount = 2;
            eye.SetPositions(new[] { new Vector3(0.35f * e, 0.15f * e), new Vector3(0.55f * e, 0.2f * e) });

            Pose(BatWings.Flap);
        }

        private LineRenderer Line(string name, bool loop)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var line = go.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.loop = loop;
            line.numCapVertices = 3;
            line.numCornerVertices = 2;
            line.widthMultiplier = lineWidth;
            line.sharedMaterial = lineMaterial != null ? lineMaterial : InkMaterial.Runtime;
            line.sortingOrder = sortingOrder;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            return line;
        }

        /// <summary>Advances one tick and draws the wings. Call once per gameplay tick.</summary>
        public void Pose(BatWings wings)
        {
            if (body == null) return;
            tick++;

            // Wing angle in degrees above horizontal.
            float angle = wings switch
            {
                BatWings.Raised => 65f,
                BatWings.Folded => -55f,
                BatWings.Splayed => -5f,   // lying on the ground, wings spread flat
                _ => 35f * Mathf.Sin(tick * 2f * Mathf.PI / flapFrames),
            };
            DrawWing(wingFront, 1f, angle);
            DrawWing(wingBack, -1f, angle);

            Color c = Tint ?? ink;
            foreach (LineRenderer line in new[] { body, ears, eye, wingFront, wingBack })
                line.startColor = line.endColor = c;
        }

        /// <summary>A scalloped wing from the body's side out to the tip and back along two bumps.</summary>
        private void DrawWing(LineRenderer line, float side, float angleDegrees)
        {
            float a = angleDegrees * Mathf.Deg2Rad;
            var root = new Vector2(side * bodyRadius * 0.8f, 0.05f);
            var along = new Vector2(side * Mathf.Cos(a), Mathf.Sin(a));
            var down = new Vector2(side * Mathf.Sin(a), -Mathf.Cos(a));   // perpendicular, toward the wing's underside

            Vector2 tip = root + along * wingSpan;
            line.positionCount = 5;
            line.SetPositions(new Vector3[]
            {
                root,
                tip,
                root + along * wingSpan * 0.66f + down * wingSpan * 0.18f,
                root + along * wingSpan * 0.4f + down * wingSpan * 0.05f,
                root + along * wingSpan * 0.15f + down * wingSpan * 0.22f,
            });
        }
    }
}
