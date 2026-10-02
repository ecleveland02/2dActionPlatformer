using System;
using System.Collections.Generic;
using Pt = Margin.Rendering.WeaponShape.Pt;

namespace Margin.Level
{
    /// <summary>
    /// The route of a World 2 grid block (spec 11.2: grid-snapped moving blocks), in pure C# for testing. The block
    /// waits on a grid point, slides to the next one (easing in and out, so it "clunks" into place), waits again, and
    /// so on: back and forth (ping-pong) or round in a loop. Points are in grid cells relative to the start; one
    /// <see cref="Tick"/> per game tick returns the offset in cells.
    /// </summary>
    public sealed class GridPath
    {
        private readonly List<Pt> points;
        private readonly float cellsPerTick;
        private readonly int pauseFrames;
        private readonly bool loop;
        private int from, to, step = 1;
        private int waitLeft;
        private float travelled;   // cells along the current segment

        public Pt Offset { get; private set; }
        /// <summary>True while sliding between points (false while waiting on one).</summary>
        public bool Moving => waitLeft <= 0 && points.Count > 1;

        /// <param name="cellsPerSecond">Slide speed.</param>
        /// <param name="pauseFrames">Wait on every point, in frames.</param>
        /// <param name="startDelayFrames">Extra wait before the first move (so neighbouring blocks don't move in step).</param>
        public GridPath(IList<Pt> cells, float cellsPerSecond, int pauseFrames, bool loop, int startDelayFrames = 0)
        {
            points = new List<Pt>(cells != null && cells.Count > 0 ? cells : new[] { new Pt(0f, 0f) });
            cellsPerTick = Math.Max(0.001f, cellsPerSecond / 60f);
            this.pauseFrames = Math.Max(0, pauseFrames);
            this.loop = loop;
            from = 0;
            to = points.Count > 1 ? 1 : 0;
            waitLeft = this.pauseFrames + Math.Max(0, startDelayFrames);
            Offset = points[0];
        }

        /// <summary>Advances one game tick and returns the new offset (cells).</summary>
        public Pt Tick()
        {
            if (points.Count < 2) return Offset;
            if (waitLeft > 0)
            {
                waitLeft--;
                return Offset;
            }

            Pt a = points[from], b = points[to];
            float length = Distance(a, b);
            travelled += cellsPerTick;
            if (length <= 0f || travelled >= length - 1e-4f)   // tolerance: 40 x 0.1 adds up to 3.9999998
            {
                // Arrived: snap exactly onto the grid point and wait.
                Offset = b;
                travelled = 0f;
                waitLeft = pauseFrames;
                Advance();
                return Offset;
            }

            float t = travelled / length;
            t = t * t * (3f - 2f * t);   // ease in and out
            Offset = new Pt(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t);
            return Offset;
        }

        private void Advance()
        {
            from = to;
            if (loop)
            {
                to = (to + 1) % points.Count;
                return;
            }
            if (to + step >= points.Count || to + step < 0) step = -step;
            to += step;
        }

        private static float Distance(Pt a, Pt b)
        {
            float dx = b.X - a.X, dy = b.Y - a.Y;
            return (float)Math.Sqrt(dx * dx + dy * dy);
        }
    }
}
