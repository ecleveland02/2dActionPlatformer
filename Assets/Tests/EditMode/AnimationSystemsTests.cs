using Margin.Rendering;
using NUnit.Framework;

namespace Margin.Tests
{
    /// <summary>Whole-body tilt, fractional sampling and foot lock (CycleSync).</summary>
    public class AnimationSystemsTests
    {
        [Test]
        public void RootRotation_BlendsTheShortWayAcross180()
        {
            var a = new FigurePose { rootRotation = 170f };
            var b = new FigurePose { rootRotation = -170f };
            Assert.AreEqual(180f, System.Math.Abs(FigurePose.Lerp(a, b, 0.5f).rootRotation), 0.01f);
            Assert.AreEqual(180f, System.Math.Abs(FigurePose.CatmullRom(a, a, b, b, 0.5f).rootRotation), 0.5f);
        }

        [Test]
        public void RootRotation_SubtractAndAddRoundTrip()
        {
            var a = new FigurePose { rootRotation = -90f, spine = 10f };
            var b = new FigurePose { rootRotation = 20f };
            FigurePose delta = FigurePose.Subtract(a, b);
            Assert.AreEqual(-90f, FigurePose.Add(b, delta, 1f).rootRotation, 0.01f);
        }

        [Test]
        public void Sample_FractionalTime_BlendsBetweenTicks()
        {
            var t = new PoseTimeline(new[] { new FigurePose { spine = 0f }, new FigurePose { spine = 40f } },
                                     new[] { 4, 4 }, new[] { PoseEasing.Linear, PoseEasing.Linear }, loop: false);
            Assert.AreEqual(15f, t.Sample(1.5f).spine, 1e-4f);
            Assert.AreEqual(t.Sample(2).spine, t.Sample(2f).spine, 1e-5f, "Int and float sampling agree on whole ticks.");
        }

        [Test]
        public void CycleRate_FollowsSpeed_AndClamps()
        {
            // 9 units/s, 3 units per 32-frame cycle: 9 * 32 / (60 * 3) = 1.6 ticks per tick.
            Assert.AreEqual(1.6f, CycleSync.Rate(9f, 3f, 32, 0.35f, 3f), 1e-4f);
            Assert.AreEqual(1.6f, CycleSync.Rate(-9f, 3f, 32, 0.35f, 3f), 1e-4f, "Direction doesn't matter.");
            Assert.AreEqual(0.35f, CycleSync.Rate(0f, 3f, 32, 0.35f, 3f), 1e-4f);
            Assert.AreEqual(3f, CycleSync.Rate(50f, 3f, 32, 0.35f, 3f), 1e-4f);
            Assert.AreEqual(1f, CycleSync.Rate(9f, 0f, 32, 0.35f, 3f), "No stride length: normal speed.");
        }

        [Test]
        public void FootPosition_UsesLegAnglesAndTilt()
        {
            (float x, float y) = CycleSync.FootPosition(FigurePose.Neutral, true, 0.4f, 0.4f);
            Assert.AreEqual(0f, x, 1e-5f);
            Assert.AreEqual(-0.8f, y, 1e-5f);

            (x, y) = CycleSync.FootPosition(new FigurePose { hipFront = 90f }, true, 0.4f, 0.4f);
            Assert.AreEqual(0.8f, x, 1e-5f, "Hip 90 points the leg forward.");
            Assert.AreEqual(0f, y, 1e-5f);

            (x, y) = CycleSync.FootPosition(new FigurePose { rootRotation = 90f }, false, 0.4f, 0.4f);
            Assert.AreEqual(0.8f, x, 1e-5f, "Tilting the body 90 forward swings the legs too.");
        }

        [Test]
        public void StrideLength_MeasuresPlantedFootTravel()
        {
            // Front leg straight down, sliding from x +0.4 to -0.4 over 10 ticks (planted), then lifted and
            // carried back. Back leg folded up so it's never on the ground. Planted speed 0.08/tick x 20 ticks.
            FigurePose Key(float x, float y) => new FigurePose { rootOffsetX = x, rootOffsetY = y, kneeBack = -90f };
            var cycle = new PoseTimeline(
                new[] { Key(0.4f, 0f), Key(-0.4f, 0f), Key(-0.4f, 0.2f) }, new[] { 10, 1, 9 },
                new[] { PoseEasing.Linear, PoseEasing.Linear, PoseEasing.Linear }, loop: true);
            Assert.AreEqual(1.6f, CycleSync.StrideLength(cycle, 0.4f, 0.4f), 0.01f);
        }
    }
}
