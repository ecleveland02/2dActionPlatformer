using Margin.Core;
using Margin.Level;
using Margin.Rendering;
using UnityEngine;

namespace Margin.Bosses
{
    /// <summary>
    /// A platform in the Stapler Titan's arena (spec 10: phase 2 staples pin platforms in place and remove others).
    /// It may move (a GridBlock). <see cref="Pin"/> staples it where it is: it stops moving and gets a staple drawn
    /// across it. <see cref="Remove"/> tears it out: it fades away and stops being solid. Everything goes back to
    /// normal when the fight resets or the room is entered.
    /// </summary>
    public sealed class StaplePlatform : MonoBehaviour, IRoomReset
    {
        [Tooltip("Phase 2 keeps this one (stapled in place). Unticked ones are torn out.")]
        [SerializeField] private bool keepInPhase2;

        private GridBlock mover;
        private BoxCollider2D box;
        private LineRenderer[] lines;
        private Color[] colors;
        private LineRenderer staple;

        public bool KeepInPhase2
        {
            get => keepInPhase2;
            set => keepInPhase2 = value;
        }

        public bool Pinned { get; private set; }
        public bool Removed { get; private set; }

        private void Awake()
        {
            mover = GetComponent<GridBlock>();
            box = GetComponent<BoxCollider2D>();
        }

        public void Pin()
        {
            if (Pinned || Removed) return;
            Pinned = true;
            if (mover != null) mover.Frozen = true;   // stays exactly where it is
            DrawStaple(true);
        }

        public void Remove()
        {
            if (Removed) return;
            Removed = true;
            if (box != null) box.enabled = false;
            if (mover != null) mover.Frozen = true;   // a torn-out block must not keep shoving people
            SetAlpha(0.15f);
            UnityEngine.Physics2D.SyncTransforms();
        }

        public void Restore()
        {
            Pinned = false;
            Removed = false;
            if (box != null) box.enabled = true;
            if (mover != null) mover.ResetForRoom();
            SetAlpha(1f);
            DrawStaple(false);
            UnityEngine.Physics2D.SyncTransforms();
        }

        public void ResetForRoom() => Restore();

        /// <summary>About to be torn out: flickers (call every tick with a frame count). Spec 10: telegraph everything.</summary>
        public void Warn(int frame)
        {
            if (Removed || Pinned) return;
            SetAlpha(frame / 4 % 2 == 0 ? 0.45f : 1f);
        }

        /// <summary>A staple bent over the platform: two legs down into it and a bar across the top.</summary>
        private void DrawStaple(bool on)
        {
            if (staple == null)
            {
                if (!on) return;
                var go = new GameObject("Staple");
                go.transform.SetParent(transform, false);
                staple = go.AddComponent<LineRenderer>();
                staple.useWorldSpace = false;
                staple.sharedMaterial = InkMaterial.Runtime;
                staple.widthMultiplier = 0.1f;
                staple.numCornerVertices = 2;
                staple.sortingOrder = 7;
                staple.startColor = staple.endColor = new Color32(0x8A, 0x8D, 0x94, 0xFF);
                staple.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                staple.receiveShadows = false;
            }
            staple.enabled = on;
            if (!on || box == null) return;
            float w = box.size.x * 0.35f, top = box.offset.y + box.size.y * 0.5f;
            staple.positionCount = 4;
            staple.SetPositions(new[]
            {
                new Vector3(-w, top - 0.3f), new Vector3(-w, top + 0.06f), new Vector3(w, top + 0.06f), new Vector3(w, top - 0.3f),
            });
        }

        private void SetAlpha(float a)
        {
            if (lines == null)
            {
                lines = GetComponentsInChildren<LineRenderer>(true);
                colors = new Color[lines.Length];
                for (int i = 0; i < lines.Length; i++) colors[i] = lines[i].startColor;
            }
            for (int i = 0; i < lines.Length; i++)
            {
                if (lines[i] == staple) continue;
                Color c = colors[i];
                c.a *= a;
                lines[i].startColor = lines[i].endColor = c;
            }
        }
    }
}
