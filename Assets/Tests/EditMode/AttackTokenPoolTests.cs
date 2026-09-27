using Margin.Enemies;
using NUnit.Framework;

namespace Margin.Tests
{
    public class AttackTokenPoolTests
    {
        [Test]
        public void Capacity_NoCap_IsOnePerEngagedEnemy()
        {
            Assert.AreEqual(0, AttackTokenPool.Capacity(0, 0));
            Assert.AreEqual(1, AttackTokenPool.Capacity(1, 0));
            Assert.AreEqual(5, AttackTokenPool.Capacity(5, 0));
        }

        [Test]
        public void Capacity_WithCap_NeverExceedsIt()
        {
            Assert.AreEqual(1, AttackTokenPool.Capacity(1, 2));
            Assert.AreEqual(2, AttackTokenPool.Capacity(5, 2));
        }

        [Test]
        public void TryTake_RespectsCapacity_AndReleaseFreesASlot()
        {
            var pool = new AttackTokenPool();
            object a = new object(), b = new object();
            Assert.IsTrue(pool.TryTake(a, 1));
            Assert.IsFalse(pool.TryTake(b, 1), "Only one slot.");
            Assert.IsTrue(pool.TryTake(a, 1), "Asking again while holding one is fine.");
            Assert.AreEqual(1, pool.Count);

            pool.Release(a);
            Assert.IsFalse(pool.Holds(a));
            Assert.IsTrue(pool.TryTake(b, 1));
        }
    }
}
