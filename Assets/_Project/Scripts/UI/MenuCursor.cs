using System;

namespace Margin.UI
{
    /// <summary>
    /// Which menu item is selected, and how a held stick or key scrolls through them (pure C# for testing).
    /// A tap moves once; holding waits <c>repeatDelay</c> frames, then moves every <c>repeatInterval</c> frames.
    /// The selection wraps around the ends.
    /// </summary>
    public sealed class MenuCursor
    {
        private int heldDirection;
        private float heldFrames;
        private float nextRepeat;

        public int Index { get; private set; }
        public int Count { get; private set; }

        public MenuCursor(int count)
        {
            SetCount(count);
        }

        public void SetCount(int count)
        {
            Count = Math.Max(0, count);
            Index = Count == 0 ? 0 : Math.Min(Index, Count - 1);
        }

        /// <summary>Selects an item directly (e.g. the mouse hovered it).</summary>
        public void Select(int index)
        {
            if (Count > 0) Index = Math.Max(0, Math.Min(Count - 1, index));
        }

        /// <summary>Moves by delta (+1 = next item), wrapping around.</summary>
        public void Move(int delta)
        {
            if (Count == 0) return;
            Index = ((Index + delta) % Count + Count) % Count;
        }

        /// <summary>
        /// Feeds the held direction (-1 up, +1 down, 0 released) for this frame and moves when it should.
        /// Returns true if the selection moved.
        /// </summary>
        public bool Hold(int direction, float frames, float repeatDelay, float repeatInterval)
        {
            direction = Math.Sign(direction);
            if (direction == 0)
            {
                heldDirection = 0;
                return false;
            }
            if (direction != heldDirection)
            {
                // A fresh press (or a change of direction) moves right away.
                heldDirection = direction;
                heldFrames = 0f;
                nextRepeat = repeatDelay;
                Move(direction);
                return true;
            }
            heldFrames += frames;
            if (heldFrames < nextRepeat) return false;
            nextRepeat += Math.Max(1f, repeatInterval);
            Move(direction);
            return true;
        }
    }
}
