using Margin.Core;
using Margin.Input;
using Margin.Level;
using Margin.Player;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace Margin.UI
{
    /// <summary>
    /// The game's UI root (spec 14): one UI Toolkit document, built entirely from code, that holds the HUD and the
    /// pause menu (Esc / Start). Pausing stops the GameLoop and game time and turns gameplay input off; resuming
    /// puts everything back exactly as it was (including the F3 debug pause and F5 slow motion).
    /// Room transitions, pit falls and respawns fade the level to paper (LevelDirector.Fade) under the HUD.
    /// It scales with the screen (reference 1920x1080) and draws its hand-drawn look with InkPainter, so there are
    /// no UXML/USS files to maintain. Added to gameplay scenes by the gym/arena builders, or automatically at play
    /// time in any scene with a GameLoop.
    /// </summary>
    [DefaultExecutionOrder(100)]   // after gameplay, so it shows this frame's values
    public sealed class MarginUI : MonoBehaviour
    {
        private const string SettingsPath = "Assets/_Project/Data/UI/UISettings.asset";
        private const string ThemePath = "Assets/_Project/Data/UI/MarginTheme.tss";

        [SerializeField] private UISettings settings;

        private PanelSettings panel;
        private UIDocument document;
        private HudView hud;
        private VisualElement fade;
        private float shownFade = -1f;
        private PauseMenuView pauseMenu;
        private PlayerController player;
        private InputReader reader;
        private InputAction pauseAction, navigateAction, submitAction, cancelAction;
        private float nextSearch;
        private bool warnedNoMenuMap;

        // What pausing changed, to restore on resume.
        private bool wasPaused;
        private float timeScaleBefore = 1f;

        public UISettings Settings => settings;
        public bool IsPaused => pauseMenu != null && pauseMenu.IsOpen;

        private void Awake()
        {
            if (settings == null) settings = FindSettings();

            // The document lives on a child that starts inactive, so its PanelSettings is set before it enables.
            var host = new GameObject("Document");
            host.SetActive(false);
            host.transform.SetParent(transform, false);
            panel = ScriptableObject.CreateInstance<PanelSettings>();
            panel.hideFlags = HideFlags.DontSave;
            panel.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            panel.referenceResolution = new Vector2Int(1920, 1080);
            panel.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            panel.match = 0.5f;
            panel.themeStyleSheet = settings.theme;
            document = host.AddComponent<UIDocument>();
            document.panelSettings = panel;
            host.SetActive(true);

            VisualElement root = document.rootVisualElement;
            root.pickingMode = PickingMode.Ignore;
            // Room fades cover the level but not the HUD (added first, so it's drawn underneath).
            fade = HudView.Layer(root);
            Color paper = settings.paper;
            paper.a = 1f;
            fade.style.backgroundColor = paper;
            UpdateFade();
            hud = new HudView(root, settings);
            pauseMenu = new PauseMenuView(root, settings, Resume, Restart, Quit);
        }

        private void OnDisable()
        {
            if (IsPaused) Resume();
            if (pauseAction != null) pauseAction.actionMap.Disable();
        }

        private void OnDestroy()
        {
            if (panel != null) Destroy(panel);
        }

        private void Update()
        {
            if ((player == null || reader == null) && Time.unscaledTime >= nextSearch)
            {
                nextSearch = Time.unscaledTime + 0.5f;
                if (player == null) player = SceneQuery.FindFirst<PlayerController>();
                if (reader == null) FindMenuInput();
            }

            float realFrames = Time.unscaledDeltaTime * 60f;
            UpdatePause(realFrames);

            // HUD animation follows the game: nothing drains while paused or frame-stepping.
            hud.Update(player, GameLoop.Paused ? 0f : realFrames);
            UpdateFade();
        }

        private void UpdateFade()
        {
            float f = LevelDirector.Instance != null ? LevelDirector.Instance.Fade : 0f;
            if (Mathf.Approximately(f, shownFade)) return;
            shownFade = f;
            fade.style.opacity = f;
            HudView.SetVisible(fade, f > 0f);
        }

        // ---------------- pause ----------------

        private void UpdatePause(float realFrames)
        {
            if (pauseAction == null) return;

            if (pauseAction.WasPressedThisFrame())
            {
                if (!IsPaused) Pause();
                else if (pauseMenu.Back()) Resume();
                return;
            }

            if (IsPaused && !pauseMenu.Update(navigateAction.ReadValue<Vector2>(), submitAction.WasPressedThisFrame(),
                                              cancelAction.WasPressedThisFrame(), realFrames))
                Resume();
        }

        public void Pause()
        {
            if (IsPaused) return;
            wasPaused = GameLoop.Paused;
            timeScaleBefore = Time.timeScale;
            GameLoop.Paused = true;
            Time.timeScale = 0f;   // particles, trails and camera shake stop too
            if (reader != null) reader.SetGameplayInput(false);
            pauseMenu.Open(reader != null ? reader.Actions : null);
        }

        public void Resume()
        {
            if (!IsPaused) return;
            pauseMenu.Close();
            GameLoop.Paused = wasPaused;
            Time.timeScale = timeScaleBefore;
            if (reader != null) reader.SetGameplayInput(true);
        }

        /// <summary>Back to the spawn point with full health; enemies reset (same as after a death).</summary>
        private void Restart()
        {
            Resume();
            if (player != null && player.Health != null) player.Health.Respawn();
        }

        private void Quit()
        {
            Resume();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private void FindMenuInput()
        {
            reader = SceneQuery.FindFirst<InputReader>();
            InputActionAsset actions = reader != null ? reader.Actions : null;
            if (actions == null) return;
            InputActionMap menu = actions.FindActionMap("Menu");
            if (menu == null)
            {
                if (!warnedNoMenuMap) Debug.LogWarning("MarginUI: the controls asset has no 'Menu' map, so there is no pause menu.", this);
                warnedNoMenuMap = true;
                return;
            }
            pauseAction = menu.FindAction("Pause");
            navigateAction = menu.FindAction("Navigate");
            submitAction = menu.FindAction("Submit");
            cancelAction = menu.FindAction("Cancel");
            menu.Enable();   // always on: Pause must work while gameplay input is off
            hud.SetInput(actions);
        }

        /// <summary>A text label in the UI font, placed absolutely (width &lt; 0 = fit the text).</summary>
        public static Label MakeLabel(UISettings s, string text, int size, Color color, float x, float y, float width = -1f)
        {
            var label = new Label(text) { pickingMode = PickingMode.Ignore };
            label.style.position = Position.Absolute;
            label.style.left = x;
            label.style.top = y;
            if (width >= 0f) label.style.width = width;
            label.style.fontSize = size;
            label.style.color = color;
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            label.style.unityFontDefinition = FontDefinition.FromFont(Font(s));
            label.style.paddingLeft = label.style.paddingRight = label.style.paddingTop = label.style.paddingBottom = 0f;
            label.style.marginLeft = label.style.marginRight = label.style.marginTop = label.style.marginBottom = 0f;
            return label;
        }

        private static Font builtinFont;

        private static Font Font(UISettings s)
        {
            if (s.font != null) return s.font;
            if (builtinFont == null) builtinFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            return builtinFont;
        }

        private static UISettings FindSettings()
        {
#if UNITY_EDITOR
            // Scenes made before the UI existed: use the project's settings asset if there is one.
            var asset = UnityEditor.AssetDatabase.LoadAssetAtPath<UISettings>(SettingsPath);
            if (asset != null) return asset;
            UISettings defaults = UISettings.Defaults;
            if (defaults.theme == null)
                defaults.theme = UnityEditor.AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(ThemePath);
            return defaults;
#else
            return UISettings.Defaults;
#endif
        }

        // ---------------- automatic setup ----------------

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Initialize()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            EnsureUI();
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => EnsureUI();

        /// <summary>Gameplay scenes (with a GameLoop) get a UI root if they don't have one.</summary>
        public static void EnsureUI()
        {
            if (GameLoop.Clock == null) return;
            if (SceneQuery.FindFirst<MarginUI>() == null) new GameObject("[UI]").AddComponent<MarginUI>();
        }
    }
}
