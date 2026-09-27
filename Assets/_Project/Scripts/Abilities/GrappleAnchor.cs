using System.Collections.Generic;
using Margin.Rendering;
using UnityEngine;

namespace Margin.Abilities
{
    /// <summary>
    /// A ring the Grapple Line can hook onto (spec 8: swing from anchor points). Drawn as a hand-inked ring pinned
    /// to the page with a tack. When it's the ring the line would go to (in reach, in front of you), it's highlighted
    /// so you know before you throw. All enabled anchors register themselves.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GrappleAnchor : MonoBehaviour
    {
        [SerializeField] private Material lineMaterial;
        [SerializeField, Min(0.1f)] private float radius = 0.35f;
        [SerializeField] private Color ink = new Color32(0x1A, 0x1A, 0x1A, 0xFF);
        [SerializeField] private Color highlight = new Color32(0xE0, 0xA8, 0x10, 0xFF);

        private static readonly List<GrappleAnchor> all = new List<GrappleAnchor>();
        private LineRenderer ring, tack, glow;
        private bool highlighted;

        public static IReadOnlyList<GrappleAnchor> All => all;
        public Vector2 Point => transform.position;

        private void Awake() => Build();
        private void OnEnable() => all.Add(this);

        private void OnDisable()
        {
            all.Remove(this);
            SetHighlight(false);
        }

        /// <summary>Shows that the line would hook this ring right now.</summary>
        public void SetHighlight(bool on)
        {
            if (on == highlighted || ring == null) return;
            highlighted = on;
            Color c = on ? highlight : ink;
            ring.startColor = ring.endColor = c;
            glow.enabled = on;
        }

        private void Build()
        {
            if (ring != null) return;
            // A ring drawn in one and a bit turns, like a quick pen circle.
            ring = Line("Ring", ink, 0.07f, 6);
            const int points = 30;
            ring.positionCount = points;
            for (int i = 0; i < points; i++)
            {
                float a = i / 24f * Mathf.PI * 2f + 0.4f;
                float r = radius * (1f + 0.06f * Mathf.Sin(i * 1.7f));
                ring.SetPosition(i, new Vector3(Mathf.Cos(a) * r, Mathf.Sin(a) * r, 0f));
            }

            // The tack pinning it to the page: a short line up to a dot.
            tack = Line("Tack", ink, 0.06f, 6);
            tack.positionCount = 3;
            tack.SetPositions(new[]
            {
                new Vector3(0f, radius, 0f), new Vector3(0f, radius + 0.28f, 0f), new Vector3(0.1f, radius + 0.3f, 0f),
            });

            glow = Line("Glow", new Color(1f, 0.9f, 0.4f, 0.45f), 0.22f, 5);
            glow.loop = true;
            glow.positionCount = 20;
            for (int i = 0; i < 20; i++)
            {
                float a = i / 20f * Mathf.PI * 2f;
                glow.SetPosition(i, new Vector3(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius, 0f));
            }
            glow.enabled = false;
        }

        private LineRenderer Line(string lineName, Color color, float width, int order)
        {
            var go = new GameObject(lineName);
            go.transform.SetParent(transform, false);
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

        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.9f, 0.6f, 0.1f, 0.9f);
            Gizmos.DrawWireSphere(transform.position, radius);
        }
    }
}
