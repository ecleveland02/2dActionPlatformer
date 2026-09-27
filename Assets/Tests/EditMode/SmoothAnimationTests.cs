using System.Collections.Generic;
using Margin.Rendering;
using NUnit.Framework;

namespace Margin.Tests
{
    /// <summary>Curved (Smooth) blending between keyframes and SecondaryMotion follow-through.</summary>
    public class SmoothAnimationTests
    {
        private static FigurePose Spine(float degrees) => new FigurePose { spine = degrees };

        private static PoseTimeline Cycle(params float[] spines)
        {
            var poses = new List<FigurePose>();
            var frames = new List<int>();
            var easings = new List<PoseEasing>();
            foreach (float s in spines)
            {
                poses.Add(Spine(s));
                frames.Add(4);
                easings.Add(PoseEasing.Smooth);
            }
            return new PoseTimeline(poses, frames, easings, loop: true);
        }

        [Test]
        public void Smooth_PassesExactlyThroughEveryKeyframe()
        {
            PoseTimeline t = Cycle(0f, 30f, 10f, -20f);
            Assert.AreEqual(0f, t.Sample(0).spine, 1e-4f);
            Assert.AreEqual(30f, t.Sample(4).spine, 1e-4f);
            Assert.AreEqual(10f, t.Sample(8).spine, 1e-4f);
            Assert.AreEqual(-20f, t.Sample(12).spine, 1e-4f);
        }

        [Test]
        public void Smooth_OnEvenlySpacedKeys_MatchesAStraightLine()
        {
            // With keys 0, 10, 20, 30 the curve through them is the line itself.
            PoseTimeline t = new PoseTimeline(
                new[] { Spine(0f), Spine(10f), Spine(20f), Spine(30f) }, new[] { 4, 4, 4, 4 },
                new[] { PoseEasing.Smooth, PoseEasing.Smooth, PoseEasing.Smooth, PoseEasing.Smooth }, loop: false);
            Assert.AreEqual(15f, t.Sample(6).spine, 1e-3f);
        }

        [Test]
        public void Smooth_HasNoSuddenSpeedChangeAtAKey()
        {
            // At the key 30 (between 0 and 10) the motion reverses. Straight lines jump from +7.5 to -5 degrees
            // per tick there (12.5); the curve eases through the turn.
            PoseTimeline t = Cycle(0f, 30f, 10f, -20f);
            float before = t.Sample(4).spine - t.Sample(3).spine;
            float after = t.Sample(5).spine - t.Sample(4).spine;
            float linearJump = 7.5f - (-5f);
            Assert.Less(System.Math.Abs(before - after), linearJump * 0.5f);
        }

        [Test]
        public void Smooth_LoopsBackIntoTheFirstKey()
        {
            PoseTimeline t = Cycle(0f, 30f, 10f, -20f);
            Assert.AreEqual(t.Sample(0).spine, t.Sample(16).spine, 1e-4f);
            float last = t.Sample(15).spine;
            Assert.Greater(last, -20f, "Heading from -20 back up toward 0.");
            Assert.Less(last, 0f);
        }

        [Test]
        public void CatmullRom_TakesTheShortWayAcross180()
        {
            FigurePose mid = FigurePose.CatmullRom(Spine(160f), Spine(170f), Spine(-170f), Spine(-160f), 0.5f);
            Assert.AreEqual(180f, System.Math.Abs(mid.spine), 0.5f);
        }

        private static readonly float[] NeckOnly = Weights(PoseJoint.Neck);

        private static float[] Weights(PoseJoint sprung)
        {
            var w = new float[FigurePose.AllJoints.Length];
            for (int i = 0; i < w.Length; i++) w[i] = FigurePose.AllJoints[i] == sprung ? 1f : 0f;
            return w;
        }

        [Test]
        public void SecondaryMotion_UnweightedJointsAreExact()
        {
            var motion = new SecondaryMotion();
            motion.Reset(FigurePose.Neutral);
            FigurePose target = new FigurePose { neck = 40f, shoulderFront = 90f };
            FigurePose result = motion.Step(target, NeckOnly, 5f, 0.5f, 1f / 60f);
            Assert.AreEqual(90f, result.shoulderFront, "The sword arm is not sprung.");
            Assert.Less(result.neck, 40f, "The head lags behind.");
        }

        [Test]
        public void SecondaryMotion_OvershootsThenSettles()
        {
            var motion = new SecondaryMotion();
            motion.Reset(FigurePose.Neutral);
            var target = new FigurePose { neck = 40f };
            float peak = 0f, value = 0f;
            for (int i = 0; i < 180; i++)
            {
                value = motion.Step(target, NeckOnly, 5f, 0.5f, 1f / 60f).neck;
                peak = System.Math.Max(peak, value);
            }
            Assert.Greater(peak, 40f, "Damping 0.5 overshoots.");
            Assert.AreEqual(40f, value, 0.1f, "Settled after 3 seconds.");
        }

        [Test]
        public void SecondaryMotion_CriticalDampingDoesNotOvershoot()
        {
            var motion = new SecondaryMotion();
            motion.Reset(FigurePose.Neutral);
            var target = new FigurePose { neck = 40f };
            float peak = 0f;
            for (int i = 0; i < 180; i++) peak = System.Math.Max(peak, motion.Step(target, NeckOnly, 5f, 1f, 1f / 60f).neck);
            Assert.LessOrEqual(peak, 40.01f);
        }

        [Test]
        public void SecondaryMotion_WrapsAcross180()
        {
            var motion = new SecondaryMotion();
            motion.Reset(new FigurePose { neck = 170f });
            float neck = motion.Step(new FigurePose { neck = -170f }, NeckOnly, 5f, 1f, 1f / 60f).neck;
            Assert.IsTrue(neck > 170f || neck < -170f, $"Moved toward 180, not back through 0 (got {neck}).");
        }
    }
}
