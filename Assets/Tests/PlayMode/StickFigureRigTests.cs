using Margin.Rendering;
using NUnit.Framework;
using UnityEngine;

namespace Margin.Tests
{
    /// <summary>
    /// Checks the angle convention with forward kinematics on the default proportions
    /// (thigh 0.4, shin 0.4, spine 0.55, upper arm 0.3, forearm 0.28, feet 0.9 below origin).
    /// </summary>
    public class StickFigureRigTests
    {
        private const float Tol = 0.002f;
        private GameObject go;
        private StickFigureRig rig;

        [SetUp]
        public void SetUp()
        {
            go = new GameObject("TestRig");
            rig = go.AddComponent<StickFigureRig>();   // builds itself in OnEnable
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(go);

        private static FigurePose With(PoseJoint joint, float degrees)
        {
            FigurePose p = FigurePose.Neutral;
            p.Set(joint, degrees);
            return p;
        }

        [Test]
        public void Neutral_FeetOnGroundLine_HandsHangBelowShoulders()
        {
            rig.ApplyPose(FigurePose.Neutral);
            Assert.AreEqual(-0.9f, rig.FootPosition(true).y, Tol);
            Assert.AreEqual(0f, rig.FootPosition(true).x, Tol);
            Assert.AreEqual(rig.ChestPosition.y - 0.58f, rig.HandPosition(true).y, Tol);
            Assert.AreEqual(rig.ChestPosition.x, rig.HandPosition(false).x, Tol);
        }

        [Test]
        public void ShoulderPlus90_PointsArmForward()
        {
            rig.ApplyPose(With(PoseJoint.ShoulderFront, 90f));
            Vector3 chest = rig.ChestPosition;
            Assert.AreEqual(chest.x + 0.58f, rig.HandPosition(true).x, Tol);
            Assert.AreEqual(chest.y, rig.HandPosition(true).y, Tol);
        }

        [Test]
        public void KneeMinus90_BendsShinBackward()
        {
            rig.ApplyPose(With(PoseJoint.KneeFront, -90f));
            Vector3 hips = rig.HipsPosition;
            Assert.AreEqual(hips.x - 0.4f, rig.FootPosition(true).x, Tol);
            Assert.AreEqual(hips.y - 0.4f, rig.FootPosition(true).y, Tol);
        }

        [Test]
        public void SpinePositive_LeansForward()
        {
            rig.ApplyPose(With(PoseJoint.Spine, 20f));
            Assert.Greater(rig.ChestPosition.x, 0.1f);
        }

        [Test]
        public void RootOffset_MovesHips()
        {
            FigurePose crouch = FigurePose.Neutral;
            crouch.rootOffsetY = -0.25f;
            rig.ApplyPose(crouch);
            Assert.AreEqual(-0.1f - 0.25f, rig.HipsPosition.y, Tol);
        }

        [Test]
        public void FlippedRig_MirrorsWorldPositions()
        {
            go.transform.localScale = new Vector3(-1f, 1f, 1f);
            rig.ApplyPose(With(PoseJoint.ShoulderFront, 90f));
            Assert.Less(rig.HandPosition(true).x, -0.5f, "Facing left: forward is -x.");
        }

        [Test]
        public void CapturePose_ReturnsWhatWasApplied()
        {
            var pose = new FigurePose
            {
                rootOffsetX = 0.05f, rootOffsetY = -0.1f, spine = 12, neck = -8,
                shoulderFront = 150, elbowFront = 20, shoulderBack = -95, elbowBack = 15,
                hipFront = 45, kneeFront = -80, hipBack = -25, kneeBack = -35,
            };
            rig.ApplyPose(pose);
            FigurePose back = rig.CapturePose();
            foreach (PoseJoint joint in FigurePose.AllJoints)
                Assert.AreEqual(pose.Get(joint), back.Get(joint), 0.01f, joint.ToString());
            Assert.AreEqual(pose.rootOffsetY, back.rootOffsetY, 0.0001f);
        }
    }
}
