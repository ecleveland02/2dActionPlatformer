using Margin.Core;
using Margin.Player;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Margin.DebugTools
{
    /// <summary>
    /// Debug hotkeys (spec 3.3):
    ///   F1 collision box view, F2 frame data overlay, F3 pause/resume, F4 step one tick, F5 slow motion.
    ///
    /// Creates itself automatically in the Editor and development builds whenever a scene with a GameLoop
    /// loads, so no scene setup is needed. Pause and step go through GameLoop, so the frame counter and
    /// buffered inputs freeze too: press a key while paused, then F4, and it is handled on that exact tick.
    /// </summary>
    public sealed class DebugController : MonoBehaviour
    {
        [Tooltip("Time scale used by the F5 slow motion toggle (spec: 0.25x).")]
        [SerializeField, Range(0.05f, 1f)] private float slowMotionScale = 0.25f;

        private FrameDataOverlay overlay;
        private HitboxVisualizer boxes;
        private PlayerController player;
        private float nextPlayerSearch;

        public bool SlowMotion { get; private set; }
        public float SlowMotionScale => slowMotionScale;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoCreate()
        {
            // Editor counts as a debug build. Only gameplay scenes (with a GameLoop) get the tools.
            if (!Debug.isDebugBuild || GameLoop.Clock == null) return;
            if (FindObjectOfTypeInScenes<DebugController>() != null) return;

            var go = new GameObject("[DebugTools]");
            DontDestroyOnLoad(go);
            go.AddComponent<DebugController>();
        }

        private void Awake()
        {
            overlay = gameObject.AddComponent<FrameDataOverlay>();
            boxes = gameObject.AddComponent<HitboxVisualizer>();
            overlay.Controller = this;
            overlay.Visible = true;     // on by default so the tools are discoverable
            boxes.Visible = false;
        }

        private void OnDisable()
        {
            // Never leave the game paused or slowed after the tools go away.
            GameLoop.Paused = false;
            if (SlowMotion) Time.timeScale = 1f;
        }

        private void Update()
        {
            FindPlayerIfNeeded();

            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) return;

            if (keyboard.f1Key.wasPressedThisFrame) boxes.Visible = !boxes.Visible;
            if (keyboard.f2Key.wasPressedThisFrame) overlay.Visible = !overlay.Visible;
            if (keyboard.f3Key.wasPressedThisFrame) GameLoop.Paused = !GameLoop.Paused;
            if (keyboard.f4Key.wasPressedThisFrame) GameLoop.Step();
            if (keyboard.f5Key.wasPressedThisFrame)
            {
                SlowMotion = !SlowMotion;
                Time.timeScale = SlowMotion ? slowMotionScale : 1f;
            }
        }

        private void FindPlayerIfNeeded()
        {
            if (player != null || Time.unscaledTime < nextPlayerSearch) return;
            nextPlayerSearch = Time.unscaledTime + 1f;

            player = FindObjectOfTypeInScenes<PlayerController>();
            overlay.Player = player;
            boxes.Player = player;
        }

        /// <summary>Searches loaded scenes (works the same in every Unity version, unlike FindObjectOfType).</summary>
        public static T FindObjectOfTypeInScenes<T>() where T : Component
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (!scene.isLoaded) continue;
                foreach (GameObject root in scene.GetRootGameObjects())
                {
                    T found = root.GetComponentInChildren<T>(true);
                    if (found != null) return found;
                }
            }
            return null;
        }
    }
}
