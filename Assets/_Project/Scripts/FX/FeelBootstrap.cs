using Margin.Core;
using Margin.Player;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Margin.FX
{
    /// <summary>
    /// Makes sure every gameplay scene (one with a GameLoop) has its feel effects, even if the scene was set up
    /// before they existed: an InkSplatter, CameraShake on the main camera, and PlayerFX on each player.
    /// Scenes built by the gym builder already have them (with the FeelSettings asset); this only fills gaps,
    /// using default settings.
    /// </summary>
    public static class FeelBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Initialize()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            EnsureEffects();
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => EnsureEffects();

        public static void EnsureEffects()
        {
            if (GameLoop.Clock == null) return;

            if (SceneQuery.FindFirst<InkSplatter>() == null)
                new GameObject("[InkSplatter]").AddComponent<InkSplatter>();

            Camera cam = Camera.main;
            if (cam != null && cam.GetComponent<CameraShake>() == null) cam.gameObject.AddComponent<CameraShake>();

            foreach (PlayerController player in SceneQuery.FindAll<PlayerController>())
                if (player.GetComponent<PlayerFX>() == null) player.gameObject.AddComponent<PlayerFX>();
        }
    }
}
