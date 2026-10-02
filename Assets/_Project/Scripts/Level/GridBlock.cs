using System.Collections.Generic;
using Margin.Core;
using Margin.Physics;
using Margin.Player;
using UnityEngine;
using Pt = Margin.Rendering.WeaponShape.Pt;

namespace Margin.Level
{
    /// <summary>
    /// A World 2 moving block (spec 11.2: grid-snapped moving blocks). It follows a GridPath through grid points,
    /// carries anything standing on it (IMovingSolid, used by KinematicBody2D), and pushes anything in its way.
    /// A player squeezed between it and a wall is crushed: pit damage and back to the last door (LevelDirector).
    /// Ticks before the player (TickOrder -50) so riders see where it is this tick. The drawing is a child that is
    /// smoothed between ticks, like the player's interpolated body.
    /// Put it on the Ground layer with a BoxCollider2D; resets to its start when its room is entered.
    /// </summary>
    [RequireComponent(typeof(BoxCollider2D))]
    public sealed class GridBlock : MonoBehaviour, ITickable, IMovingSolid, IRoomReset
    {
        [Tooltip("The route in grid cells, relative to where the block starts. The first point should be (0, 0).")]
        [SerializeField] private List<Vector2Int> path = new List<Vector2Int> { Vector2Int.zero, new Vector2Int(4, 0) };
        [Tooltip("Size of one grid cell in units (World 2's graph paper squares are 1 unit).")]
        [SerializeField, Min(0.1f)] private float cellSize = 1f;
        [SerializeField, Min(0.1f)] private float cellsPerSecond = 3f;
        [Tooltip("Frames it waits on every grid point.")]
        [SerializeField, Min(0)] private int pauseFrames = 45;
        [Tooltip("Extra wait before the very first move, so neighbouring blocks don't move together.")]
        [SerializeField, Min(0)] private int startDelayFrames;
        [Tooltip("Round in a loop (last point back to the first) instead of back and forth.")]
        [SerializeField] private bool loop;
        [Tooltip("The drawing (smoothed between ticks). Optional.")]
        [SerializeField] private Transform visual;

        private BoxCollider2D box;
        private GridPath route;
        private Vector2 home;
        private Vector3 drawFrom, drawTo;
        private float tickedAt = -1f;

        public int TickOrder => -50;
        public Vector2 CarryDelta { get; private set; }
        public int MoveStamp { get; private set; }

        public void Configure(IList<Vector2Int> cells, float speed, int pause, int delay, bool isLoop, Transform drawing)
        {
            path = new List<Vector2Int>(cells);
            cellsPerSecond = speed;
            pauseFrames = pause;
            startDelayFrames = delay;
            loop = isLoop;
            visual = drawing;
        }

        private void Awake()
        {
            box = GetComponent<BoxCollider2D>();
            home = transform.position;
            Restart();
        }

        private void OnEnable() => GameLoop.Register(this);
        private void OnDisable() => GameLoop.Unregister(this);

        public void ResetForRoom()
        {
            transform.position = home;
            UnityEngine.Physics2D.SyncTransforms();
            Restart();
        }

        private void Restart()
        {
            var cells = new List<Pt>();
            foreach (Vector2Int c in path) cells.Add(new Pt(c.x, c.y));
            route = new GridPath(cells, cellsPerSecond, pauseFrames, loop, startDelayFrames);
            CarryDelta = Vector2.zero;
            drawFrom = drawTo = transform.position;
            if (visual != null) visual.position = drawTo;
        }

        public void Tick()
        {
            Vector2 before = transform.position;
            Pt offset = route.Tick();
            Vector2 after = home + new Vector2(offset.X, offset.Y) * cellSize;
            CarryDelta = after - before;
            MoveStamp++;
            drawFrom = before;
            drawTo = after;
            tickedAt = Time.fixedTime;
            if (CarryDelta == Vector2.zero) return;

            transform.position = after;
            UnityEngine.Physics2D.SyncTransforms();   // casts this tick must see the block where it is now
            PushBodies(before, after);
        }

        /// <summary>Shoves bodies the block moved into (riders on top are carried instead, by KinematicBody2D).</summary>
        private void PushBodies(Vector2 before, Vector2 after)
        {
            Vector2 half = box.size * 0.5f;
            Vector2 delta = after - before;
            Rect now = new Rect(after + box.offset - half, box.size);
            float topBefore = before.y + box.offset.y + half.y;

            foreach (KinematicBody2D body in KinematicBody2D.All)
            {
                if (body == null) continue;
                Rect b = new Rect(body.Position - body.Size * 0.5f, body.Size);
                if (!now.Overlaps(b)) continue;
                if (b.yMin >= topBefore - 0.05f) continue;   // standing on top: a rider, carried

                // Push out along the block's motion by however much it now overlaps.
                Vector2 push;
                if (Mathf.Abs(delta.x) >= Mathf.Abs(delta.y))
                    push = new Vector2(delta.x > 0f ? now.xMax - b.xMin : now.xMin - b.xMax, 0f);
                else
                    push = new Vector2(0f, delta.y > 0f ? now.yMax - b.yMin : now.yMin - b.yMax);
                Vector2 moved = body.Push(push, box);
                if ((push - moved).magnitude > 0.05f) Crushed(body);
            }
        }

        private static void Crushed(KinematicBody2D body)
        {
            var player = body.GetComponent<PlayerController>();
            if (player != null && LevelDirector.Instance != null) LevelDirector.Instance.Crush(player);
        }

        /// <summary>Smooth drawing between ticks (the collider itself moves in tick steps).</summary>
        private void LateUpdate()
        {
            if (visual == null) return;
            if (Time.fixedTime - tickedAt > Time.fixedDeltaTime * 1.5f) drawFrom = drawTo;   // paused: hold still
            float t = Mathf.Clamp01((Time.time - Time.fixedTime) / Time.fixedDeltaTime);
            visual.position = Vector3.Lerp(drawFrom, drawTo, t);
        }

        private void OnDrawGizmos()
        {
            Vector3 start = Application.isPlaying ? (Vector3)home : transform.position;
            Gizmos.color = new Color(0.2f, 0.5f, 1f, 0.8f);
            for (int i = 0; i + 1 < path.Count; i++)
                Gizmos.DrawLine(start + (Vector3)(Vector2)path[i] * cellSize, start + (Vector3)(Vector2)path[i + 1] * cellSize);
            if (loop && path.Count > 2)
                Gizmos.DrawLine(start + (Vector3)(Vector2)path[path.Count - 1] * cellSize, start + (Vector3)(Vector2)path[0] * cellSize);
        }
    }
}
