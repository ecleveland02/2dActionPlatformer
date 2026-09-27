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

namespace Margin.Tests
{
    using Margin.Rendering;
    using NUnit.Framework;

    /// <summary>The katana's cutting edge leads the swing (the curve flips to follow the slash).</summary>
    public class KatanaEdgeTests
    {
        private static float Run(float side, float turn, float rest, ref float still, int frames, float dt = 1f / 60f)
        {
            for (int i = 0; i < frames; i++)
                side = WeaponShape.EdgeSide(side, turn, rest, ref still, dt, 240f, 0.35f, 0.05f);
            return side;
        }

        [Test]
        public void ClockwiseSwing_KeepsTheDefaultSide_CounterClockwiseFlipsIt()
        {
            float still = 0f;
            Assert.AreEqual(1f, Run(1f, -900f, 1f, ref still, 10), 1e-5f, "downward slash facing right: edge leads down");
            Assert.AreEqual(-1f, Run(1f, 900f, 1f, ref still, 10), 1e-5f, "upswing: the edge flips to lead upward");
        }

        [Test]
        public void Flip_PassesThroughStraight_InsteadOfPopping()
        {
            float still = 0f;
            float side = WeaponShape.EdgeSide(1f, 900f, 1f, ref still, 1f / 60f, 240f, 0.35f, 0.05f);
            Assert.That(side, Is.InRange(-1f, 1f).And.Not.EqualTo(-1f), "one frame in, still mid-flip");
            Assert.AreEqual(-1f, Run(side, 900f, 1f, ref still, 3), 1e-5f, "done within the 0.05 s flip");
        }

        [Test]
        public void HoldingStill_KeepsTheSide_ThenSettlesBack()
        {
            float still = 0f;
            float side = Run(1f, 900f, 1f, ref still, 10);
            side = Run(side, 0f, 1f, ref still, 15);
            Assert.AreEqual(-1f, side, 1e-5f, "a short pause keeps the swing's edge");
            side = Run(side, 0f, 1f, ref still, 30);
            Assert.AreEqual(1f, side, 1e-5f, "after ~0.35 s it's back to resting");
        }
    }
}
