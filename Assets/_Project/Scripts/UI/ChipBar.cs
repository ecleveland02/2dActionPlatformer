using System;

namespace Margin.UI
{
    /// <summary>
    /// A meter with a "damage chip" (pure C# for testing): when the value drops, the lost part stays visible as a
    /// lighter trail, holds for a moment, then drains down to the new value. That makes each hit readable at a glance.
    /// Gains show instantly (the trail jumps up with the value).
    /// Values are fractions 0..1. Time is in frames (60 per second), and can be fractional for real-time UI.
    /// </summary>
    public sealed class ChipBar
    {
        private float hold;

        /// <summary>The current value.</summary>
        public float Value { get; private set; }

        /// <summary>The trailing chip: always at or above Value.</summary>
        public float Trail { get; private set; }

        /// <summary>True while the trail still shows lost value.</summary>
        public bool Draining => Trail > Value;

        public ChipBar(float start = 1f)
        {
            Value = Trail = Clamp01(start);
        }

        /// <summary>Sets the value. A drop (re)starts the hold, so a combo's hits pile up into one chip.</summary>
        public void Set(float fraction, int holdFrames)
        {
            fraction = Clamp01(fraction);
            if (fraction < Value) hold = holdFrames;
            else if (fraction > Trail) Trail = fraction;
            Value = fraction;
        }

        /// <summary>Advances time: waits out the hold, then drains the trail by drainPerFrame each frame.</summary>
        public void Tick(float frames, float drainPerFrame)
        {
            if (frames <= 0f) return;
            if (hold > 0f)
            {
                float used = Math.Min(hold, frames);
                hold -= used;
                frames -= used;
            }
            if (frames > 0f) Trail = Math.Max(Value, Trail - drainPerFrame * frames);
        }

        /// <summary>Jumps straight to a value with no trail (spawn, respawn).</summary>
        public void Snap(float fraction)
        {
            Value = Trail = Clamp01(fraction);
            hold = 0f;
        }

        private static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;
    }
}
