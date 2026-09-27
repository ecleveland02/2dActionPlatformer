using System.Collections.Generic;
using Margin.UI;
using NUnit.Framework;

namespace Margin.Tests
{
    public class BossBarsTests
    {
        private sealed class FakeBoss : IBossBarSource
        {
            public string BossName { get; set; } = "The Highlighter";
            public float HealthFraction { get; set; } = 1f;
            public IReadOnlyList<float> PhaseMarks { get; set; } = new[] { 0.5f };
            public bool ShowBossBar { get; set; }
        }

        private readonly List<IBossBarSource> added = new List<IBossBarSource>();

        [TearDown]
        public void TearDown()
        {
            foreach (IBossBarSource s in added) BossBars.Unregister(s);
            added.Clear();
        }

        private FakeBoss Add(bool showing)
        {
            var boss = new FakeBoss { ShowBossBar = showing };
            BossBars.Register(boss);
            added.Add(boss);
            return boss;
        }

        [Test]
        public void Current_IsTheFirstSourceThatWantsItsBar()
        {
            FakeBoss idle = Add(false);
            FakeBoss fighting = Add(true);
            Assert.AreSame(fighting, BossBars.Current);
            idle.ShowBossBar = true;
            Assert.AreSame(idle, BossBars.Current);
        }

        [Test]
        public void Current_IsNull_WhenNoFightIsOn()
        {
            Add(false);
            Assert.IsNull(BossBars.Current);
        }

        [Test]
        public void RegisteringTwice_AddsOnce_AndUnregisterRemoves()
        {
            FakeBoss boss = Add(true);
            BossBars.Register(boss);
            int count = 0;
            foreach (IBossBarSource s in BossBars.Sources) if (s == boss) count++;
            Assert.AreEqual(1, count);
            BossBars.Unregister(boss);
            Assert.IsNull(BossBars.Current);
        }
    }
}
