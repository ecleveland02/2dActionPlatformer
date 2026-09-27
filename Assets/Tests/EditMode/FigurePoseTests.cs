using Margin.Rendering;
using NUnit.Framework;

namespace Margin.Tests
{
    public class FigurePoseTests
    {
        private static FigurePose Sample() => new FigurePose
        {
            rootOffsetX = 0.1f, rootOffsetY = -0.2f,
            spine = 10, neck = -5,
            shoulderFront = 30, elbowFront = 40, shoulderBack = -20, elbowBack = 15,
            hipFront = 25, kneeFront = -60, hipBack = -35, kneeBack = -10,
        };

        [Test]
        public void GetSet_RoundTripsEveryJoint()
        {
            var pose = new FigurePose();
            float value = 1f;
            foreach (PoseJoint joint in FigurePose.AllJoints) pose.Set(joint, value++);

            value = 1f;
            foreach (PoseJoint joint in FigurePose.AllJoints) Assert.AreEqual(value++, pose.Get(joint), joint.ToString());
        }

        [Test]
        public void Lerp_EndpointsAreExact()
        {
            FigurePose a = FigurePose.Neutral, b = Sample();
            Assert.AreEqual(a, FigurePose.Lerp(a, b, 0f));
            Assert.AreEqual(b, FigurePose.Lerp(a, b, 1f));
        }

        [Test]
        public void Lerp_Midpoint_BlendsAnglesAndOffset()
        {
            FigurePose mid = FigurePose.Lerp(FigurePose.Neutral, Sample(), 0.5f);
            Assert.AreEqual(15f, mid.shoulderFront, 0.001f);
            Assert.AreEqual(-30f, mid.kneeFront, 0.001f);
            Assert.AreEqual(-0.1f, mid.rootOffsetY, 0.001f);
        }

        [Test]
        public void LerpAngle_TakesTheShortWayAround()
        {
            Assert.AreEqual(0f, FigurePose.LerpAngle(350f, 10f, 0.5f), 0.001f);
            Assert.AreEqual(-180f, FigurePose.LerpAngle(170f, -170f, 0.5f), 0.001f);
            Assert.AreEqual(45f, FigurePose.LerpAngle(0f, 90f, 0.5f), 0.001f);
        }

        [Test]
        public void NormalizeAngle_WrapsIntoHalfOpenRange()
        {
            Assert.AreEqual(-160f, FigurePose.NormalizeAngle(200f), 0.001f);
            Assert.AreEqual(10f, FigurePose.NormalizeAngle(370f), 0.001f);
            Assert.AreEqual(-180f, FigurePose.NormalizeAngle(180f), 0.001f);
        }

        [Test]
        public void Mirrored_SwapsLimbs_KeepsBodyAndOffset()
        {
            FigurePose p = Sample();
            FigurePose m = p.Mirrored();
            Assert.AreEqual(p.shoulderBack, m.shoulderFront);
            Assert.AreEqual(p.kneeFront, m.kneeBack);
            Assert.AreEqual(p.spine, m.spine);
            Assert.AreEqual(p.rootOffsetY, m.rootOffsetY);
            Assert.AreEqual(p, m.Mirrored(), "Mirroring twice gives the original.");
        }
    }
}
