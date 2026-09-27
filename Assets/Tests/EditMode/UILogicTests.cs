using System;
using System.Collections.Generic;
using Margin.UI;
using NUnit.Framework;
using Pt = Margin.Rendering.WeaponShape.Pt;

namespace Margin.Tests
{
    public class InkLinesTests
    {
        [Test]
        public void Noise_IsRepeatable_AndInRange()
        {
            for (int i = 0; i < 200; i++)
            {
                float n = InkLines.Noise(7, i);
                Assert.AreEqual(n, InkLines.Noise(7, i));
                Assert.That(n, Is.InRange(-1f, 1f));
            }
            Assert.AreNotEqual(InkLines.Noise(7, 3), InkLines.Noise(8, 3), "different seeds give different wobble");
        }

        [Test]
        public void WobblyLine_StaysWithinAmplitude_OfTheStraightLine()
        {
            List<Pt> line = InkLines.WobblyLine(new Pt(0f, 0f), new Pt(100f, 0f), amplitude: 2f, step: 5f, seed: 3);
            Assert.AreEqual(21, line.Count, "100 long, a point every 5");
            foreach (Pt p in line) Assert.LessOrEqual(Math.Abs(p.Y), 2f + 1e-4f);
            Assert.AreEqual(0f, line[0].X, 1e-4f);
            Assert.AreEqual(100f, line[line.Count - 1].X, 1e-4f);
            bool wobbles = false;
            foreach (Pt p in line) wobbles |= Math.Abs(p.Y) > 0.05f;
            Assert.IsTrue(wobbles);
        }

        [Test]
        public void WobblyLine_SameSeed_SameShape()
        {
            List<Pt> a = InkLines.WobblyLine(new Pt(0f, 0f), new Pt(50f, 20f), 1.5f, 4f, 11);
            List<Pt> b = InkLines.WobblyLine(new Pt(0f, 0f), new Pt(50f, 20f), 1.5f, 4f, 11);
            for (int i = 0; i < a.Count; i++)
            {
                Assert.AreEqual(a[i].X, b[i].X);
                Assert.AreEqual(a[i].Y, b[i].Y);
            }
        }

        [Test]
        public void SketchBox_FourSides_OvershootTheCorners()
        {
            List<List<Pt>> sides = InkLines.SketchBox(10f, 10f, 100f, 40f, amplitude: 0f, overshoot: 4f, step: 10f, seed: 1);
            Assert.AreEqual(4, sides.Count);
            List<Pt> top = sides[0];
            Assert.Less(top[0].X, 10f, "top starts left of the corner");
            Assert.Greater(top[top.Count - 1].X, 110f, "and ends right of the other corner");
            Assert.GreaterOrEqual(top[0].X, 10f - 4f - 1e-4f);
        }

        [Test]
        public void WobblyOutline_KeepsTheCorners()
        {
            List<Pt> outline = InkLines.WobblyOutline(0f, 0f, 80f, 20f, amplitude: 2f, step: 5f, seed: 5);
            Assert.IsTrue(Contains(outline, 0f, 0f));
            Assert.IsTrue(Contains(outline, 80f, 0f));
            Assert.IsTrue(Contains(outline, 80f, 20f));
            Assert.IsTrue(Contains(outline, 0f, 20f));
        }

        [Test]
        public void StrokeWidth_VariesAroundTheBaseWidth()
        {
            for (int i = 0; i <= 20; i++)
                Assert.That(InkLines.StrokeWidth(3f, 0.25f, 2, i / 20f), Is.InRange(3f * 0.75f - 1e-4f, 3f * 1.25f + 1e-4f));
        }

        private static bool Contains(List<Pt> points, float x, float y)
        {
            foreach (Pt p in points)
                if (Math.Abs(p.X - x) < 1e-4f && Math.Abs(p.Y - y) < 1e-4f) return true;
            return false;
        }
    }

    public class ChipBarTests
    {
        [Test]
        public void Drop_LeavesATrail_ThatHoldsThenDrains()
        {
            var bar = new ChipBar(1f);
            bar.Set(0.7f, holdFrames: 20);
            Assert.AreEqual(0.7f, bar.Value, 1e-5f);
            Assert.AreEqual(1f, bar.Trail, 1e-5f);
            bar.Tick(19f, drainPerFrame: 0.01f);
            Assert.AreEqual(1f, bar.Trail, 1e-5f, "still holding");
            bar.Tick(11f, 0.01f);
            Assert.AreEqual(0.9f, bar.Trail, 1e-4f, "1 frame of hold left, then 10 frames of drain");
            bar.Tick(100f, 0.01f);
            Assert.AreEqual(0.7f, bar.Trail, 1e-5f, "never drains below the value");
            Assert.IsFalse(bar.Draining);
        }

        [Test]
        public void ComboHits_RestartTheHold_AndPileIntoOneChip()
        {
            var bar = new ChipBar(1f);
            bar.Set(0.9f, 20);
            bar.Tick(15f, 0.01f);
            bar.Set(0.8f, 20);
            bar.Tick(15f, 0.01f);
            Assert.AreEqual(1f, bar.Trail, 1e-5f, "second hit restarted the hold");
            Assert.AreEqual(0.8f, bar.Value, 1e-5f);
        }

        [Test]
        public void Gains_ShowInstantly()
        {
            var bar = new ChipBar(0.2f);
            bar.Set(0.6f, 20);
            Assert.AreEqual(0.6f, bar.Value, 1e-5f);
            Assert.AreEqual(0.6f, bar.Trail, 1e-5f);
        }

        [Test]
        public void Snap_ClearsTheTrail_AndValuesClamp()
        {
            var bar = new ChipBar(1f);
            bar.Set(-0.5f, 20);
            Assert.AreEqual(0f, bar.Value);
            bar.Snap(2f);
            Assert.AreEqual(1f, bar.Value);
            Assert.AreEqual(1f, bar.Trail);
        }
    }

    public class MenuCursorTests
    {
        [Test]
        public void Move_WrapsAroundBothEnds()
        {
            var c = new MenuCursor(4);
            c.Move(-1);
            Assert.AreEqual(3, c.Index);
            c.Move(1);
            Assert.AreEqual(0, c.Index);
            c.Move(6);
            Assert.AreEqual(2, c.Index);
        }

        [Test]
        public void Hold_MovesOnPress_ThenRepeatsAfterTheDelay()
        {
            var c = new MenuCursor(10);
            Assert.IsTrue(c.Hold(1, 1f, repeatDelay: 20f, repeatInterval: 6f));
            Assert.AreEqual(1, c.Index);
            int moves = 0;
            for (int f = 0; f < 19; f++) if (c.Hold(1, 1f, 20f, 6f)) moves++;
            Assert.AreEqual(0, moves, "nothing until the delay");
            Assert.IsTrue(c.Hold(1, 1f, 20f, 6f), "frame 20: first repeat");
            for (int f = 0; f < 5; f++) Assert.IsFalse(c.Hold(1, 1f, 20f, 6f));
            Assert.IsTrue(c.Hold(1, 1f, 20f, 6f), "then every 6 frames");
            Assert.AreEqual(3, c.Index);
        }

        [Test]
        public void Hold_ReleaseOrReverse_MovesImmediately()
        {
            var c = new MenuCursor(5);
            c.Hold(1, 1f, 20f, 6f);
            c.Hold(0, 1f, 20f, 6f);
            Assert.IsTrue(c.Hold(1, 1f, 20f, 6f), "re-press moves at once");
            Assert.IsTrue(c.Hold(-1, 1f, 20f, 6f), "reversing moves at once");
            Assert.AreEqual(1, c.Index);
        }

        [Test]
        public void EmptyMenu_IsSafe()
        {
            var c = new MenuCursor(0);
            c.Move(1);
            c.Select(3);
            Assert.AreEqual(0, c.Index);
        }
    }
}

namespace Margin.Tests
{
    public class KeyIconsTests
    {
        [Test]
        public void Letters_Digits_AndFunctionKeys()
        {
            Assert.AreEqual("f", KeyIcons.ForPath("<Keyboard>/f"));
            Assert.AreEqual("j", KeyIcons.ForPath("<Keyboard>/j"));
            Assert.AreEqual("7", KeyIcons.ForPath("<Keyboard>/digit7"));
            Assert.AreEqual("f12", KeyIcons.ForPath("<Keyboard>/f12"));
            Assert.IsNull(KeyIcons.ForPath("<Keyboard>/f13"));
        }

        [Test]
        public void RenamedKeys_UseThePacksFileNames()
        {
            Assert.AreEqual("esc", KeyIcons.ForPath("<Keyboard>/escape"));
            Assert.AreEqual("space", KeyIcons.ForPath("<Keyboard>/space"));
            Assert.AreEqual("shift", KeyIcons.ForPath("<Keyboard>/leftShift"));
            Assert.AreEqual("arrow-up", KeyIcons.ForPath("<Keyboard>/upArrow"));
            Assert.AreEqual("backspace", KeyIcons.ForPath("<Keyboard>/backspace"));
        }

        [Test]
        public void MouseButtons_HaveIcons_GamepadDoesNot()
        {
            Assert.AreEqual("mouse-left", KeyIcons.ForPath("<Mouse>/leftButton"));
            Assert.AreEqual("mouse-right", KeyIcons.ForPath("<Mouse>/rightButton"));
            Assert.IsNull(KeyIcons.ForPath("<Gamepad>/buttonSouth"));
            Assert.IsNull(KeyIcons.ForPath(""));
            Assert.IsNull(KeyIcons.ForPath("<Keyboard>/"));
        }

        [Test]
        public void Composites_MapToTheClusterPictures()
        {
            Assert.AreEqual("keyboard-wasd", KeyIcons.ForComposite(new[] { "<Keyboard>/w", "<Keyboard>/s", "<Keyboard>/a", "<Keyboard>/d" }));
            Assert.AreEqual("keyboard-arrows", KeyIcons.ForComposite(new[]
                { "<Keyboard>/upArrow", "<Keyboard>/downArrow", "<Keyboard>/leftArrow", "<Keyboard>/rightArrow" }));
            Assert.IsNull(KeyIcons.ForComposite(new[] { "<Keyboard>/i", "<Keyboard>/k", "<Keyboard>/j", "<Keyboard>/l" }));
        }
    }
}
