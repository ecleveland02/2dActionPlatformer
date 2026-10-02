using UnityEngine;

namespace Margin.Rendering
{
    /// <summary>
    /// The Tack Turret's procedural look (spec 4: no sprite sheets): a pushpin stuck in the page. A flat base
    /// against the surface, a round red head, and the steel pin pointing where it aims; the pin pulls back into the
    /// head while winding up. Posed once per tick by TackTurret.
    /// </summary>
    public sealed class TackTurretVisual : MonoBehaviour
    {
        [SerializeField] private Material lineMaterial;
        [SerializeField] private Color ink = new Color32(0x1A, 0x1A, 0x1A, 0xFF);
        [SerializeField] private Color head = new Color32(0xC8, 0x3C, 0x32, 0xFF);
        [SerializeField] private int sortingOrder = 9;

        private LineRenderer baseLine, headFill, headOutline, pin;

        private void Awake() => Build();

        public void Build()
        {
            if (pin != null) return;
            baseLine = Line("Base", ink, 0.09f, sortingOrder);
            headFill = Line("Head", head, 0.48f, sortingOrder);
            headOutline = Line("Head Outline", ink, 0.06f, sortingOrder + 1);
            headOutline.loop = true;
            pin = Line("Pin", ink, 0.06f, sortingOrder - 1);
        }

        /// <summary>mount: the surface's outward normal. aim: unit direction. pull: 0..1 wind-up. dead: knocked flat.</summary>
        public void Pose(Vector2 mount, Vector2 aim, float pull, Color? tint, bool dead)
        {
            if (pin == null) return;
            Vector2 n = mount.sqrMagnitude > 0f ? mount.normalized : Vector2.up;
            Vector2 side = new Vector2(-n.y, n.x);
            Vector2 foot = -n * 0.4f;   // the surface is 0.4 below the center

            Set(baseLine, foot - side * 0.35f, foot + side * 0.35f);
            Set(headFill, -aim * 0.02f, aim * 0.02f);   // a fat round dot

            const int points = 16;
            headOutline.positionCount = points;
            for (int i = 0; i < points; i++)
            {
                float a = i / (float)points * Mathf.PI * 2f;
                headOutline.SetPosition(i, new Vector3(Mathf.Cos(a) * 0.26f, Mathf.Sin(a) * 0.26f, 0f));
            }

            Vector2 d = dead ? side : aim;
            float length = Mathf.Lerp(0.6f, 0.3f, pull);
            Set(pin, Vector2.zero, d * length);

            Color c = tint ?? ink;
            pin.startColor = pin.endColor = c;
            headOutline.startColor = headOutline.endColor = c;
            headFill.startColor = headFill.endColor = tint.HasValue ? Color.Lerp(head, tint.Value, 0.6f) : head;
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
            line.numCapVertices = 4;
            line.sortingOrder = order;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            return line;
        }

        private static void Set(LineRenderer line, Vector2 a, Vector2 b)
        {
            line.positionCount = 2;
            line.SetPosition(0, a);
            line.SetPosition(1, b);
        }
    }
}
