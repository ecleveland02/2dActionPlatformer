using Margin.Combat;
using NUnit.Framework;

namespace Margin.Tests
{
    public class InkMeterTests
    {
        [Test]
        public void Gain_ClampsToMax()
        {
            var ink = new InkMeter();
            ink.Gain(70);
            ink.Gain(70);
            Assert.AreEqual(100, ink.Value);
        }

        [Test]
        public void Spend_OnlyWhenEnough()
        {
            var ink = new InkMeter();
            ink.Gain(40);
            Assert.IsFalse(ink.TrySpend(50));
            Assert.AreEqual(40, ink.Value);
            ink.Gain(10);
            Assert.IsTrue(ink.TrySpend(50));
            Assert.AreEqual(0, ink.Value);
        }

        [Test]
        public void Decay_StartsAfterFiveSeconds_OneInkPerSecond()
        {
            var ink = new InkMeter();
            ink.Gain(10);
            for (int i = 0; i < 300; i++) ink.Tick();
            Assert.AreEqual(10, ink.Value, "No decay during the first 300 frames.");
            for (int i = 0; i < 60; i++) ink.Tick();
            Assert.AreEqual(9, ink.Value, "1 ink after the next 60 frames.");
            for (int i = 0; i < 120; i++) ink.Tick();
            Assert.AreEqual(7, ink.Value);
        }

        [Test]
        public void Hitting_ResetsTheDecayTimer()
        {
            var ink = new InkMeter();
            ink.Gain(10);
            for (int i = 0; i < 290; i++) ink.Tick();
            ink.Gain(5);
            for (int i = 0; i < 300; i++) ink.Tick();
            Assert.AreEqual(15, ink.Value);
        }

        [Test]
        public void Decay_StopsAtZero()
        {
            var ink = new InkMeter(decayDelayFrames: 0, decayIntervalFrames: 1);
            ink.Gain(3);
            for (int i = 0; i < 10; i++) ink.Tick();
            Assert.AreEqual(0, ink.Value);
        }
    }
}
