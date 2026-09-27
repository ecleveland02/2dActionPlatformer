using System.Collections.Generic;
using UnityEngine;

namespace Margin.Abilities
{
    /// <summary>Something the Grapple Line can hook and yank toward the player (spec 8: pull light enemies in).</summary>
    public interface IGrappleTarget
    {
        bool CanBeGrappled { get; }
        Vector2 GrapplePoint { get; }
        /// <summary>Hooked: fly with this velocity, stunned for this many frames.</summary>
        void OnGrappled(Vector2 pullVelocity, int stunFrames);
    }

    /// <summary>Registry of grapple targets. Targets add themselves in OnEnable and remove themselves in OnDisable.</summary>
    public static class GrappleTargets
    {
        private static readonly List<IGrappleTarget> all = new List<IGrappleTarget>();

        public static IReadOnlyList<IGrappleTarget> All => all;

        public static void Register(IGrappleTarget target)
        {
            if (!all.Contains(target)) all.Add(target);
        }

        public static void Unregister(IGrappleTarget target) => all.Remove(target);
    }

    /// <summary>Static ability events (spec 3.2 event bus: OnAbilityUnlocked).</summary>
    public static class AbilityEvents
    {
        public static event System.Action<Ability> Unlocked;
        public static void RaiseUnlocked(Ability ability) => Unlocked?.Invoke(ability);
    }

    /// <summary>The abilities bosses give (spec 8).</summary>
    public enum Ability { Dash, GrappleLine, DoubleJump, GroundPound, WallCling, CarbonCopy }
}
