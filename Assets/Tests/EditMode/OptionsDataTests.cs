using Margin.Save;
using NUnit.Framework;

namespace Margin.Tests
{
    /// <summary>The Options menu's values (spec 14): sliders step in clean notches, choices wrap, bad files are clamped.</summary>
    public class OptionsDataTests
    {
        [Test]
        public void Step_LandsExactlyOnNotches_AndStopsAtTheEnds()
        {
            Assert.AreEqual(0.8f, OptionsData.Step(0.7f, 1), 1e-6f);
            Assert.AreEqual(0.6f, OptionsData.Step(0.7f, -1), 1e-6f);
            Assert.AreEqual(1f, OptionsData.Step(1f, 1));
            Assert.AreEqual(0f, OptionsData.Step(0f, -1));
            Assert.AreEqual(0.8f, OptionsData.Step(0.73f, 1), 1e-6f, "an off-notch value snaps to the next notch");
        }

        [Test]
        public void Cycle_WrapsBothWays()
        {
            Assert.AreEqual(0, OptionsData.Cycle(2, 1, 3));
            Assert.AreEqual(2, OptionsData.Cycle(0, -1, 3));
            Assert.AreEqual(0, OptionsData.Cycle(5, 1, 0), "no choices is safe");
        }

        [Test]
        public void Clamp_FixesOutOfRangeValues()
        {
            var o = new OptionsData { masterVolume = 3f, musicVolume = -1f, screenShake = float.NaN, displayMode = (DisplayMode)9, resolutionWidth = -5 };
            o.Clamp();
            Assert.AreEqual(1f, o.masterVolume);
            Assert.AreEqual(0f, o.musicVolume);
            Assert.AreEqual(1f, o.screenShake);
            Assert.AreEqual(DisplayMode.Borderless, o.displayMode);
            Assert.AreEqual(0, o.resolutionWidth);
            Assert.AreEqual(0, o.resolutionHeight);
        }

        [Test]
        public void Percent_AndDefaults()
        {
            Assert.AreEqual("70%", OptionsData.Percent(0.7f));
            var o = new OptionsData();
            Assert.AreEqual(1f, o.screenShake);
            Assert.IsTrue(o.vsync);
            Assert.AreNotSame(o, o.Copy());
        }
    }
}
