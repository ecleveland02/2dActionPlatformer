using Margin.Level;
using NUnit.Framework;

namespace Margin.Tests
{
    /// <summary>The camera's rules (spec 12) and the room transition fade (spec 11.1).</summary>
    public class CameraMathTests
    {
        [Test]
        public void DeadZone_HoldsStill_InsideTheZone()
        {
            Assert.AreEqual(10f, CameraMath.DeadZone(10f, 10.2f, 0.3f, 0.3f));
            Assert.AreEqual(10f, CameraMath.DeadZone(10f, 9.75f, 0.3f, 0.3f));
        }

        [Test]
        public void DeadZone_PullsTheCamera_JustEnoughToKeepTheTargetAtTheEdge()
        {
            Assert.AreEqual(10.7f, CameraMath.DeadZone(10f, 11f, 0.3f, 0.3f), 1e-5f);
            Assert.AreEqual(8.3f, CameraMath.DeadZone(10f, 8f, 0.3f, 0.3f), 1e-5f);
        }

        [Test]
        public void VerticalDeadZone_IsLopsided_SoAJumpDoesntMoveTheView()
        {
            // Up 2.4, down 1.2: a 2.2 unit rise is ignored, a 2.2 unit drop is followed.
            Assert.AreEqual(0f, CameraMath.DeadZone(0f, 2.2f, 1.2f, 2.4f));
            Assert.AreEqual(-1f, CameraMath.DeadZone(0f, -2.2f, 1.2f, 2.4f), 1e-5f);
        }

        [Test]
        public void Confine_KeepsTheViewInsideTheRoom()
        {
            // Room 0..40 wide, view 24 wide (half 12): the center stays within 12..28.
            Assert.AreEqual(12f, CameraMath.Confine(3f, 12f, 0f, 40f));
            Assert.AreEqual(28f, CameraMath.Confine(35f, 12f, 0f, 40f));
            Assert.AreEqual(20f, CameraMath.Confine(20f, 12f, 0f, 40f));
        }

        [Test]
        public void Confine_CentersARoomSmallerThanTheView()
        {
            Assert.AreEqual(5f, CameraMath.Confine(0f, 12f, 0f, 10f));
            Assert.AreEqual(5f, CameraMath.Confine(9f, 12f, 0f, 10f));
        }

        [Test]
        public void Approach_IsFrameRateIndependent()
        {
            // One 2-frame step lands where two 1-frame steps do.
            float once = CameraMath.Approach(0f, 10f, 8f, 2f);
            float twice = CameraMath.Approach(CameraMath.Approach(0f, 10f, 8f, 1f), 10f, 8f, 1f);
            Assert.AreEqual(once, twice, 1e-4f);
            Assert.AreEqual(10f * (1f - (float)System.Math.Exp(-1f)), CameraMath.Approach(0f, 10f, 8f, 8f), 1e-4f);
        }

        [Test]
        public void Approach_ZeroFramesSnaps_ZeroTimeHolds()
        {
            Assert.AreEqual(10f, CameraMath.Approach(0f, 10f, 0f, 1f));
            Assert.AreEqual(0f, CameraMath.Approach(0f, 10f, 8f, 0f), "paused: no time passed");
        }
    }

    public class TransitionTimerTests
    {
        [Test]
        public void FadesOut_SignalsTheSwapOnce_ThenFadesIn()
        {
            var t = new TransitionTimer();
            t.Start(fadeOut: 4, fadeIn: 5);
            Assert.AreEqual(0f, t.Fade);

            int swaps = 0, swapTick = -1;
            for (int tick = 1; tick <= 20; tick++)
            {
                if (t.Tick())
                {
                    swaps++;
                    swapTick = tick;
                    Assert.AreEqual(1f, t.Fade, "fully covered when the swap happens");
                }
            }
            Assert.AreEqual(1, swaps);
            Assert.AreEqual(4, swapTick, "covered on the 4th tick of a 4-frame fade");
            Assert.IsFalse(t.Active);
            Assert.AreEqual(0f, t.Fade);
        }

        [Test]
        public void FadeIn_TakesExactlyItsFrames()
        {
            var t = new TransitionTimer();
            t.StartIn(3);
            Assert.AreEqual(1f, t.Fade);
            t.Tick();
            Assert.AreEqual(2f / 3f, t.Fade, 1e-5f);
            t.Tick();
            Assert.IsTrue(t.Active);
            t.Tick();
            Assert.IsFalse(t.Active);
        }

        [Test]
        public void Fade_RisesSteadily_AndCancelClears()
        {
            var t = new TransitionTimer();
            t.Start(10, 10);
            t.Tick();
            t.Tick();
            Assert.AreEqual(0.2f, t.Fade, 1e-5f);
            t.Cancel();
            Assert.IsFalse(t.Active);
            Assert.AreEqual(0f, t.Fade);
        }
    }
}

namespace Margin.Tests
{
    using System;
    using System.Collections.Generic;
    using Margin.Level;
    using NUnit.Framework;
    using Pt = Margin.Rendering.WeaponShape.Pt;

    /// <summary>The level's procedural line art stays inside the shapes it decorates.</summary>
    public class LevelArtTests
    {
        [Test]
        public void Hatch_StaysInsideTheBox_AndCoversIt()
        {
            List<Pt> hatch = LevelArt.Hatch(10f, 2f, 0.5f);
            Assert.Greater(hatch.Count, 20);
            float minX = float.MaxValue, maxX = float.MinValue;
            foreach (Pt p in hatch)
            {
                Assert.That(p.X, Is.InRange(-5f - 1e-4f, 5f + 1e-4f));
                Assert.That(p.Y, Is.InRange(-1f - 1e-4f, 1f + 1e-4f));
                minX = Math.Min(minX, p.X);
                maxX = Math.Max(maxX, p.X);
            }
            Assert.Less(minX, -4.5f, "reaches the left end");
            Assert.Greater(maxX, 4.5f, "reaches the right end");
        }

        [Test]
        public void Hatch_TinyBox_IsSafe()
        {
            Assert.DoesNotThrow(() => LevelArt.Hatch(0.05f, 0.05f, 0.5f));
        }

        [Test]
        public void EveryDoodle_IsDrawable_AndAboutTheRightSize()
        {
            foreach (DoodleKind kind in Enum.GetValues(typeof(DoodleKind)))
            {
                List<List<Pt>> strokes = LevelArt.Doodle(kind, 2f);
                Assert.Greater(strokes.Count, 0, kind.ToString());
                foreach (List<Pt> stroke in strokes)
                {
                    Assert.GreaterOrEqual(stroke.Count, 2, kind.ToString());
                    foreach (Pt p in stroke)
                    {
                        Assert.That(p.X, Is.InRange(-1.3f, 1.3f), kind.ToString());
                        Assert.That(p.Y, Is.InRange(-1.3f, 1.3f), kind.ToString());
                    }
                }
            }
        }

        [Test]
        public void Smudge_FitsItsEllipse()
        {
            foreach (Pt p in LevelArt.Smudge(3f, 1f, 5))
            {
                Assert.That(p.X, Is.InRange(-1.8f, 1.8f));
                Assert.That(p.Y, Is.InRange(-0.6f, 0.6f));
            }
        }
    }
}

namespace Margin.Tests
{
    using Margin.Audio;
    using NUnit.Framework;

    /// <summary>Audio helpers (spec 13).</summary>
    public class AudioMathTests
    {
        [Test]
        public void BarsToSeconds_MatchesTheBossTempo()
        {
            Assert.AreEqual(3.75f, AudioMath.BarsToSeconds(2f, 128f), 1e-4f, "two bars at 128 BPM");
            Assert.AreEqual(90f, AudioMath.BarsToSeconds(48f, 128f), 1e-3f, "the 48-bar boss loop is 90 s");
            Assert.AreEqual(0f, AudioMath.BarsToSeconds(2f, 0f));
        }

        [Test]
        public void PickVariant_NeverRepeatsBackToBack_AndCoversAll()
        {
            var seen = new bool[3];
            int last = -1;
            for (int i = 0; i < 300; i++)
            {
                int pick = AudioMath.PickVariant(3, last, (i * 0.6180339f) % 1f);
                Assert.That(pick, Is.InRange(0, 2));
                Assert.AreNotEqual(last, pick);
                seen[pick] = true;
                last = pick;
            }
            Assert.IsTrue(seen[0] && seen[1] && seen[2]);
        }

        [Test]
        public void PickVariant_SingleOrNone_IsSafe()
        {
            Assert.AreEqual(0, AudioMath.PickVariant(1, 0, 0.9f));
            Assert.AreEqual(0, AudioMath.PickVariant(0, -1, 0.5f));
            Assert.AreEqual(1, AudioMath.PickVariant(2, 0, 0.99f));
        }

        [Test]
        public void DbToGain()
        {
            Assert.AreEqual(1f, AudioMath.DbToGain(0f), 1e-5f);
            Assert.AreEqual(0.5012f, AudioMath.DbToGain(-6f), 1e-3f);
        }
    }
}
