using Margin.Rendering;
using UnityEngine;

namespace Margin.FX
{
    /// <summary>
    /// Smear frame (spec 4.3): a filled ink crescent from the blade's previous angle to its current angle,
    /// drawn on the fastest frames of a swing and fading over a few frames. Built as a mesh in world space,
    /// pivoting on the shoulder so it follows the arc the blade tip actually traced.
    /// </summary>
    public sealed class SmearRenderer : MonoBehaviour
    {
        private Mesh mesh;
        private MeshRenderer meshRenderer;
        private Color[] baseColors;
        private Color[] colors;
        private int life, lifeTotal;

        public bool Visible => life > 0;

        public static SmearRenderer Create(int sortingOrder)
        {
            var go = new GameObject("Smear") { hideFlags = HideFlags.DontSave };
            var smear = go.AddComponent<SmearRenderer>();
            smear.mesh = new Mesh { name = "Smear" };
            smear.mesh.MarkDynamic();
            go.AddComponent<MeshFilter>().sharedMesh = smear.mesh;
            smear.meshRenderer = go.AddComponent<MeshRenderer>();
            smear.meshRenderer.sharedMaterial = InkMaterial.Runtime;
            smear.meshRenderer.sortingOrder = sortingOrder;
            smear.meshRenderer.enabled = false;
            return smear;
        }

        /// <summary>
        /// Draws a crescent around <paramref name="pivot"/> from angle <paramref name="fromDeg"/> (oldest, thin)
        /// to <paramref name="toDeg"/> (newest, thick), shortest way round.
        /// </summary>
        public void Show(Vector3 pivot, float fromDeg, float toDeg, float outerRadius, float thickness, Color color, int frames)
        {
            float sweep = FeelMath.DeltaAngle(fromDeg, toDeg);
            int segments = FeelMath.SmearSegments(sweep);

            var vertices = new Vector3[(segments + 1) * 2];
            baseColors = new Color[vertices.Length];
            var triangles = new int[segments * 6];

            for (int i = 0; i <= segments; i++)
            {
                float t = i / (float)segments;
                float a = (fromDeg + sweep * t) * Mathf.Deg2Rad;
                var dir = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f);
                float inner = FeelMath.SmearInnerRadius(outerRadius, thickness, t);
                vertices[i * 2] = pivot + dir * outerRadius;
                vertices[i * 2 + 1] = pivot + dir * inner;

                // Older part of the arc is fainter.
                Color c = color;
                c.a *= Mathf.Lerp(0.15f, 1f, t);
                baseColors[i * 2] = baseColors[i * 2 + 1] = c;

                if (i < segments)
                {
                    int v = i * 2, k = i * 6;
                    triangles[k] = v; triangles[k + 1] = v + 2; triangles[k + 2] = v + 1;
                    triangles[k + 3] = v + 1; triangles[k + 4] = v + 2; triangles[k + 5] = v + 3;
                }
            }

            mesh.Clear();
            mesh.vertices = vertices;
            mesh.triangles = triangles;
            colors = (Color[])baseColors.Clone();
            mesh.colors = colors;
            mesh.RecalculateBounds();

            life = lifeTotal = Mathf.Max(1, frames);
            meshRenderer.enabled = true;
        }

        /// <summary>Call once per gameplay tick: fades the smear and hides it when done.</summary>
        public void Tick()
        {
            if (life <= 0) return;
            life--;
            if (life == 0)
            {
                meshRenderer.enabled = false;
                return;
            }

            float fade = life / (float)lifeTotal;
            for (int i = 0; i < colors.Length; i++)
            {
                colors[i] = baseColors[i];
                colors[i].a *= fade;
            }
            mesh.colors = colors;
        }

        public void Hide()
        {
            life = 0;
            if (meshRenderer != null) meshRenderer.enabled = false;
        }

        private void OnDestroy()
        {
            if (mesh != null) Destroy(mesh);
        }
    }
}
