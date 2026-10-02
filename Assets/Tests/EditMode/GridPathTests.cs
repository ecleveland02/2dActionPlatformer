using System.Collections.Generic;
using Margin.Level;
using NUnit.Framework;
using Pt = Margin.Rendering.WeaponShape.Pt;

namespace Margin.Tests
{
    /// <summary>World 2's grid blocks (spec 11.2): they wait on grid points and slide between them.</summary>
    public class GridPathTests
    {
        private static List<Pt> Line(params float[] xs)
        {
            var list = new List<Pt>();
            foreach (float x in xs) list.Add(new Pt(x, 0f));
            return list;
        }

        [Test]
        public void Waits_ThenSlides_AndLandsExactlyOnTheGrid()
        {
            // 6 cells per second = 0.1 cell per tick: 4 cells take 40 ticks.
            var path = new GridPath(Line(0f, 4f), cellsPerSecond: 6f, pauseFrames: 10, loop: false);
            for (int i = 0; i < 10; i++) Assert.AreEqual(0f, path.Tick().X, "waiting on the first point");
            for (int i = 0; i < 39; i++) path.Tick();
            Assert.That(path.Offset.X, Is.InRange(3.9f, 4f));
            Assert.AreEqual(4f, path.Tick().X, "snaps exactly onto the grid point");
            Assert.IsFalse(path.Moving, "then waits");
        }

        [Test]
        public void EasesInAndOut()
        {
            var path = new GridPath(Line(0f, 4f), 6f, 0, false);
            float first = path.Tick().X;
            for (int i = 0; i < 18; i++) path.Tick();
            float a = path.Offset.X, b = path.Tick().X;
            Assert.Less(first, 0.05f, "starts slowly");
            Assert.Greater(b - a, first, "faster in the middle");
        }

        [Test]
        public void PingPong_ComesBack_LoopGoesRound()
        {
            var ping = new GridPath(Line(0f, 1f, 2f), 60f, 0, false);   // 1 cell per tick
            var seen = new List<float>();
            for (int i = 0; i < 6; i++) seen.Add(ping.Tick().X);
            CollectionAssert.AreEqual(new[] { 1f, 2f, 1f, 0f, 1f, 2f }, seen);

            var loop = new GridPath(new List<Pt> { new Pt(0f, 0f), new Pt(1f, 0f), new Pt(1f, 1f) }, 60f, 0, true);
            loop.Tick(); loop.Tick();
            loop.Tick();   // the diagonal back is 1.41 cells long: two ticks
            Pt back = loop.Tick();
            Assert.AreEqual(0f, back.X, 1e-5f);
            Assert.AreEqual(0f, back.Y, 1e-5f, "the last point leads back to the first");
        }

        [Test]
        public void StartDelay_AndSinglePoint()
        {
            var late = new GridPath(Line(0f, 1f), 60f, 0, false, startDelayFrames: 5);
            for (int i = 0; i < 5; i++) Assert.AreEqual(0f, late.Tick().X);
            Assert.AreEqual(1f, late.Tick().X);
            var still = new GridPath(Line(3f), 6f, 0, false);
            Assert.AreEqual(3f, still.Tick().X);
            Assert.IsFalse(still.Moving);
        }
    }
}
