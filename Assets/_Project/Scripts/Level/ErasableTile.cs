using System.Collections.Generic;
using Margin.Core;
using Margin.Physics;
using UnityEngine;

namespace Margin.Level
{
    /// <summary>
    /// A floor tile the Eraser Crawler can rub out (spec 9: deletes platform tiles it walks over; pressure and
    /// routing). Touched, it waits <see cref="eraseDelayFrames"/> (so the crawler has walked on), fades out over
    /// <see cref="fadeFrames"/> and stops being solid. It's redrawn after <see cref="restoreFrames"/> unless something
    /// is standing where it would come back, and is whole again whenever its room is entered.
    /// Put it on the Ground layer with a BoxCollider2D and a LevelBlock (its drawing).
    /// </summary>
    [RequireComponent(typeof(BoxCollider2D))]
    public sealed class ErasableTile : MonoBehaviour, ITickable, IRoomReset
    {
        private enum State { Solid, Marked, Fading, Gone }

        [SerializeField, Min(0)] private int eraseDelayFrames = 40;
        [SerializeField, Min(1)] private int fadeFrames = 20;
        [Tooltip("Frames until the tile is drawn back in. 0 = never (only when the room is entered again).")]
        [SerializeField, Min(0)] private int restoreFrames = 360;

        private BoxCollider2D box;
        private LineRenderer[] lines;
        private Color[] colors;
        private State state;
        private int frames;

        public int TickOrder => 26;
        public bool Solid => state != State.Gone;

        private void Awake()
        {
            box = GetComponent<BoxCollider2D>();
        }

        private void OnEnable() => GameLoop.Register(this);
        private void OnDisable() => GameLoop.Unregister(this);

        /// <summary>The crawler is on it: start rubbing it out (no effect if it's already going).</summary>
        public void Touch()
        {
            if (state != State.Solid) return;
            state = State.Marked;
            frames = 0;
        }

        public void ResetForRoom() => Restore();

        public void Tick()
        {
            frames++;
            switch (state)
            {
                case State.Marked:
                    if (frames >= eraseDelayFrames)
                    {
                        state = State.Fading;
                        frames = 0;
                    }
                    break;
                case State.Fading:
                    SetAlpha(1f - frames / (float)fadeFrames);
                    if (frames >= fadeFrames)
                    {
                        state = State.Gone;
                        frames = 0;
                        box.enabled = false;
                        UnityEngine.Physics2D.SyncTransforms();
                        if (FX.InkSplatter.Instance != null) FX.InkSplatter.Instance.Burst(transform.position, Vector2.up, 4);
                    }
                    break;
                case State.Gone:
                    if (restoreFrames > 0 && frames >= restoreFrames && !Occupied()) Restore();
                    break;
            }
        }

        private void Restore()
        {
            state = State.Solid;
            frames = 0;
            if (box == null) box = GetComponent<BoxCollider2D>();
            box.enabled = true;
            UnityEngine.Physics2D.SyncTransforms();
            SetAlpha(1f);
        }

        /// <summary>Something is where the tile would reappear (it mustn't trap anyone inside it).</summary>
        private bool Occupied()
        {
            Rect r = new Rect((Vector2)transform.position + box.offset - box.size * 0.5f, box.size);
            foreach (KinematicBody2D body in KinematicBody2D.All)
            {
                if (body == null) continue;
                if (r.Overlaps(new Rect(body.Position - body.Size * 0.5f, body.Size))) return true;
            }
            return false;
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
                Color c = colors[i];
                c.a *= Mathf.Clamp01(a);
                lines[i].startColor = lines[i].endColor = c;
                lines[i].enabled = a > 0.01f;
            }
        }
    }
}
