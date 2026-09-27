using Margin.Player;
using NUnit.Framework;

namespace Margin.Tests
{
    public class AirPoseBlendTests
    {
        [Test]
        public void RisingFast_IsPureJump_AndSlowingBlendsTowardApex()
        {
            (bool rising, float w) = AirPoseBlend.Weights(20f, 12f, 10f);
            Assert.IsTrue(rising);
            Assert.AreEqual(0f, w, 1e-5f);
            Assert.Greater(AirPoseBlend.Weights(3f, 12f, 10f).weight, AirPoseBlend.Weights(9f, 12f, 10f).weight);
        }

        [Test]
        public void TopOfTheJump_IsTheApexPoseFromBothSides()
        {
            // Just before the top: nearly all apex. Just after: apex with almost no fall. So no pop at the top.
            (bool up, float wUp) = AirPoseBlend.Weights(0.01f, 12f, 10f);
            (bool down, float wDown) = AirPoseBlend.Weights(-0.01f, 12f, 10f);
            Assert.IsTrue(up);
            Assert.IsFalse(down);
            Assert.AreEqual(1f, wUp, 1e-3f);
            Assert.AreEqual(0f, wDown, 1e-3f);
        }

        [Test]
        public void FallingFast_IsPureFall()
        {
            (bool rising, float w) = AirPoseBlend.Weights(-15f, 12f, 10f);
            Assert.IsFalse(rising);
            Assert.AreEqual(1f, w, 1e-5f);
        }
    }
}
