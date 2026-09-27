using System;
using System.Collections.Generic;

namespace Margin.Enemies
{
    /// <summary>
    /// Attack slots (spec 9, "attack tokens"), in pure C# for testing. An enemy must hold a slot to start an
    /// attack and gives it back when its attack (or combo string) ends. Enemies without a slot wait nearby.
    /// </summary>
    public sealed class AttackTokenPool
    {
        private readonly HashSet<object> holders = new HashSet<object>();

        public int Count => holders.Count;

        /// <summary>
        /// Slots available for a fight: one per engaged enemy (everyone may attack), capped by
        /// <paramref name="maxAttackers"/> unless it is 0 (no cap).
        /// </summary>
        public static int Capacity(int engagedEnemies, int maxAttackers)
        {
            int engaged = Math.Max(0, engagedEnemies);
            return maxAttackers <= 0 ? engaged : Math.Min(engaged, maxAttackers);
        }

        public bool Holds(object who) => holders.Contains(who);

        /// <summary>Takes a slot if one is free (or already held). Returns true if <paramref name="who"/> holds one.</summary>
        public bool TryTake(object who, int capacity)
        {
            if (holders.Contains(who)) return true;
            if (holders.Count >= capacity) return false;
            holders.Add(who);
            return true;
        }

        public void Release(object who) => holders.Remove(who);

        public void Clear() => holders.Clear();
    }
}
