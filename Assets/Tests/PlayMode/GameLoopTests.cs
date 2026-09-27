using System.Collections;
using Margin.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Margin.Tests
{
    /// <summary>Pause and frame-step (F3/F4) must freeze gameplay exactly and advance one tick per step.</summary>
    public class GameLoopTests
    {
        private sealed class TickCounter : ITickable
        {
            public int TickOrder => 0;
            public int Ticks;
            public void Tick() => Ticks++;
        }

        private GameObject loopObject;
        private TickCounter counter;

        [SetUp]
        public void SetUp()
        {
            loopObject = new GameObject("TestGameLoop");
            loopObject.AddComponent<GameLoop>();
            counter = new TickCounter();
            GameLoop.Register(counter);
        }

        [TearDown]
        public void TearDown()
        {
            GameLoop.Unregister(counter);
            GameLoop.Paused = false;
            Object.DestroyImmediate(loopObject);
        }

        private static IEnumerator FixedUpdates(int count)
        {
            for (int i = 0; i < count; i++) yield return new WaitForFixedUpdate();
        }

        [UnityTest]
        public IEnumerator Pause_FreezesTicks_StepRunsExactlyOne_ResumeContinues()
        {
            yield return FixedUpdates(3);
            Assert.Greater(counter.Ticks, 0, "Should tick while running.");

            GameLoop.Paused = true;
            int ticksAtPause = counter.Ticks;
            int frameAtPause = GameLoop.Clock.CurrentFrame;
            yield return FixedUpdates(5);
            Assert.AreEqual(ticksAtPause, counter.Ticks, "No ticks while paused.");
            Assert.AreEqual(frameAtPause, GameLoop.Clock.CurrentFrame, "Frame counter frozen while paused.");

            GameLoop.Step();
            yield return FixedUpdates(4);
            Assert.AreEqual(ticksAtPause + 1, counter.Ticks, "One step = exactly one tick.");
            Assert.AreEqual(frameAtPause + 1, GameLoop.Clock.CurrentFrame);

            GameLoop.Step();
            GameLoop.Step();
            yield return FixedUpdates(4);
            Assert.AreEqual(ticksAtPause + 3, counter.Ticks, "Two quick steps = two ticks.");

            GameLoop.Paused = false;
            yield return FixedUpdates(3);
            Assert.Greater(counter.Ticks, ticksAtPause + 3, "Resumes ticking after unpause.");
        }
    }
}
