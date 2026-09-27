using System.Collections.Generic;
using Margin.Bosses;
using NUnit.Framework;

namespace Margin.Tests
{
    /// <summary>Boss move choice and phase rules (spec 10).</summary>
    public class BossLogicTests
    {
        [Test]
        public void Picker_OnlyPicksMovesInRange()
        {
            var picker = new BossMovePicker(7u);
            var options = new List<MoveOption>
            {
                new MoveOption(5, 0f, 3f),    // close only
                new MoveOption(5, 6f, 0f),    // far only
            };
            for (int i = 0; i < 50; i++)
            {
                Assert.AreEqual(0, picker.Pick(options, 1f), "close: only the close move");
                picker.Reseed((uint)i + 1u);
            }
            for (int i = 0; i < 50; i++)
            {
                Assert.AreEqual(1, picker.Pick(options, 9f), "far: only the far move");
                picker.Reseed((uint)i + 100u);
            }
        }

        [Test]
        public void Picker_IgnoresRange_WhenNothingFits()
        {
            var picker = new BossMovePicker(3u);
            var options = new List<MoveOption> { new MoveOption(1, 10f, 12f) };
            Assert.AreEqual(0, picker.Pick(options, 2f));
        }

        [Test]
        public void Picker_NeverRepeatsAMoveThreeTimes()
        {
            var picker = new BossMovePicker(11u);
            var options = new List<MoveOption> { new MoveOption(100, 0f, 0f), new MoveOption(1, 0f, 0f) };
            int last = -1, run = 0;
            for (int i = 0; i < 500; i++)
            {
                int pick = picker.Pick(options, 1f);
                run = pick == last ? run + 1 : 1;
                last = pick;
                Assert.LessOrEqual(run, 2, "the same move at most twice in a row");
            }
        }

        [Test]
        public void Picker_FollowsTheWeights()
        {
            var picker = new BossMovePicker(99u);
            var options = new List<MoveOption> { new MoveOption(3, 0f, 0f), new MoveOption(1, 0f, 0f), new MoveOption(0, 0f, 0f) };
            var counts = new int[3];
            for (int i = 0; i < 4000; i++) counts[picker.Pick(options, 1f)]++;
            Assert.AreEqual(0, counts[2], "weight 0 is never picked");
            // 3:1 weights, but never three in a row: the heavy move still comes up clearly more often.
            Assert.Greater(counts[0], counts[1] * 1.3f, "weight 3 comes up more than weight 1");
        }

        [Test]
        public void Picker_NoOptions_ReturnsMinusOne()
        {
            var picker = new BossMovePicker(1u);
            Assert.AreEqual(-1, picker.Pick(new List<MoveOption>(), 1f));
            Assert.AreEqual(-1, picker.Pick(new List<MoveOption> { new MoveOption(0, 0f, 0f) }, 1f));
        }

        [Test]
        public void Range_IsInclusive_AndRepeatable()
        {
            var a = new BossMovePicker(5u);
            var b = new BossMovePicker(5u);
            for (int i = 0; i < 200; i++)
            {
                int x = a.Range(30, 50);
                Assert.That(x, Is.InRange(30, 50));
                Assert.AreEqual(x, b.Range(30, 50), "same seed, same numbers");
            }
            Assert.AreEqual(7, a.Range(7, 7));
        }

        [Test]
        public void PhaseFor_ChangesAtHalfHealth()
        {
            var thresholds = new List<float> { 1f, 0.5f };
            Assert.AreEqual(0, BossPhases.PhaseFor(1f, thresholds));
            Assert.AreEqual(0, BossPhases.PhaseFor(0.51f, thresholds));
            Assert.AreEqual(1, BossPhases.PhaseFor(0.5f, thresholds));
            Assert.AreEqual(1, BossPhases.PhaseFor(0.1f, thresholds));
        }

        [Test]
        public void PhaseFor_ThreePhases()
        {
            var thresholds = new List<float> { 1f, 0.5f, 0.2f };
            Assert.AreEqual(1, BossPhases.PhaseFor(0.3f, thresholds));
            Assert.AreEqual(2, BossPhases.PhaseFor(0.2f, thresholds));
        }
    }
}
