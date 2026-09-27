using System;

namespace Margin.Combat
{
    /// <summary>
    /// Tracks a combo being taken by one target and scales its damage down (pure C# for testing).
    /// Hit 1 does full damage, each later hit does <c>step</c> less, never below <c>floor</c>.
    /// Example with step 0.15 and floor 0.5: 100%, 85%, 70%, 55%, 50%, 50%...
    /// </summary>
    public sealed class ComboScaling
    {
        public int Hits { get; private set; }
        public int Damage { get; private set; }
        public bool Active => Hits > 0;

        /// <summary>Damage multiplier for the Nth hit of a combo (1 = first hit).</summary>
        public static float Scale(int hitNumber, float step, float floor) =>
            Math.Max(floor, 1f - step * Math.Max(0, hitNumber - 1));

        /// <summary>Counts a hit and returns its scaled damage. A hit with any base damage deals at least 1.</summary>
        public int Register(int baseDamage, float step, float floor)
        {
            Hits++;
            int scaled = (int)Math.Round(baseDamage * Scale(Hits, step, floor), MidpointRounding.AwayFromZero);
            if (baseDamage > 0) scaled = Math.Max(1, scaled);
            Damage += scaled;
            return scaled;
        }

        public void End()
        {
            Hits = 0;
            Damage = 0;
        }
    }
}
