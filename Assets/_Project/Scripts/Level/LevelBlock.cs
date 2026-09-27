using System.Collections.Generic;
using Margin.Rendering;
using Margin.UI;
using UnityEngine;
using Pt = Margin.Rendering.WeaponShape.Pt;

namespace Margin.Level
{
    /// <summary>
    /// A piece of level geometry that draws itself from its BoxCollider2D (spec 11.1 line-art look): solid blocks get
    /// a hand-inked outline filled with pencil hatching; one-way platforms (World 1's notebook lines) are a single
    /// ink line along the top. Runs in the editor too, so resizing the collider in the Scene view redraws it.
    /// Put solid blocks on the Ground layer and one-way ones on OneWayPlatform (the level builder does).
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(BoxCollider2D))]
    public sealed class LevelBlock : MonoBehaviour
    {
        public enum Style { Solid, OneWay }

        [SerializeField] private Style style = Style.Solid;
        [SerializeField] private Material lineMaterial;
        [SerializeField, Min(0.005f)] private float lineWidth = 0.07f;
        [Tooltip("Gap between the pencil hatch lines that fill solid blocks. 0 = no hatching.")]
        [SerializeField, Min(0f)] private float hatchSpacing = 0.45f;
        [Tooltip("How far the ink outline wanders from straight, in units.")]
        [SerializeField, Min(0f)] private float wobble = 0.025f;
        [SerializeField] private Color ink = new Color32(0x1A, 0x1A, 0x1A, 0xFF);
        [SerializeField] private Color hatch = new Color32(0x9A, 0x96, 0x8C, 0xB0);

        private const string HatchName = "Hatch";
        private Vector2 drawnSize = -Vector2.one, drawnOffset;
        private Style drawnStyle;
        private bool dirty = true;

        public Style BlockStyle => style;

        public void Configure(Style blockStyle, Material material)
        {
            style = blockStyle;
            lineMaterial = material;
            dirty = true;
            Redraw();
        }

        private void OnEnable() => Redraw();
        private void OnValidate() => dirty = true;   // can't create objects here; Update redraws

        private void Update()
        {
            if (!Application.isPlaying) Redraw();
        }

        /// <summary>Redraws if the collider's size, offset or the style changed since the last drawing.</summary>
        public void Redraw()
        {
            var box = GetComponent<BoxCollider2D>();
            if (box == null) return;
            if (!dirty && box.size == drawnSize && box.offset == drawnOffset && style == drawnStyle) return;
            dirty = false;
            drawnSize = box.size;
            drawnOffset = box.offset;
            drawnStyle = style;

            Vector2 size = box.size, center = box.offset;
            int seed = Mathf.RoundToInt(transform.position.x * 13f + transform.position.y * 71f);

            LineRenderer outline = GetComponent<LineRenderer>();
            if (outline == null) outline = gameObject.AddComponent<LineRenderer>();
            Setup(outline, ink, lineWidth, 0);

            if (style == Style.OneWay)
            {
                float top = center.y + size.y * 0.5f;
                List<Pt> line = InkLines.WobblyLine(new Pt(center.x - size.x * 0.5f, top), new Pt(center.x + size.x * 0.5f, top),
                                                    wobble, 0.6f, seed);
                outline.loop = false;
                SetPoints(outline, line, Vector2.zero);
            }
            else
            {
                List<Pt> box4 = InkLines.WobblyOutline(center.x - size.x * 0.5f, center.y - size.y * 0.5f, size.x, size.y,
                                                       wobble, 0.6f, seed);
                outline.loop = true;
                SetPoints(outline, box4, Vector2.zero);
            }

            Transform hatchChild = transform.Find(HatchName);
            bool wantHatch = style == Style.Solid && hatchSpacing > 0f && size.x > 0.2f && size.y > 0.2f;
            if (!wantHatch)
            {
                if (hatchChild != null) hatchChild.gameObject.SetActive(false);
                return;
            }
            if (hatchChild == null)
            {
                hatchChild = new GameObject(HatchName).transform;
                hatchChild.SetParent(transform, false);
            }
            hatchChild.gameObject.SetActive(true);
            hatchChild.gameObject.layer = gameObject.layer;
            var hatchLine = hatchChild.GetComponent<LineRenderer>();
            if (hatchLine == null) hatchLine = hatchChild.gameObject.AddComponent<LineRenderer>();
            Setup(hatchLine, hatch, lineWidth * 0.4f, -1);
            hatchLine.loop = false;
            const float inset = 0.08f;
            SetPoints(hatchLine, LevelArt.Hatch(size.x - inset * 2f, size.y - inset * 2f, hatchSpacing), center);
        }

        private void Setup(LineRenderer line, Color color, float width, int order)
        {
            line.useWorldSpace = false;
            line.sharedMaterial = lineMaterial != null ? lineMaterial : InkMaterial.Runtime;
            line.widthMultiplier = width;
            line.startColor = line.endColor = color;
            line.numCapVertices = 3;
            line.numCornerVertices = 2;
            line.sortingOrder = order;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
        }

        private static void SetPoints(LineRenderer line, List<Pt> points, Vector2 offset)
        {
            var positions = new Vector3[points.Count];
            for (int i = 0; i < points.Count; i++) positions[i] = new Vector3(points[i].X + offset.x, points[i].Y + offset.y, 0f);
            line.positionCount = positions.Length;
            line.SetPositions(positions);
        }
    }
}
