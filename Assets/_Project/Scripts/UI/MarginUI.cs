using Margin.Core;
using Margin.Player;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace Margin.UI
{
    /// <summary>
    /// The game's UI root (spec 14): one UI Toolkit document, built entirely from code, that holds the HUD.
    /// It scales with the screen (reference 1920x1080) and draws its hand-drawn look with InkPainter, so there are
    /// no UXML/USS files to maintain. Added to gameplay scenes by the gym/arena builders, or automatically at play
    /// time in any scene with a GameLoop.
    /// </summary>
    [DefaultExecutionOrder(100)]   // after gameplay, so it shows this frame's values
    public sealed class MarginUI : MonoBehaviour
    {
        private const string SettingsPath = "Assets/_Project/Data/UI/UISettings.asset";

        [SerializeField] private UISettings settings;

        private PanelSettings panel;
        private UIDocument document;
        private HudView hud;
        private PlayerController player;
        private float nextSearch;

        public UISettings Settings => settings;

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
            hud = new HudView(root, settings);
        }

        private void OnDestroy()
        {
            if (panel != null) Destroy(panel);
        }

        private void Update()
        {
            if (player == null && Time.unscaledTime >= nextSearch)
            {
                nextSearch = Time.unscaledTime + 0.5f;
                player = SceneQuery.FindFirst<PlayerController>();
            }

            // UI animation follows the game: nothing drains while paused or frame-stepping.
            float frames = GameLoop.Paused ? 0f : Time.unscaledDeltaTime * 60f;
            hud.Update(player, frames);
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
#endif
            return UISettings.Defaults;
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
