using Margin.Rendering;
using NUnit.Framework;

namespace Margin.Tests
{
    /// <summary>PoseEasing.Flow: monotone cubic through keys (flows, never overshoots).</summary>
    public class FlowEasingTests
    {
        [Test]
        public void MonotoneValue_HitsBothKeys()
        {
            Assert.AreEqual(10f, FigurePose.MonotoneValue(5f, 10f, 20f, 5f, 0f), 1e-4f);
            Assert.AreEqual(30f, FigurePose.MonotoneValue(5f, 10f, 20f, 5f, 1f), 1e-4f);
        }

        [Test]
        public void MonotoneValue_NeverOvershoots_EvenWithWildNeighbours()
        {
            // Steps of +100 before and after a +10 step: Catmull-Rom would bulge past the keys; Flow must not.
            for (int i = 0; i <= 20; i++)
            {
                float v = FigurePose.MonotoneValue(100f, 0f, 10f, 100f, i / 20f);
                Assert.That(v, Is.InRange(-1e-4f, 10f + 1e-4f));
            }
        }

        [Test]
        public void MonotoneValue_KeepsMoving_ThroughAKeyInTheSameDirection()
        {
            // 0 -> 10 -> 20: at the key (t = 0 of the second segment) the speed isn't zero, so there's no stop.
            float justAfter = FigurePose.MonotoneValue(10f, 10f, 10f, 10f, 0.05f);
            Assert.Greater(justAfter - 10f, 0.3f);
        }

        [Test]
        public void MonotoneValue_EasesOnlyWhereTheMotionReverses()
        {
            // 0 -> 10 -> 0: the tangent at the peak is 0 (a natural turn-around), so it doesn't pass 10.
            for (int i = 0; i <= 20; i++)
                Assert.LessOrEqual(FigurePose.MonotoneValue(10f, 10f, -10f, -10f, i / 20f), 10f + 1e-4f);
        }

        [Test]
        public void Monotone_Angles_TakeTheShortWayAcross180()
        {
            var a = new FigurePose { shoulderFront = 170f };
            var b = new FigurePose { shoulderFront = -170f };
            FigurePose mid = FigurePose.Monotone(a, a, b, b, 0.5f);
            Assert.AreEqual(180f, System.Math.Abs(mid.shoulderFront), 0.5f);
        }

        [Test]
        public void Timeline_FlowEntries_PassThroughEveryKey()
        {
            var poses = new[] { new FigurePose { hipFront = 0f }, new FigurePose { hipFront = 30f }, new FigurePose { hipFront = 90f } };
            var t = new PoseTimeline(poses, new[] { 4, 4, 1 }, new[] { PoseEasing.Flow, PoseEasing.Flow, PoseEasing.Linear }, false);
            Assert.AreEqual(30f, t.Sample(4).hipFront, 1e-3f);
            Assert.AreEqual(90f, t.Sample(8).hipFront, 1e-3f);
            for (int f = 0; f <= 8; f++) Assert.That(t.Sample(f).hipFront, Is.InRange(-1e-3f, 90f + 1e-3f));
        }
    }
}
