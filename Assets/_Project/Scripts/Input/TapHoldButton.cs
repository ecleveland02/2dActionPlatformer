using System.Collections.Generic;

namespace Margin.Input
{
    public enum TapHoldResult { None, Tap, Hold }

    /// <summary>
    /// Turns one button into two actions: a quick tap (released before HoldFrames) or a hold (kept down for
    /// HoldFrames). Used for "tap = light attack, hold = heavy attack".
    ///
    /// Presses and releases arrive from Input System callbacks (between ticks) and are queued in order, so a
    /// press and release that both happen between two ticks still count as a tap.
    /// Frame rule: the tick the press is processed is frame 1; Hold fires on frame HoldFrames.
    /// Note the unavoidable trade-off: a tap is only known on release, so tap actions start on release.
    /// </summary>
    public sealed class TapHoldButton
    {
        private readonly Queue<bool> events = new Queue<bool>();   // true = press, false = release
        private bool down;
        private bool resolved;
        private int heldTicks;

        public TapHoldButton(int holdFrames) { HoldFrames = holdFrames; }

        public int HoldFrames { get; set; }

        /// <summary>Button is down and it's not yet decided whether this is a tap or a hold.</summary>
        public bool Pending => down && !resolved;

        public void QueuePress() => events.Enqueue(true);
        public void QueueRelease() => events.Enqueue(false);

        /// <summary>Call once per tick. Returns Tap on the tick of a quick release, Hold on the tick the hold completes.</summary>
        public TapHoldResult Tick()
        {
            TapHoldResult result = TapHoldResult.None;

            while (events.Count > 0)
            {
                bool press = events.Dequeue();
                if (press)
                {
                    down = true;
                    resolved = false;
                    heldTicks = 0;
                }
                else if (down)
                {
                    if (!resolved) result = TapHoldResult.Tap;
                    down = false;
                }
            }

            if (down && !resolved)
            {
                heldTicks++;
                if (heldTicks >= HoldFrames)
                {
                    resolved = true;
                    result = TapHoldResult.Hold;
                }
            }
            return result;
        }

        public void Reset()
        {
            events.Clear();
            down = resolved = false;
            heldTicks = 0;
        }
    }
}
