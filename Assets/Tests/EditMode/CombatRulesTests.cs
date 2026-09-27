using Margin.Combat;
using NUnit.Framework;

namespace Margin.Tests
{
    public class CombatRulesTests
    {
        // Spec 6.2 example: Brush Katana Light 1.
        private static readonly AttackTiming Light1 = new AttackTiming(startup: 4, active: 3, recovery: 10, cancelStart: 9, cancelEnd: 17);
        // Same move, but a miss can still chain into a follow-up 4 frames after the window opens.
        private static readonly AttackTiming Light1Chaining = new AttackTiming(4, 3, 10, 9, 17, whiffChainDelay: 4);

        [Test]
        public void Phases_FollowFrameData()
        {
            Assert.AreEqual(17, Light1.TotalFrames);
            Assert.AreEqual(AttackPhase.Startup, Light1.PhaseAt(1));
            Assert.AreEqual(AttackPhase.Startup, Light1.PhaseAt(4));
            Assert.AreEqual(AttackPhase.Active, Light1.PhaseAt(5));
            Assert.AreEqual(AttackPhase.Active, Light1.PhaseAt(7));
            Assert.AreEqual(AttackPhase.Recovery, Light1.PhaseAt(8));
            Assert.AreEqual(AttackPhase.Recovery, Light1.PhaseAt(17));
            Assert.AreEqual(AttackPhase.Finished, Light1.PhaseAt(18));
        }

        [TestCase(8, false)]
        [TestCase(9, true)]
        [TestCase(17, true)]
        [TestCase(18, false)]
        public void AttackCancel_OnHit_OnlyInsideWindow(int frame, bool expected)
        {
            Assert.AreEqual(expected, Light1.AllowsAttackCancel(frame, hasHit: true));
        }

        [TestCase(5)]
        [TestCase(9)]
        [TestCase(17)]
        public void AttackCancel_OnWhiff_NeverAllowed_WhenChainingDisabled(int frame)
        {
            Assert.IsFalse(Light1.AllowsAttackCancel(frame, hasHit: false));
        }

        [TestCase(9, false)]
        [TestCase(12, false)]
        [TestCase(13, true)]
        [TestCase(17, true)]
        [TestCase(18, false)]
        public void AttackCancel_OnWhiff_ChainsAfterDelay(int frame, bool expected)
        {
            Assert.AreEqual(expected, Light1Chaining.AllowsAttackCancel(frame, hasHit: false));
        }

        [Test]
        public void AttackCancel_OnHit_UnaffectedByWhiffDelay()
        {
            Assert.IsTrue(Light1Chaining.AllowsAttackCancel(9, hasHit: true));
        }

        [TestCase(8, false)]
        [TestCase(9, true)]
        [TestCase(17, true)]
        public void MovementCancel_OnWhiff_FromWindowStart(int frame, bool expected)
        {
            Assert.AreEqual(expected, Light1.AllowsMovementCancel(frame, hasHit: false));
        }

        [Test]
        public void MovementCancel_OnHit_InsideWindow()
        {
            Assert.IsFalse(Light1.AllowsMovementCancel(8, hasHit: true));
            Assert.IsTrue(Light1.AllowsMovementCancel(9, hasHit: true));
        }

        [Test]
        public void Hitbox_MirrorsWhenFacingLeft()
        {
            AabbBox right = HitboxMath.ToWorld(10f, 0f, 1, 0.9f, 0.3f, 1.2f, 0.8f);
            AabbBox left = HitboxMath.ToWorld(10f, 0f, -1, 0.9f, 0.3f, 1.2f, 0.8f);
            Assert.AreEqual(10.9f, right.CenterX, 0.0001f);
            Assert.AreEqual(9.1f, left.CenterX, 0.0001f);
            Assert.AreEqual(0.3f, left.CenterY, 0.0001f);
        }

        [Test]
        public void Overlaps_TrueWhenIntersecting_FalseWhenOnlyTouching()
        {
            var a = new AabbBox(0f, 0f, 2f, 2f);
            Assert.IsTrue(HitboxMath.Overlaps(a, new AabbBox(1.5f, 0f, 2f, 2f)));
            Assert.IsFalse(HitboxMath.Overlaps(a, new AabbBox(2f, 0f, 2f, 2f)), "Touching edges is not a hit.");
            Assert.IsFalse(HitboxMath.Overlaps(a, new AabbBox(0f, 3f, 2f, 2f)));
        }

        [Test]
        public void OverlapCenter_IsMiddleOfIntersection()
        {
            HitboxMath.OverlapCenter(new AabbBox(0f, 0f, 2f, 2f), new AabbBox(1.5f, 0.5f, 2f, 2f), out float x, out float y);
            Assert.AreEqual(0.75f, x, 0.0001f);
            Assert.AreEqual(0.25f, y, 0.0001f);
        }
    }
}
