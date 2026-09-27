using Margin.Core;
using Margin.UI;
using NUnit.Framework;
using UnityEngine;

namespace Margin.Tests
{
    /// <summary>The pause menu freezes the game and gives back exactly what it took on resume.</summary>
    public class PauseMenuTests
    {
        private GameObject go;

        [SetUp]
        public void SetUp()
        {
            GameLoop.Paused = false;
            Time.timeScale = 1f;
        }

        [TearDown]
        public void TearDown()
        {
            if (go != null) Object.DestroyImmediate(go);
            GameLoop.Paused = false;
            Time.timeScale = 1f;
        }

        private MarginUI MakeUI()
        {
            go = new GameObject("[UI]");
            return go.AddComponent<MarginUI>();
        }

        [Test]
        public void Pause_StopsTheGame_AndResumeRestoresSlowMotion()
        {
            Time.timeScale = 0.25f;   // F5 slow motion was on
            MarginUI ui = MakeUI();

            ui.Pause();
            Assert.IsTrue(ui.IsPaused);
            Assert.IsTrue(GameLoop.Paused);
            Assert.AreEqual(0f, Time.timeScale);

            ui.Resume();
            Assert.IsFalse(ui.IsPaused);
            Assert.IsFalse(GameLoop.Paused);
            Assert.AreEqual(0.25f, Time.timeScale, 1e-6f);
        }

        [Test]
        public void Resume_KeepsADebugPauseThatWasAlreadyOn()
        {
            GameLoop.Paused = true;   // F3 frame-step pause
            MarginUI ui = MakeUI();
            ui.Pause();
            ui.Resume();
            Assert.IsTrue(GameLoop.Paused);
        }

        [Test]
        public void DisablingTheUI_WhilePaused_Resumes()
        {
            MarginUI ui = MakeUI();
            ui.Pause();
            go.SetActive(false);
            Assert.IsFalse(GameLoop.Paused);
            Assert.AreEqual(1f, Time.timeScale);
        }
    }
}
