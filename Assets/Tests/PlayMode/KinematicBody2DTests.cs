using Margin.Physics;
using NUnit.Framework;
using UnityEngine;

namespace Margin.Tests
{
    /// <summary>
    /// Collision tests for KinematicBody2D. Move() is called directly, one call per simulated tick,
    /// so results are deterministic and don't depend on real frame timing.
    /// </summary>
    public class KinematicBody2DTests
    {
        private const float Tolerance = 0.002f;
        private TestWorld world;

        [SetUp]
        public void SetUp() => world = new TestWorld();

        [TearDown]
        public void TearDown() => world.Destroy();

        private static void MoveTicks(KinematicBody2D body, Vector2 delta, int ticks, float corner = 0f)
        {
            for (int i = 0; i < ticks; i++) body.Move(delta, corner);
        }

        [Test]
        public void FallsOntoFlatGround_AndRestsOnIt()
        {
            world.Box(-10, -1, 10, 0);
            KinematicBody2D body = world.Body(new Vector2(0, 2));

            int landings = 0;
            for (int i = 0; i < 30; i++)
            {
                body.Move(new Vector2(0, -0.2f));
                if (body.Collisions.JustLanded) landings++;
            }

            Assert.IsTrue(body.Collisions.Grounded);
            Assert.AreEqual(0f, TestWorld.Feet(body), Tolerance);
            Assert.AreEqual(1, landings, "JustLanded should be true on exactly one tick.");
        }

        [Test]
        public void StaysGrounded_WhenStandingStill()
        {
            world.Box(-10, -1, 10, 0);
            KinematicBody2D body = world.Body(new Vector2(0, 0.05f));
            body.Move(new Vector2(0, -0.1f));

            for (int i = 0; i < 10; i++)
            {
                body.Move(Vector2.zero);
                Assert.IsTrue(body.Collisions.Grounded, $"tick {i}");
            }
        }

        [Test]
        public void StopsAtWall_EvenAtFastFallSpeed()
        {
            world.Box(2, -5, 3, 5);
            KinematicBody2D body = world.Body(new Vector2(0, 0));
            float wallFace = 2f;
            bool hitWall = false;

            for (int i = 0; i < 20; i++)
            {
                body.Move(new Vector2(28f / 60f, 0));
                hitWall |= body.Collisions.HitWallRight;
                Assert.LessOrEqual(body.Position.x + 0.3f, wallFace + Tolerance, "Body went into the wall.");
            }

            Assert.IsTrue(hitWall);
            Assert.AreEqual(wallFace, body.Position.x + 0.3f, Tolerance);
        }

        [Test]
        public void HitsCeiling()
        {
            world.Box(-5, 3, 5, 4);
            KinematicBody2D body = world.Body(new Vector2(0, 0));

            bool hitCeiling = false;
            for (int i = 0; i < 10 && !hitCeiling; i++)
            {
                body.Move(new Vector2(0, 0.3f));
                hitCeiling = body.Collisions.HitCeiling;
            }

            Assert.IsTrue(hitCeiling);
            Assert.AreEqual(3f, body.Position.y + TestWorld.HalfHeight, Tolerance, "Head should touch the ceiling.");
        }

        [Test]
        public void Walks45DegreeHill_UpAndDown_WithoutLeavingGround()
        {
            world.Box(-10, -1, 30, 0);
            // Up 45° from x 2 to 5, plateau to 8, down 45° to 11.
            world.Polygon(new Vector2(2, 0), new Vector2(5, 3), new Vector2(8, 3), new Vector2(11, 0),
                          new Vector2(11, -0.5f), new Vector2(2, -0.5f));
            KinematicBody2D body = world.Body(new Vector2(0, 0.01f));
            body.Move(new Vector2(0, -0.1f));
            Assert.IsTrue(body.Collisions.Grounded);

            float highest = 0f;
            for (int i = 0; i < 120; i++)      // 9 u/s for 2 seconds
            {
                body.Move(new Vector2(9f / 60f, 0));
                Assert.IsTrue(body.Collisions.Grounded, $"Left the ground on tick {i} at x {body.Position.x:F2}");
                highest = Mathf.Max(highest, TestWorld.Feet(body));
            }

            Assert.Greater(body.Position.x, 12f, "Should have crossed the whole hill.");
            Assert.AreEqual(3f, highest, 0.05f, "Should have reached the plateau.");
            Assert.AreEqual(0f, TestWorld.Feet(body), Tolerance, "Should be back on the floor.");
        }

        [Test]
        public void SixtyDegreeSlope_BlocksLikeAWall()
        {
            world.Box(-10, -1, 30, 0);
            world.Polygon(new Vector2(2, 0), new Vector2(3.155f, 2), new Vector2(5, 2),
                          new Vector2(5, -0.5f), new Vector2(2, -0.5f));
            KinematicBody2D body = world.Body(new Vector2(0, 0.01f));
            body.Move(new Vector2(0, -0.1f));

            bool hitWall = false;
            for (int i = 0; i < 60; i++)
            {
                body.Move(new Vector2(9f / 60f, 0));
                hitWall |= body.Collisions.HitWallRight;
            }

            Assert.IsTrue(hitWall);
            Assert.Less(TestWorld.Feet(body), 0.2f, "Should not climb a 60° slope.");
        }

        [Test]
        public void OneWay_PassUpFromBelow_ThenLandOnTop()
        {
            world.Box(-3, 2, 3, 2.2f, oneWay: true);
            KinematicBody2D body = world.Body(new Vector2(0, 0));

            for (int i = 0; i < 12; i++)
            {
                body.Move(new Vector2(0, 0.3f));
                Assert.IsFalse(body.Collisions.HitCeiling, "One-way platforms must not block from below.");
            }
            Assert.Greater(TestWorld.Feet(body), 2.2f);

            MoveTicks(body, new Vector2(0, -0.1f), 30);
            Assert.IsTrue(body.Collisions.Grounded);
            Assert.IsTrue(body.Collisions.OnOneWay);
            Assert.AreEqual(2.2f, TestWorld.Feet(body), Tolerance);
        }

        [Test]
        public void DropThrough_FallsToSolidGroundBelow()
        {
            world.Box(-10, -1, 10, 0);
            world.Box(-3, 2, 3, 2.2f, oneWay: true);
            KinematicBody2D body = world.Body(new Vector2(0, 2.25f));
            body.Move(new Vector2(0, -0.1f));
            Assert.IsTrue(body.Collisions.OnOneWay);

            body.DropThroughOneWay();
            MoveTicks(body, new Vector2(0, -0.1f), 40);

            Assert.IsTrue(body.Collisions.Grounded);
            Assert.IsFalse(body.Collisions.OnOneWay);
            Assert.AreEqual(0f, TestWorld.Feet(body), Tolerance);
            Assert.IsFalse(body.IsDroppingThrough, "Should stop ignoring one-way platforms after passing through.");
        }

        [Test]
        public void CornerCorrection_NudgesPastSmallOverlap()
        {
            // Block's left edge is 0.1 inside the body's right edge (body spans x -0.3..0.3).
            world.Box(0.2f, 3, 3, 4);
            KinematicBody2D body = world.Body(new Vector2(0, 0));

            bool bonked = false;
            for (int i = 0; i < 12; i++)
            {
                body.Move(new Vector2(0, 0.3f), 0.15f);
                bonked |= body.Collisions.HitCeiling;
            }

            Assert.IsFalse(bonked);
            Assert.Greater(body.Position.y + TestWorld.HalfHeight, 4f, "Head should pass the block.");
            Assert.LessOrEqual(body.Position.x + 0.3f, 0.2f + Tolerance, "Should be nudged left of the block.");
        }

        [Test]
        public void CornerCorrection_DoesNotApplyToLargeOverlap()
        {
            world.Box(0f, 3, 3, 4);   // 0.3 overlap, more than the 0.15 limit
            KinematicBody2D body = world.Body(new Vector2(0, 0));

            bool bonked = false;
            for (int i = 0; i < 12 && !bonked; i++)
            {
                body.Move(new Vector2(0, 0.3f), 0.15f);
                bonked = body.Collisions.HitCeiling;
            }

            Assert.IsTrue(bonked);
            Assert.AreEqual(0f, body.Position.x, Tolerance);
        }

        [Test]
        public void IsTouchingWall_OnlyWhenAdjacent()
        {
            world.Box(0.3f, -5, 1, 5);   // wall face exactly at the body's right edge
            KinematicBody2D body = world.Body(new Vector2(0, 0));

            Assert.IsTrue(body.IsTouchingWall(1));
            Assert.IsFalse(body.IsTouchingWall(-1));

            body.Teleport(new Vector2(-0.5f, TestWorld.HalfHeight));
            UnityEngine.Physics2D.SyncTransforms();
            Assert.IsFalse(body.IsTouchingWall(1));
        }
    }
}
