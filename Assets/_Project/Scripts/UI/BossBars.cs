using System.Collections.Generic;

namespace Margin.UI
{
    /// <summary>Anything that wants the big health bar at the bottom of the screen (spec 14): bosses, mini-bosses.</summary>
    public interface IBossBarSource
    {
        string BossName { get; }
        /// <summary>Health left, 0..1.</summary>
        float HealthFraction { get; }
        /// <summary>Health fractions where a new phase starts (spec 10: 0.5, and 0.2 for the final boss). Drawn as notches.</summary>
        IReadOnlyList<float> PhaseMarks { get; }
        /// <summary>True while the bar should be on screen (the fight is on).</summary>
        bool ShowBossBar { get; }
    }

    /// <summary>Registry of boss bar sources; the HUD shows the first one that wants its bar shown.</summary>
    public static class BossBars
    {
        private static readonly List<IBossBarSource> sources = new List<IBossBarSource>();

        public static IReadOnlyList<IBossBarSource> Sources => sources;

        public static void Register(IBossBarSource source)
        {
            if (!sources.Contains(source)) sources.Add(source);
        }

        public static void Unregister(IBossBarSource source) => sources.Remove(source);

        /// <summary>The boss whose bar should be on screen now, or null.</summary>
        public static IBossBarSource Current
        {
            get
            {
                foreach (IBossBarSource source in sources)
                    if (source != null && source.ShowBossBar) return source;
                return null;
            }
        }
    }
}
