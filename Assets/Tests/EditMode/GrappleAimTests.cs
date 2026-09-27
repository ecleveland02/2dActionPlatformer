using Margin.Abilities;
using NUnit.Framework;

namespace Margin.Tests
{
    /// <summary>Which ring the Grapple Line goes to (spec 8).</summary>
    public class GrappleAimTests
    {
        [Test]
        public void AngleFromUp_IsMeasuredTowardFacing()
        {
            Assert.AreEqual(0f, GrappleAim.AngleFromUp(0f, 5f, 1), 1e-4f, "straight up");
            Assert.AreEqual(90f, GrappleAim.AngleFromUp(5f, 0f, 1), 1e-4f, "straight ahead facing right");
            Assert.AreEqual(90f, GrappleAim.AngleFromUp(-5f, 0f, -1), 1e-4f, "straight ahead facing left");
            Assert.AreEqual(-90f, GrappleAim.AngleFromUp(-5f, 0f, 1), 1e-4f, "behind");
            Assert.AreEqual(45f, GrappleAim.AngleFromUp(3f, 3f, 1), 1e-4f);
        }

        [Test]
        public void OutOfRange_OrBehind_IsRejected()
        {
            Assert.Less(GrappleAim.Score(20f, 5f, 1, 9.5f, -15f, 110f), 0f, "too far");
            Assert.Less(GrappleAim.Score(-4f, 3f, 1, 9.5f, -15f, 110f), 0f, "behind you");
            Assert.Less(GrappleAim.Score(3f, -6f, 1, 9.5f, -15f, 110f), 0f, "well below");
            Assert.GreaterOrEqual(GrappleAim.Score(-0.5f, 5f, 1, 9.5f, -15f, 110f), 0f, "just behind straight up still counts");
        }

        [Test]
        public void NearerAndUpAhead_ScoresBetter()
        {
            float near = GrappleAim.Score(3f, 3f, 1, 9.5f, -15f, 110f);
            float far = GrappleAim.Score(5f, 5f, 1, 9.5f, -15f, 110f);
            Assert.Less(near, far);
            float upAhead = GrappleAim.Score(3f, 3f, 1, 9.5f, -15f, 110f);
            float overhead = GrappleAim.Score(0f, 4.2426f, 1, 9.5f, -15f, 110f);
            Assert.Less(upAhead, overhead, "same distance: 45 degrees up-ahead beats overhead");
        }
    }
}
