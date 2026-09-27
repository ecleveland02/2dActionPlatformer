using System.Collections.Generic;

namespace Margin.Combat
{
    /// <summary>Anything that puts out attack hitboxes (player sword, enemies, projectiles). Read by the F1 view.</summary>
    public interface IHitboxSource
    {
        Faction Faction { get; }
        /// <summary>World hitboxes out on the current tick (empty when not attacking).</summary>
        IReadOnlyList<AabbBox> ActiveHitboxes { get; }
    }

    /// <summary>Registry of hitbox sources. Sources add themselves in OnEnable and remove themselves in OnDisable.</summary>
    public static class HitboxSources
    {
        private static readonly List<IHitboxSource> active = new List<IHitboxSource>();

        public static IReadOnlyList<IHitboxSource> Active => active;

        public static void Register(IHitboxSource source)
        {
            if (!active.Contains(source)) active.Add(source);
        }

        public static void Unregister(IHitboxSource source) => active.Remove(source);
    }
}
