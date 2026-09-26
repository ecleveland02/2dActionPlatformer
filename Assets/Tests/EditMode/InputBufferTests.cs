using System;
using Margin.Core;
using Margin.Input;
using NUnit.Framework;

namespace Margin.Tests
{
    public class InputBufferTests
    {
        private FrameCounter clock;
        private InputBuffer buffer;

        [SetUp]
        public void SetUp()
        {
            clock = new FrameCounter();
            buffer = new InputBuffer(clock);
        }

        // Advances the clock by a number of ticks.
        private void Tick(int frames)
        {
            for (int i = 0; i < frames; i++) clock.Advance();
        }

        [Test]
        public void NothingPressed_IsNotBuffered()
        {
            Assert.IsFalse(buffer.IsBuffered(BufferedAction.Jump));
            Assert.IsFalse(buffer.Consume(BufferedAction.Jump));
            Assert.AreEqual(-1, buffer.FramesSincePress(BufferedAction.Jump));
        }

        [Test]
        public void PressThisTick_CanBeConsumedThisTick()
        {
            buffer.Record(BufferedAction.Jump);
            Assert.IsTrue(buffer.Consume(BufferedAction.Jump));
        }

        [Test]
        public void DefaultWindow_IsSixFrames()
        {
            Assert.AreEqual(6, buffer.GetWindow(BufferedAction.Jump));
            Assert.AreEqual(6, buffer.GetWindow(BufferedAction.Parry));
        }

        // Press tick is frame 1. With the default 6 frame window, frames 1 to 6 succeed and frame 7 fails.
        [TestCase(1, true)]
        [TestCase(2, true)]
        [TestCase(5, true)]
        [TestCase(6, true)]
        [TestCase(7, false)]
        [TestCase(30, false)]
        public void DefaultWindow_Boundary(int frameOfWindow, bool expected)
        {
            buffer.Record(BufferedAction.Jump);
            Tick(frameOfWindow - 1);
            Assert.AreEqual(expected, buffer.Consume(BufferedAction.Jump));
        }

        [Test]
        public void Consume_UsesUpThePress()
        {
            buffer.Record(BufferedAction.Jump);
            Assert.IsTrue(buffer.Consume(BufferedAction.Jump));
            Assert.IsFalse(buffer.Consume(BufferedAction.Jump), "One press must not trigger two jumps.");

            Tick(1);
            Assert.IsFalse(buffer.Consume(BufferedAction.Jump));
        }

        [Test]
        public void IsBuffered_DoesNotConsume()
        {
            buffer.Record(BufferedAction.Dash);
            Assert.IsTrue(buffer.IsBuffered(BufferedAction.Dash));
            Assert.IsTrue(buffer.IsBuffered(BufferedAction.Dash));
            Assert.IsTrue(buffer.Consume(BufferedAction.Dash));
        }

        [Test]
        public void NewerPress_RestartsTheWindow()
        {
            buffer.Record(BufferedAction.Jump);
            Tick(4);
            buffer.Record(BufferedAction.Jump);
            Tick(5); // 9 frames after the first press, 5 after the second (frame 6 of its window)
            Assert.IsTrue(buffer.Consume(BufferedAction.Jump));
        }

        [Test]
        public void PressAgainAfterConsume_IsBufferedAgain()
        {
            buffer.Record(BufferedAction.LightAttack);
            Assert.IsTrue(buffer.Consume(BufferedAction.LightAttack));
            Tick(1);
            buffer.Record(BufferedAction.LightAttack);
            Assert.IsTrue(buffer.Consume(BufferedAction.LightAttack));
        }

        [Test]
        public void Actions_AreIndependent()
        {
            buffer.Record(BufferedAction.Jump);
            Assert.IsFalse(buffer.IsBuffered(BufferedAction.LightAttack));

            buffer.Record(BufferedAction.LightAttack);
            Assert.IsTrue(buffer.Consume(BufferedAction.Jump));
            Assert.IsTrue(buffer.IsBuffered(BufferedAction.LightAttack), "Consuming Jump must not touch LightAttack.");
        }

        [Test]
        public void PerActionWindow_IsRespected()
        {
            buffer.SetWindow(BufferedAction.Parry, 3);
            buffer.Record(BufferedAction.Parry);
            buffer.Record(BufferedAction.Jump);
            Tick(3); // frame 4 of the window

            Assert.IsFalse(buffer.IsBuffered(BufferedAction.Parry));
            Assert.IsTrue(buffer.IsBuffered(BufferedAction.Jump), "Jump keeps the default 6 frames.");
        }

        [Test]
        public void ExplicitWithinFrames_OverridesConfiguredWindow()
        {
            buffer.Record(BufferedAction.Jump);
            Tick(2); // frame 3 of the window

            Assert.IsFalse(buffer.Consume(BufferedAction.Jump, withinFrames: 2));
            Assert.IsTrue(buffer.Consume(BufferedAction.Jump, withinFrames: 3));
        }

        [Test]
        public void FailedConsume_DoesNotClearAFreshPressForLaterChecks()
        {
            // A state asking with a tight window must not destroy the press for a state with a wider one.
            buffer.Record(BufferedAction.Jump);
            Tick(3);
            Assert.IsFalse(buffer.Consume(BufferedAction.Jump, withinFrames: 2));
            Assert.IsTrue(buffer.Consume(BufferedAction.Jump));
        }

        [Test]
        public void Clear_And_ClearAll()
        {
            buffer.Record(BufferedAction.Jump);
            buffer.Record(BufferedAction.Dash);
            buffer.Record(BufferedAction.Parry);

            buffer.Clear(BufferedAction.Jump);
            Assert.IsFalse(buffer.IsBuffered(BufferedAction.Jump));
            Assert.IsTrue(buffer.IsBuffered(BufferedAction.Dash));

            buffer.ClearAll();
            Assert.IsFalse(buffer.IsBuffered(BufferedAction.Dash));
            Assert.IsFalse(buffer.IsBuffered(BufferedAction.Parry));
        }

        [Test]
        public void FramesSincePress_ReportsAge_IncludingStalePresses()
        {
            buffer.Record(BufferedAction.HeavyAttack);
            Assert.AreEqual(0, buffer.FramesSincePress(BufferedAction.HeavyAttack));
            Tick(10);
            Assert.AreEqual(10, buffer.FramesSincePress(BufferedAction.HeavyAttack));
            Assert.IsFalse(buffer.IsBuffered(BufferedAction.HeavyAttack));
        }

        [Test]
        public void ClockReset_DropsOldPresses()
        {
            Tick(50);
            buffer.Record(BufferedAction.Jump);
            clock.Reset();
            Assert.IsFalse(buffer.IsBuffered(BufferedAction.Jump));
            Assert.AreEqual(-1, buffer.FramesSincePress(BufferedAction.Jump));
        }

        [Test]
        public void InvalidWindows_Throw()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => buffer.SetWindow(BufferedAction.Jump, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new InputBuffer(clock, 0));
            Assert.Throws<ArgumentNullException>(() => new InputBuffer(null));
        }

        [Test]
        public void EveryActionCanBeBuffered()
        {
            foreach (BufferedAction action in Enum.GetValues(typeof(BufferedAction)))
            {
                buffer.Record(action);
                Assert.IsTrue(buffer.Consume(action), action.ToString());
            }
        }
    }
}
