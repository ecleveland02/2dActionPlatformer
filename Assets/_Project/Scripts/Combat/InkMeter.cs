using System;

namespace Margin.Combat
{
    /// <summary>
    /// The ink meter (spec 6.6), in pure C#. Filled by landing hits (AttackData.inkGain) and parries,
    /// spent on specials (50) and the Redraw heal (100). After a stretch without hitting anything it drains
    /// slowly, to encourage aggression. All timing in frames.
    /// </summary>
    public sealed class InkMeter
    {
        public int Max { get; }
        public int DecayDelayFrames { get; }
        public int DecayIntervalFrames { get; }

        public int Value { get; private set; }
        public int FramesSinceHit { get; private set; }
        public float Fraction => Max > 0 ? Value / (float)Max : 0f;

        /// <param name="max">Spec: 100.</param>
        /// <param name="decayDelayFrames">Spec: 5 seconds = 300 frames without hitting before it drains.</param>
        /// <param name="decayIntervalFrames">Spec: 1 ink per second = every 60 frames.</param>
        public InkMeter(int max = 100, int decayDelayFrames = 300, int decayIntervalFrames = 60)
        {
            Max = Math.Max(1, max);
            DecayDelayFrames = Math.Max(0, decayDelayFrames);
            DecayIntervalFrames = Math.Max(1, decayIntervalFrames);
        }

        /// <summary>Adds ink (clamped to Max) and resets the decay timer, as landing a hit does.</summary>
        public void Gain(int amount)
        {
            Value = Math.Min(Max, Value + Math.Max(0, amount));
            FramesSinceHit = 0;
        }

        public bool CanSpend(int amount) => amount <= Value;

        public bool TrySpend(int amount)
        {
            if (amount < 0 || !CanSpend(amount)) return false;
            Value -= amount;
            return true;
        }

        /// <summary>Call once per gameplay tick. Drains 1 ink every interval once the delay has passed.</summary>
        public void Tick()
        {
            FramesSinceHit++;
            int past = FramesSinceHit - DecayDelayFrames;
            if (past > 0 && past % DecayIntervalFrames == 0 && Value > 0) Value--;
        }

        public void Reset()
        {
            Value = 0;
            FramesSinceHit = 0;
        }
    }
}
