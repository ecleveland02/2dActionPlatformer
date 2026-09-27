using System.Collections.Generic;

namespace Margin.Bosses
{
    /// <summary>One move a boss may pick: its weight and the distances to the player it's used at.</summary>
    public readonly struct MoveOption
    {
        public readonly int Weight;
        public readonly float MinRange, MaxRange;

        public MoveOption(int weight, float minRange, float maxRange)
        {
            Weight = weight;
            MinRange = minRange;
            MaxRange = maxRange;
        }

        /// <summary>maxRange 0 means no upper limit.</summary>
        public bool InRange(float distance) => distance >= MinRange && (MaxRange <= 0f || distance <= MaxRange);
    }

    /// <summary>
    /// Chooses a boss's next move (spec 10), in pure C# for testing: a weighted random pick among the moves whose
    /// range fits the distance to the player, never the same move three times in a row (so patterns stay readable
    /// but not spammy). If no move fits the distance, range is ignored. Uses its own seeded random numbers.
    /// </summary>
    public sealed class BossMovePicker
    {
        private uint state;
        private int last = -1, repeats;

        public BossMovePicker(uint seed) => Reseed(seed);

        public void Reseed(uint seed)
        {
            state = seed == 0 ? 0x9E3779B9u : seed;
            last = -1;
            repeats = 0;
        }

        /// <summary>Index of the chosen option, or -1 if there are none with weight.</summary>
        public int Pick(IReadOnlyList<MoveOption> options, float distance)
        {
            int pick = PickWhere(options, distance, useRange: true);
            if (pick < 0) pick = PickWhere(options, distance, useRange: false);
            if (pick < 0) return -1;
            repeats = pick == last ? repeats + 1 : 1;
            last = pick;
            return pick;
        }

        private int PickWhere(IReadOnlyList<MoveOption> options, float distance, bool useRange)
        {
            int total = 0;
            for (int i = 0; i < options.Count; i++) total += Allowed(options, i, distance, useRange);
            if (total <= 0) return -1;
            int roll = (int)(Next() % (uint)total);
            for (int i = 0; i < options.Count; i++)
            {
                roll -= Allowed(options, i, distance, useRange);
                if (roll < 0) return i;
            }
            return -1;
        }

        private int Allowed(IReadOnlyList<MoveOption> options, int i, float distance, bool useRange)
        {
            MoveOption o = options[i];
            if (o.Weight <= 0) return 0;
            if (useRange && !o.InRange(distance)) return 0;
            if (i == last && repeats >= 2 && HasAlternative(options, i)) return 0;
            return o.Weight;
        }

        private static bool HasAlternative(IReadOnlyList<MoveOption> options, int except)
        {
            for (int i = 0; i < options.Count; i++)
                if (i != except && options[i].Weight > 0) return true;
            return false;
        }

        /// <summary>xorshift32: small, fast, repeatable.</summary>
        public uint Next()
        {
            state ^= state << 13;
            state ^= state >> 17;
            state ^= state << 5;
            return state;
        }

        /// <summary>A whole number in [min, max] (inclusive).</summary>
        public int Range(int min, int max) => max <= min ? min : min + (int)(Next() % (uint)(max - min + 1));
    }

    /// <summary>Which phase a boss is in for its remaining health (spec 10: phase change at 50%).</summary>
    public static class BossPhases
    {
        /// <summary>
        /// thresholds[i] = the health fraction at or below which phase i starts, in order (e.g. 1, 0.5).
        /// Returns the last phase whose threshold has been reached.
        /// </summary>
        public static int PhaseFor(float healthFraction, IReadOnlyList<float> thresholds)
        {
            int phase = 0;
            for (int i = 0; i < thresholds.Count; i++)
                if (healthFraction <= thresholds[i] + 1e-6f) phase = i;
            return phase;
        }
    }
}
