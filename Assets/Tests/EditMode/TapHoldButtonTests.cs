using Margin.Input;
using NUnit.Framework;

namespace Margin.Tests
{
    public class TapHoldButtonTests
    {
        private TapHoldButton button;

        [SetUp]
        public void SetUp() => button = new TapHoldButton(holdFrames: 10);

        [Test]
        public void QuickTap_IsATap_OnTheReleaseTick()
        {
            button.QueuePress();
            Assert.AreEqual(TapHoldResult.None, button.Tick());
            Assert.IsTrue(button.Pending);
            Assert.AreEqual(TapHoldResult.None, button.Tick());
            button.QueueRelease();
            Assert.AreEqual(TapHoldResult.Tap, button.Tick());
            Assert.IsFalse(button.Pending);
        }

        [Test]
        public void PressAndReleaseBetweenTwoTicks_StillCountsAsTap()
        {
            button.QueuePress();
            button.QueueRelease();
            Assert.AreEqual(TapHoldResult.Tap, button.Tick());
        }

        [Test]
        public void Hold_FiresOnExactlyTheHoldFrame()
        {
            button.QueuePress();
            for (int frame = 1; frame < 10; frame++)
                Assert.AreEqual(TapHoldResult.None, button.Tick(), $"frame {frame}");
            Assert.AreEqual(TapHoldResult.Hold, button.Tick(), "frame 10");
            Assert.IsFalse(button.Pending, "Resolved once the hold fires.");
        }

        [Test]
        public void ReleaseAfterHold_DoesNotAlsoTap()
        {
            button.QueuePress();
            for (int i = 0; i < 10; i++) button.Tick();
            button.QueueRelease();
            Assert.AreEqual(TapHoldResult.None, button.Tick());
        }

        [Test]
        public void ReleaseOnFrameNine_IsStillATap()
        {
            button.QueuePress();
            for (int i = 0; i < 8; i++) button.Tick();   // frames 1-8 held
            button.QueueRelease();
            Assert.AreEqual(TapHoldResult.Tap, button.Tick());
        }

        [Test]
        public void RapidTaps_EachCount()
        {
            for (int i = 0; i < 3; i++)
            {
                button.QueuePress();
                button.Tick();
                button.QueueRelease();
                Assert.AreEqual(TapHoldResult.Tap, button.Tick(), $"tap {i + 1}");
            }
        }

        [Test]
        public void ReleaseThenNewPressInOneTick_TapsAndStartsNewPress()
        {
            button.QueuePress();
            button.Tick();
            button.QueueRelease();
            button.QueuePress();
            Assert.AreEqual(TapHoldResult.Tap, button.Tick());
            Assert.IsTrue(button.Pending, "The second press is being held.");
        }
    }
}
