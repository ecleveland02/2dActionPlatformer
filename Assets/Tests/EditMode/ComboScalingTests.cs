using Margin.Combat;
using NUnit.Framework;

namespace Margin.Tests
{
    public class ComboScalingTests
    {
        [Test]
        public void Scale_DropsPerHit_DownToTheFloor()
        {
            Assert.AreEqual(1f, ComboScaling.Scale(1, 0.15f, 0.5f), 1e-5f);
            Assert.AreEqual(0.85f, ComboScaling.Scale(2, 0.15f, 0.5f), 1e-5f);
            Assert.AreEqual(0.55f, ComboScaling.Scale(4, 0.15f, 0.5f), 1e-5f);
            Assert.AreEqual(0.5f, ComboScaling.Scale(5, 0.15f, 0.5f), 1e-5f);
            Assert.AreEqual(0.5f, ComboScaling.Scale(20, 0.15f, 0.5f), 1e-5f);
        }

        [Test]
        public void Register_CountsHits_AndReturnsRoundedScaledDamage()
        {
            var c = new ComboScaling();
            Assert.IsFalse(c.Active);
            Assert.AreEqual(8, c.Register(8, 0.15f, 0.5f));
            Assert.AreEqual(5, c.Register(6, 0.15f, 0.5f), "6 x 0.85 = 5.1");
            Assert.AreEqual(7, c.Register(10, 0.15f, 0.5f), "10 x 0.70 = 7");
            Assert.AreEqual(3, c.Hits);
            Assert.AreEqual(20, c.Damage);
        }

        [Test]
        public void Register_NeverScalesADamagingHitToZero()
        {
            var c = new ComboScaling();
            for (int i = 0; i < 10; i++) c.Register(1, 0.15f, 0.1f);
            Assert.AreEqual(1, c.Register(1, 0.15f, 0.1f));
            Assert.AreEqual(0, c.Register(0, 0.15f, 0.1f));
        }

        [Test]
        public void End_StartsANewCombo()
        {
            var c = new ComboScaling();
            c.Register(8, 0.15f, 0.5f);
            c.Register(8, 0.15f, 0.5f);
            c.End();
            Assert.IsFalse(c.Active);
            Assert.AreEqual(8, c.Register(8, 0.15f, 0.5f), "First hit of the new combo is full damage.");
        }
    }
}
