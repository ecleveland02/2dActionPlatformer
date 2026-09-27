using Margin.Rendering;
using NUnit.Framework;
using Pt = Margin.Rendering.WeaponShape.Pt;

namespace Margin.Tests
{
    public class WeaponShapeTests
    {
        private static readonly Pt Hand = new Pt(1f, 2f);
        private static readonly Pt Right = new Pt(1f, 0f);

        [Test]
        public void Blade_StartsAtTheHand_EndsExactlyAtTheTip()
        {
            Pt[] b = WeaponShape.Blade(Hand, Right, 0.9f, 0.05f, 1f);
            Assert.AreEqual(1f, b[0].X, 1e-5f); Assert.AreEqual(2f, b[0].Y, 1e-5f);
            Assert.AreEqual(1.9f, b[b.Length - 1].X, 1e-5f, "Tip = hand + dir * length, what hitboxes assume.");
            Assert.AreEqual(2f, b[b.Length - 1].Y, 1e-5f, "No curve offset at the tip.");
        }

        [Test]
        public void Blade_BowsTowardItsBack_MostAt60Percent_AndMirrors()
        {
            Pt[] up = WeaponShape.Blade(Hand, Right, 1f, 0.05f, 1f, points: 11);
            Pt[] down = WeaponShape.Blade(Hand, Right, 1f, 0.05f, -1f, points: 11);
            int peak = 6;   // t = 0.6
            Assert.AreEqual(2.05f, up[peak].Y, 1e-4f, "Full curve at 60% of the length.");
            Assert.AreEqual(1.95f, down[peak].Y, 1e-4f, "Other side when mirrored.");
            for (int i = 0; i < up.Length; i++) Assert.LessOrEqual(up[i].Y, 2.05f + 1e-4f);
        }

        [Test]
        public void Guard_IsCenteredAcrossTheBlade_HandleGoesBack()
        {
            Pt[] g = WeaponShape.Guard(Hand, Right, 0.2f);
            Assert.AreEqual(1f, g[0].X, 1e-5f); Assert.AreEqual(1f, g[1].X, 1e-5f);
            Assert.AreEqual(0.2f, System.Math.Abs(g[0].Y - g[1].Y), 1e-5f);
            Assert.AreEqual(2f, (g[0].Y + g[1].Y) / 2f, 1e-5f);

            Pt[] h = WeaponShape.Handle(Hand, Right, 0.22f);
            Assert.AreEqual(0.78f, h[1].X, 1e-5f, "Handle sticks out behind the fist.");
        }

        [Test]
        public void Pencil_TipIsExact_EraserIsBehindTheHand()
        {
            WeaponShape.Pencil p = WeaponShape.PencilShape(Hand, Right, 1.4f, 0.3f, 0.1f, 0.22f, 0.1f);
            Assert.AreEqual(2.4f, p.Graphite[1].X, 1e-5f);
            Assert.AreEqual(2f, p.Graphite[1].Y, 1e-5f);
            Assert.AreEqual(0.7f, p.Eraser[0].X, 1e-5f);
            Assert.AreEqual(0.1f, p.TopEdge[0].Y - p.BottomEdge[0].Y, 1e-5f, "Body width.");
        }
    }
}
