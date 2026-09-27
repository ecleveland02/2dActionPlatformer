using Margin.Combat;
using NUnit.Framework;

namespace Margin.Tests
{
    public class HealthTests
    {
        [Test]
        public void Damage_ReducesHealth_AndStartsInvulnerability()
        {
            var h = new Health(100);
            Assert.AreEqual(8, h.TakeDamage(8, 45));
            Assert.AreEqual(92, h.Current);
            Assert.IsTrue(h.IsInvulnerable);
        }

        [Test]
        public void Invulnerable_IgnoresFurtherHits_ForExactlyTheWindow()
        {
            var h = new Health(100);
            h.TakeDamage(10, 45);
            for (int i = 0; i < 44; i++) h.Tick();
            Assert.AreEqual(0, h.TakeDamage(10, 45), "Still invulnerable on frame 45.");
            h.Tick();
            Assert.AreEqual(10, h.TakeDamage(10, 45), "Vulnerable again after 45 frames.");
            Assert.AreEqual(80, h.Current);
        }

        [Test]
        public void Damage_StopsAtZero()
        {
            var h = new Health(20);
            Assert.AreEqual(20, h.TakeDamage(50, 0));
            Assert.IsTrue(h.IsDepleted);
        }

        [Test]
        public void HealFraction_RestoresThirtyPercent_ClampedToMax()
        {
            var h = new Health(100);
            h.TakeDamage(50, 0);
            Assert.AreEqual(30, h.HealFraction(0.3f));
            Assert.AreEqual(80, h.Current);
            Assert.AreEqual(20, h.HealFraction(0.3f), "Only up to Max.");
            Assert.AreEqual(100, h.Current);
        }

        [Test]
        public void StartInvulnerability_WithoutDamage_LastsExactlyTheWindow()
        {
            var h = new Health(100);
            h.StartInvulnerability(45);
            Assert.AreEqual(100, h.Current);
            for (int i = 0; i < 44; i++) h.Tick();
            Assert.IsTrue(h.IsInvulnerable, "Frame 45.");
            h.Tick();
            Assert.IsFalse(h.IsInvulnerable);
        }

        [Test]
        public void StartInvulnerability_NeverShortensALongerWindow()
        {
            var h = new Health(100);
            h.StartInvulnerability(45);
            h.StartInvulnerability(10);
            Assert.AreEqual(45, h.InvulnerableFramesLeft);
        }
    }
}
