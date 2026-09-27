using System.Collections.Generic;
using Margin.Rendering;
using UnityEngine;

namespace Margin.Level
{
    /// <summary>
    /// The ink pot checkpoint's procedural drawing (spec 4: no sprite sheets): a jar outline, a scribbled ink fill,
    /// and a quill pen standing in it once it's your checkpoint. Built from LineRenderers when play starts; the
    /// Checkpoint draws a gizmo of the jar in the editor. Origin = the middle of the jar's base (on the floor).
    /// </summary>
    public sealed class InkPotVisual : MonoBehaviour
    {
        [SerializeField] private Material lineMaterial;
        [SerializeField, Min(0.001f)] private float lineWidth = 0.06f;
        [SerializeField] private Color ink = new Color32(0x1A, 0x1A, 0x1A, 0xFF);
        [SerializeField] private Color pencil = new Color32(0x9A, 0x96, 0x8C, 0xFF);
        [SerializeField] private int sortingOrder = 5;

        private LineRenderer jar, fill, quill, feather;
        private TextMesh message;
        private bool lit;

        /// <summary>The jar's outline, right half from the base up (the left half mirrors it).</summary>
        public static readonly Vector2[] JarHalf =
        {
            new Vector2(0.00f, 0.00f), new Vector2(0.36f, 0.00f), new Vector2(0.41f, 0.05f), new Vector2(0.43f, 0.30f),
            new Vector2(0.37f, 0.52f), new Vector2(0.21f, 0.62f), new Vector2(0.19f, 0.70f), new Vector2(0.24f, 0.72f),
            new Vector2(0.24f, 0.79f), new Vector2(0.00f, 0.79f),
        };

        private void Awake() => Build();

        public void Build()
        {
            if (jar != null) return;
            jar = Line("Jar", ink);
            var outline = new List<Vector3>();
            for (int i = 1; i < JarHalf.Length - 1; i++) outline.Add(JarHalf[i]);
            for (int i = JarHalf.Length - 2; i >= 1; i--) outline.Add(new Vector3(-JarHalf[i].x, JarHalf[i].y));
            jar.loop = true;
            jar.positionCount = outline.Count;
            jar.SetPositions(outline.ToArray());

            fill = Line("Ink", ink);
            fill.widthMultiplier = lineWidth * 1.3f;
            quill = Line("Quill", ink);
            feather = Line("Feather", ink);
            feather.loop = true;

            // Quill: a shaft leaning out of the neck, with a narrow leaf-shaped feather along its top half.
            Vector2 nib = new Vector2(0.04f, 0.5f), top = new Vector2(0.5f, 1.55f);
            quill.positionCount = 2;
            quill.SetPositions(new Vector3[] { nib, top });
            Vector2 along = (top - nib).normalized, side = new Vector2(-along.y, along.x);
            var vane = new List<Vector3>();
            for (int i = 0; i <= 8; i++)
            {
                float t = i / 8f;
                float w = 0.13f * Mathf.Sin(t * Mathf.PI);
                vane.Add(Vector2.Lerp(nib + along * 0.45f, top + along * 0.08f, t) + side * w);
            }
            for (int i = 8; i >= 0; i--)
            {
                float t = i / 8f;
                float w = 0.08f * Mathf.Sin(t * Mathf.PI);
                vane.Add(Vector2.Lerp(nib + along * 0.45f, top + along * 0.08f, t) - side * w);
            }
            feather.positionCount = vane.Count;
            feather.SetPositions(vane.ToArray());

            Text("Label", "INK POT", new Vector3(0f, 1.95f, 0f), pencil, 0.045f);
            message = Text("Message", "health restored", new Vector3(0f, 2.35f, 0f), ink, 0.05f);
            message.gameObject.SetActive(false);
            SetLit(lit);
        }

        /// <summary>Lit = this is the current checkpoint: full of ink, quill in it.</summary>
        public void SetLit(bool on)
        {
            lit = on;
            if (jar == null) return;
            Color c = on ? ink : pencil;
            fill.startColor = fill.endColor = c;
            DrawFill(on ? 0.5f : 0.14f);
            quill.gameObject.SetActive(on);
            feather.gameObject.SetActive(on);
        }

        public void ShowMessage(bool on)
        {
            if (message != null) message.gameObject.SetActive(on);
        }

        /// <summary>A scribbled fill: a zigzag back and forth across the jar up to <paramref name="level"/>.</summary>
        private void DrawFill(float level)
        {
            var points = new List<Vector3>();
            const float step = 0.06f;
            bool right = true;
            for (float y = 0.06f; y <= level + 0.001f; y += step)
            {
                float half = HalfWidthAt(y) - 0.07f;
                points.Add(new Vector3(right ? half : -half, y));
                right = !right;
            }
            if (points.Count < 2) points.Add(new Vector3(0f, level));
            fill.positionCount = points.Count;
            fill.SetPositions(points.ToArray());
        }

        /// <summary>The jar's half width at a height (linear between the outline points).</summary>
        public static float HalfWidthAt(float y)
        {
            for (int i = 1; i < JarHalf.Length - 1; i++)
            {
                Vector2 a = JarHalf[i], b = JarHalf[i + 1];
                if (y < a.y || y > b.y || b.y <= a.y) continue;
                return Mathf.Lerp(a.x, b.x, (y - a.y) / (b.y - a.y));
            }
            return JarHalf[1].x;
        }

        private LineRenderer Line(string lineName, Color color)
        {
            var go = new GameObject(lineName);
            go.transform.SetParent(transform, false);
            var line = go.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.numCapVertices = 3;
            line.numCornerVertices = 2;
            line.widthMultiplier = lineWidth;
            line.sharedMaterial = lineMaterial != null ? lineMaterial : InkMaterial.Runtime;
            line.sortingOrder = sortingOrder;
            line.startColor = line.endColor = color;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            return line;
        }

        private TextMesh Text(string objectName, string text, Vector3 position, Color color, float characterSize)
        {
            var go = new GameObject(objectName);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = position;
            var mesh = go.AddComponent<TextMesh>();
            mesh.text = text;
            mesh.fontSize = 48;
            mesh.characterSize = characterSize;
            mesh.anchor = TextAnchor.MiddleCenter;
            mesh.alignment = TextAlignment.Center;
            mesh.color = color;
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font != null)
            {
                mesh.font = font;
                go.GetComponent<MeshRenderer>().sharedMaterial = font.material;
            }
            return mesh;
        }
    }
}
