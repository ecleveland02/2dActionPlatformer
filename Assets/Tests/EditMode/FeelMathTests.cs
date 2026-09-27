using Margin.FX;
using NUnit.Framework;

namespace Margin.Tests
{
    public class FeelMathTests
    {
        [Test]
        public void DeltaAngle_TakesShortestWay()
        {
            Assert.AreEqual(20f, FeelMath.DeltaAngle(350f, 10f), 0.001f);
            Assert.AreEqual(-135f, FeelMath.DeltaAngle(230f, 95f), 0.001f);
        }

        [Test]
        public void ShakeFalloff_FullAtStart_ZeroAtEnd_Quadratic()
        {
            Assert.AreEqual(1f, FeelMath.ShakeFalloff(12f, 12f), 0.001f);
            Assert.AreEqual(0.25f, FeelMath.ShakeFalloff(6f, 12f), 0.001f);
            Assert.AreEqual(0f, FeelMath.ShakeFalloff(0f, 12f), 0.001f);
        }

        [Test]
        public void SmearInnerRadius_ThinAtOldEdge_ThickAtNewEdge()
        {
            Assert.AreEqual(0.92f, FeelMath.SmearInnerRadius(1f, 0.35f, 0f), 0.001f);
            Assert.AreEqual(0.35f, FeelMath.SmearInnerRadius(1f, 0.35f, 1f), 0.001f);
        }

        [Test]
        public void SmearSegments_ScaleWithSweep_AndClamp()
        {
            Assert.AreEqual(3, FeelMath.SmearSegments(5f));
            Assert.AreEqual(17, FeelMath.SmearSegments(-135f));
            Assert.AreEqual(24, FeelMath.SmearSegments(300f));
        }

        [Test]
        public void SplatterCount_GrowsWithDamage()
        {
            Assert.AreEqual(13, FeelMath.SplatterCount(8, 0.6f, 8));
            Assert.AreEqual(21, FeelMath.SplatterCount(8, 0.6f, 22));
        }
    }
}
