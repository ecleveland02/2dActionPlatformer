using System;
using Margin.Core;

namespace Margin.Input
{
    /// <summary>
    /// Remembers recent button presses so that an input pressed a few frames too early
    /// still counts. Example: pressing Jump 3 frames before landing still jumps on landing.
    ///
    /// Window rule: a window of N frames covers N ticks, and the tick of the press is frame 1.
    /// A press on tick 100 with a 6 frame window can be consumed on ticks 100 to 105.
    ///
    /// Pure C# (no UnityEngine) so it can be unit tested without entering Play mode.
    /// </summary>
    public sealed class InputBuffer
    {
        /// <summary>Fallback window used when no per-action window is configured.</summary>
        public const int DefaultWindowFrames = 6;

        // Marks "no press stored" for an action.
        private const int NoPress = int.MinValue;

        private static readonly int ActionCount = Enum.GetValues(typeof(BufferedAction)).Length;

        private readonly IFrameSource clock;

        // One slot per action, indexed by (int)BufferedAction. Only the most recent
        // unconsumed press matters, so a single frame stamp per action is enough.
        private readonly int[] pressFrames;
        private readonly int[] windowFrames;

        public InputBuffer(IFrameSource clock, int defaultWindowFrames = DefaultWindowFrames)
        {
            if (clock == null) throw new ArgumentNullException(nameof(clock));
            ValidateWindow(defaultWindowFrames);

            this.clock = clock;
            pressFrames = new int[ActionCount];
            windowFrames = new int[ActionCount];

            for (int i = 0; i < ActionCount; i++)
            {
                pressFrames[i] = NoPress;
                windowFrames[i] = defaultWindowFrames;
            }
        }

        /// <summary>Sets how many frames a press of this action stays usable.</summary>
        public void SetWindow(BufferedAction action, int frames)
        {
            ValidateWindow(frames);
            windowFrames[(int)action] = frames;
        }

        public int GetWindow(BufferedAction action)
        {
            return windowFrames[(int)action];
        }

        /// <summary>Stores a press stamped with the current frame. A newer press replaces an older one.</summary>
        public void Record(BufferedAction action)
        {
            pressFrames[(int)action] = clock.CurrentFrame;
        }

        /// <summary>True if the action was pressed within its configured window. Does not use up the press.</summary>
        public bool IsBuffered(BufferedAction action)
        {
            return IsBuffered(action, windowFrames[(int)action]);
        }

        /// <summary>True if the action was pressed within the last <paramref name="withinFrames"/> frames.</summary>
        public bool IsBuffered(BufferedAction action, int withinFrames)
        {
            int age = FramesSincePress(action);
            // age 0 means "pressed this tick", which is frame 1 of the window.
            return age >= 0 && age < withinFrames;
        }

        /// <summary>
        /// If the action is buffered, uses up the press and returns true. Using it up prevents
        /// one button press from triggering two actions (e.g. jumping twice).
        /// </summary>
        public bool Consume(BufferedAction action)
        {
            return Consume(action, windowFrames[(int)action]);
        }

        public bool Consume(BufferedAction action, int withinFrames)
        {
            if (!IsBuffered(action, withinFrames)) return false;

            Clear(action);
            return true;
        }

        public void Clear(BufferedAction action)
        {
            pressFrames[(int)action] = NoPress;
        }

        public void ClearAll()
        {
            for (int i = 0; i < ActionCount; i++) pressFrames[i] = NoPress;
        }

        /// <summary>
        /// Frames since the stored press (0 = this tick), or -1 if nothing is stored.
        /// Stale presses are still reported so the debug overlay can show them.
        /// </summary>
        public int FramesSincePress(BufferedAction action)
        {
            int pressFrame = pressFrames[(int)action];
            if (pressFrame == NoPress) return -1;

            int age = clock.CurrentFrame - pressFrame;
            // A negative age means the clock was reset after the press; treat it as nothing stored.
            return age < 0 ? -1 : age;
        }

        private static void ValidateWindow(int frames)
        {
            if (frames < 1)
                throw new ArgumentOutOfRangeException(nameof(frames), frames, "A buffer window must be at least 1 frame.");
        }
    }
}
