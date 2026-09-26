using Margin.Player;
using NUnit.Framework;

namespace Margin.Tests
{
    public class MovementMathTests
    {
        // Spec defaults (Section 5.2). Tests use literal values so a changed asset can't silently change a test.
        private const float JumpHeight = 3.2f;
        private const float MinJumpHeight = 1.2f;
        private const int FramesToApex = 22;
        private const float FallMultiplier = 1.8f;
        private const float MaxFall = 20f;
        private const float ApexThreshold = 1.5f;
        private const float ApexMultiplier = 0.5f;
        private const float RunSpeed = 9f;

        private static readonly float G = MovementMath.RiseGravity(JumpHeight, FramesToApex);
        private static readonly float V0 = MovementMath.JumpVelocity(JumpHeight, FramesToApex);

        private struct JumpResult
        {
            public float Peak;
            public int PeakTick;
        }

        /// <summary>
        /// Simulates a jump tick by tick, in the order the Jump state will use:
        /// 1. if jump was released and still rising, cut speed; 2. pick gravity; 3. step.
        /// releaseTick = the first tick jump is no longer held (null = held the whole time).
        /// </summary>
        private static JumpResult SimulateJump(int? releaseTick, bool apexHang)
        {
            float y = 0f, vy = V0;
            var result = new JumpResult();

            for (int tick = 1; tick < 300; tick++)
            {
                bool held = releaseTick == null || tick < releaseTick;
                if (!held && vy > 0f)
                    vy = System.Math.Min(vy, MovementMath.JumpCutVelocity(G, MinJumpHeight, y));

                float gravity = MovementMath.Gravity(vy, G, FallMultiplier, ApexThreshold, ApexMultiplier, apexHang && held);
                vy = MovementMath.VerticalStep(vy, gravity, MaxFall, out float dy);
                y += dy;

                if (y > result.Peak)
                {
                    result.Peak = y;
                    result.PeakTick = tick;
                }
                if (y < 0f) break;
            }
            return result;
        }

        [Test]
        public void DerivedJumpValues_MatchHandCalculation()
        {
            // t = 22/60 s. g = 2h/t² ≈ 47.60, v = 2h/t ≈ 17.45
            Assert.AreEqual(47.603f, G, 0.001f);
            Assert.AreEqual(17.4545f, V0, 0.001f);
        }

        [Test]
        public void FullJump_ReachesConfiguredHeight_OnApexFrame()
        {
            JumpResult jump = SimulateJump(releaseTick: null, apexHang: false);
            Assert.AreEqual(JumpHeight, jump.Peak, 0.001f);
            Assert.AreEqual(FramesToApex, jump.PeakTick);
        }

        [Test]
        public void FullJump_WithApexHang_StaysWithinSpecTolerance()
        {
            // Section 17: jump reaches configured height within 0.05 units. Apex hang adds a little.
            JumpResult jump = SimulateJump(releaseTick: null, apexHang: true);
            Assert.AreEqual(JumpHeight, jump.Peak, 0.05f);
            Assert.That(jump.PeakTick, Is.InRange(FramesToApex, FramesToApex + 2));
        }

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(5)]
        public void TappedJump_PeaksAtMinHeight(int releaseTick)
        {
            JumpResult jump = SimulateJump(releaseTick, apexHang: true);
            Assert.AreEqual(MinJumpHeight, jump.Peak, 0.01f);
        }

        [Test]
        public void JumpHeight_GrowsWithHoldTime_BetweenMinAndFull()
        {
            float previous = 0f;
            for (int release = 1; release <= FramesToApex + 1; release++)
            {
                float peak = SimulateJump(release, apexHang: true).Peak;
                Assert.That(peak, Is.GreaterThanOrEqualTo(previous - 0.001f), $"release on tick {release}");
                Assert.That(peak, Is.InRange(MinJumpHeight - 0.01f, JumpHeight + 0.05f), $"release on tick {release}");
                previous = peak;
            }
        }

        [Test]
        public void JumpCut_IsZero_OncePastMinHeight()
        {
            Assert.AreEqual(0f, MovementMath.JumpCutVelocity(G, MinJumpHeight, 1.5f));
            // At takeoff the cap is the speed that rises exactly min height: sqrt(2 · 47.6 · 1.2) ≈ 10.69
            Assert.AreEqual(10.688f, MovementMath.JumpCutVelocity(G, MinJumpHeight, 0f), 0.01f);
        }

        [Test]
        public void Fall_UsesFallMultiplier_AndClampsToMaxFallSpeed()
        {
            Assert.AreEqual(G * FallMultiplier, MovementMath.Gravity(-5f, G, FallMultiplier, ApexThreshold, ApexMultiplier, false), 0.001f);

            float vy = 0f;
            int ticks = 0;
            while (vy > -MaxFall && ticks < 100)
            {
                vy = MovementMath.VerticalStep(vy, G * FallMultiplier, MaxFall, out _);
                ticks++;
            }
            Assert.AreEqual(-MaxFall, vy);
            Assert.AreEqual(15, ticks, "From rest, fall speed caps after 15 ticks with default tuning.");
        }

        [Test]
        public void ApexHang_OnlyInsideThreshold_AndOnlyWhenActive()
        {
            Assert.AreEqual(G * 0.5f, MovementMath.Gravity(1f, G, FallMultiplier, ApexThreshold, ApexMultiplier, true), 0.001f);
            Assert.AreEqual(G, MovementMath.Gravity(1f, G, FallMultiplier, ApexThreshold, ApexMultiplier, false), 0.001f);
            Assert.AreEqual(G, MovementMath.Gravity(2f, G, FallMultiplier, ApexThreshold, ApexMultiplier, true), 0.001f);
            Assert.AreEqual(G * FallMultiplier * 0.5f, MovementMath.Gravity(-1f, G, FallMultiplier, ApexThreshold, ApexMultiplier, true), 0.001f);
        }

        [Test]
        public void GroundRun_ReachesFullSpeed_InFourTicks()
        {
            float accel = MovementMath.SpeedStepPerTick(RunSpeed, 4);
            float decel = MovementMath.SpeedStepPerTick(RunSpeed, 3);
            float vx = 0f;

            for (int i = 0; i < 3; i++) vx = MovementMath.HorizontalStep(vx, 1f, RunSpeed, accel, decel);
            Assert.Less(vx, RunSpeed);
            vx = MovementMath.HorizontalStep(vx, 1f, RunSpeed, accel, decel);
            Assert.AreEqual(RunSpeed, vx, 0.0001f);
        }

        [Test]
        public void GroundRun_StopsInThreeTicks()
        {
            float accel = MovementMath.SpeedStepPerTick(RunSpeed, 4);
            float decel = MovementMath.SpeedStepPerTick(RunSpeed, 3);
            float vx = RunSpeed;

            for (int i = 0; i < 2; i++) vx = MovementMath.HorizontalStep(vx, 0f, RunSpeed, accel, decel);
            Assert.Greater(vx, 0f);
            vx = MovementMath.HorizontalStep(vx, 0f, RunSpeed, accel, decel);
            Assert.AreEqual(0f, vx, 0.0001f);
        }

        [Test]
        public void TurnAround_BrakesThenAccelerates_InSevenTicks()
        {
            float accel = MovementMath.SpeedStepPerTick(RunSpeed, 4);
            float decel = MovementMath.SpeedStepPerTick(RunSpeed, 3);
            float vx = RunSpeed;
            int ticks = 0;

            while (vx > -RunSpeed + 0.0001f && ticks < 50)
            {
                vx = MovementMath.HorizontalStep(vx, -1f, RunSpeed, accel, decel);
                ticks++;
            }
            Assert.AreEqual(7, ticks, "3 ticks braking at the stop rate + 4 ticks accelerating.");
        }

        [Test]
        public void Approach_NeverOvershoots()
        {
            Assert.AreEqual(5f, MovementMath.Approach(4f, 5f, 3f));
            Assert.AreEqual(-5f, MovementMath.Approach(-4f, -5f, 3f));
            Assert.AreEqual(2f, MovementMath.Approach(0f, 5f, 2f));
            Assert.AreEqual(1f, MovementMath.Approach(1f, 1f, 2f));
        }
    }
}
