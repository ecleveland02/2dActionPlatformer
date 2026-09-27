using Margin.Rendering;
using NUnit.Framework;

namespace Margin.Tests
{
    public class PoseTimelineTests
    {
        private static FigurePose Arm(float degrees) => new FigurePose { shoulderFront = degrees };

        private static PoseTimeline Timeline(bool loop, PoseEasing easing, params (float angle, int frames)[] entries)
        {
            var poses = new FigurePose[entries.Length];
            var frames = new int[entries.Length];
            var easings = new PoseEasing[entries.Length];
            for (int i = 0; i < entries.Length; i++)
            {
                poses[i] = Arm(entries[i].angle);
                frames[i] = entries[i].frames;
                easings[i] = easing;
            }
            return new PoseTimeline(poses, frames, easings, loop);
        }

        [Test]
        public void TotalFrames_IsSumOfEntries()
        {
            Assert.AreEqual(30, Timeline(true, PoseEasing.Linear, (0, 5), (10, 5), (20, 20)).TotalFrames);
        }

        [Test]
        public void Linear_BlendsTowardNextEntry()
        {
            PoseTimeline t = Timeline(false, PoseEasing.Linear, (0, 4), (40, 4));
            Assert.AreEqual(0f, t.Sample(0).shoulderFront, 0.001f);
            Assert.AreEqual(10f, t.Sample(1).shoulderFront, 0.001f);
            Assert.AreEqual(30f, t.Sample(3).shoulderFront, 0.001f);
            Assert.AreEqual(40f, t.Sample(4).shoulderFront, 0.001f);
        }

        [Test]
        public void Snap_HoldsThenJumps()
        {
            PoseTimeline t = Timeline(false, PoseEasing.Snap, (0, 4), (40, 4));
            Assert.AreEqual(0f, t.Sample(3).shoulderFront, 0.001f);
            Assert.AreEqual(40f, t.Sample(4).shoulderFront, 0.001f);
        }

        [Test]
        public void Loop_WrapsAndBlendsLastBackIntoFirst()
        {
            PoseTimeline t = Timeline(true, PoseEasing.Linear, (0, 2), (20, 2));
            Assert.AreEqual(10f, t.Sample(3).shoulderFront, 0.001f, "Last entry blends back toward the first.");
            Assert.AreEqual(t.Sample(1).shoulderFront, t.Sample(5).shoulderFront, 0.001f, "Tick 5 wraps to tick 1.");
        }

        [Test]
        public void OneShot_HoldsLastPoseAfterEnd()
        {
            PoseTimeline t = Timeline(false, PoseEasing.Linear, (0, 2), (20, 2));
            Assert.AreEqual(20f, t.Sample(3).shoulderFront, 0.001f, "Last entry has nothing to blend toward.");
            Assert.AreEqual(20f, t.Sample(500).shoulderFront, 0.001f);
        }

        [Test]
        public void Easing_CurvesHaveExpectedShape()
        {
            Assert.AreEqual(0.25f, PoseEasingMath.Apply(PoseEasing.EaseIn, 0.5f), 0.001f);
            Assert.AreEqual(0.75f, PoseEasingMath.Apply(PoseEasing.EaseOut, 0.5f), 0.001f);
            Assert.AreEqual(0.5f, PoseEasingMath.Apply(PoseEasing.EaseInOut, 0.5f), 0.001f);
            Assert.AreEqual(1f, PoseEasingMath.Apply(PoseEasing.Snap, 1f), 0.001f);
        }

        [Test]
        public void Additive_AddsTheDifferenceScaledByWeight()
        {
            FigurePose baseline = Arm(10), breath = Arm(16), running = Arm(40);
            FigurePose delta = FigurePose.Subtract(breath, baseline);
            Assert.AreEqual(6f, delta.shoulderFront, 0.001f);
            Assert.AreEqual(43f, FigurePose.Add(running, delta, 0.5f).shoulderFront, 0.001f);
        }

        [Test]
        public void Subtract_UsesShortestAngle()
        {
            Assert.AreEqual(20f, FigurePose.Subtract(Arm(10), Arm(350)).shoulderFront, 0.001f);
        }
    }
}
