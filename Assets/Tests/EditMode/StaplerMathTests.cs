using Margin.Bosses;
using NUnit.Framework;

namespace Margin.Tests
{
    /// <summary>The Stapler Titan's aiming (spec 10, Boss 2): Hop Slam's arc and Staple Rain's lanes.</summary>
    public class StaplerMathTests
    {
        private const float Dt = 1f / 60f;

        [Test]
        public void Hop_LandsOnTheTarget_AfterTheAirTime_AndPeaksAtTheHeight()
        {
            StaplerMath.HopVelocity(0f, 6f, -20f, 20f, 12f, 40, 4.5f, Dt, out float vx, out float vy, out float g, out float landX);
            Assert.AreEqual(6f, landX, 1e-4f);

            // Fly it tick by tick (midpoint steps, like the boss does) and check where and when it comes down.
            float x = 0f, y = 0f, peak = 0f;
            int frames = 0;
            do
            {
                float next = vy - g * Dt;
                y += (vy + next) * 0.5f * Dt;
                vy = next;
                x += vx * Dt;
                peak = System.Math.Max(peak, y);
                frames++;
            } while (y > 0f && frames < 200);
            Assert.AreEqual(40, frames, 1, "comes down after the air time");
            Assert.AreEqual(6f, x, 0.2f);
            Assert.AreEqual(4.5f, peak, 0.05f);
        }

        [Test]
        public void Hop_IsLimitedByMaxDistance_AndTheArena()
        {
            StaplerMath.HopVelocity(0f, 30f, -20f, 20f, 12f, 40, 4.5f, Dt, out _, out _, out _, out float far);
            Assert.AreEqual(12f, far, 1e-4f);
            StaplerMath.HopVelocity(15f, 30f, -20f, 18f, 12f, 40, 4.5f, Dt, out _, out _, out _, out float wall);
            Assert.AreEqual(18f, wall, 1e-4f);
        }

        [Test]
        public void RainLanes_OneOnThePlayer_RestSpacedApart()
        {
            var lanes = StaplerMath.RainLanes(3.3f, 0f, 30f, 5, 3f, 1234u);
            Assert.AreEqual(5, lanes.Count);
            Assert.AreEqual(3.3f, lanes[0], 1e-4f, "the first lane is right on the player");
            for (int i = 0; i < lanes.Count; i++)
            {
                Assert.That(lanes[i], Is.InRange(0f, 30f));
                for (int j = i + 1; j < lanes.Count; j++)
                    Assert.GreaterOrEqual(System.Math.Abs(lanes[i] - lanes[j]), 3f - 1e-3f);
            }
        }

        [Test]
        public void RainLanes_FewerWhenTheyDontFit()
        {
            var lanes = StaplerMath.RainLanes(5f, 0f, 10f, 9, 4f, 7u);
            Assert.LessOrEqual(lanes.Count, 3, "a 10-unit arena fits at most 3 lanes 4 apart");
            Assert.GreaterOrEqual(lanes.Count, 1);
        }

        [Test]
        public void RainLanes_SameSeed_SameLanes()
        {
            CollectionAssert.AreEqual(StaplerMath.RainLanes(2f, 0f, 30f, 5, 3f, 99u), StaplerMath.RainLanes(2f, 0f, 30f, 5, 3f, 99u));
        }
    }
}
