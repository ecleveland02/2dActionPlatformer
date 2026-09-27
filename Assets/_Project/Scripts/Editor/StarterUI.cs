using Margin.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Margin.EditorTools
{
    /// <summary>
    /// Creates the UI data: Data/UI/UISettings.asset (colors, line wobble, HUD and menu sizes) and a tiny
    /// UI Toolkit theme file it points to. Called by the gym/arena builders; never overwrites existing tuning.
    /// </summary>
    public static class StarterUI
    {
        private const string Folder = "Assets/_Project/Data/UI";
        private const string SettingsPath = Folder + "/UISettings.asset";
        private const string ThemePath = Folder + "/MarginTheme.tss";
        private const string IconFolder = "Assets/_Project/Art/UI/UIElements/UI Elements/White/2x";
        private static readonly string[] KeyIconFolders =
        {
            "Assets/_Project/Art/UI/InputIcons/keyboard/keyboard-outlined",
            "Assets/_Project/Art/UI/InputIcons/mouse/mouse-outlined",
        };

        public static UISettings EnsureCreated()
        {
            StickFigureRigEditor.EnsureFolder(Folder);

            // A theme that just imports Unity's default runtime theme. The ink look is drawn in code; the theme
            // only keeps UI Toolkit from warning that the panel has none.
            if (AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(ThemePath) == null)
            {
                System.IO.File.WriteAllText(ThemePath, "@import url(\"unity-theme://default\");\n");
                AssetDatabase.ImportAsset(ThemePath);
            }

            UISettings settings = GymBuilder.LoadOrCreate<UISettings>(SettingsPath);
            if (settings.theme == null)
            {
                settings.theme = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(ThemePath);
                EditorUtility.SetDirty(settings);
            }
            if (FillIcons(settings)) EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
            return settings;
        }

        /// <summary>
        /// Points the empty icon slots at the Art/UI packs (white pictures, tinted in code). Slots you've set
        /// yourself are left alone. Returns true if anything changed.
        /// </summary>
        private static bool FillIcons(UISettings settings)
        {
            bool changed = false;
            Texture2D Icon(string file) => AssetDatabase.LoadAssetAtPath<Texture2D>($"{IconFolder}/{file}.png");
            void Fill(ref Texture2D slot, string file)
            {
                if (slot != null) return;
                slot = Icon(file);
                changed |= slot != null;
            }
            Fill(ref settings.iconResume, "right arrow");
            Fill(ref settings.iconRestart, "backward");
            Fill(ref settings.iconControls, "gamepad1");
            Fill(ref settings.iconQuit, "right exit");
            Fill(ref settings.iconBack, "left arrow");
            Fill(ref settings.iconPlay, "forward");
            Fill(ref settings.iconOptions, "settings");
            Fill(ref settings.iconHome, "home");

            // Every key/mouse picture, named by file ("f", "space", "mouse-left"); KeyIcons maps bindings to names.
            var known = new System.Collections.Generic.HashSet<string>();
            foreach (UISettings.KeyIcon k in settings.keyIcons) known.Add(k.name);
            foreach (string folder in KeyIconFolders)
            {
                if (!AssetDatabase.IsValidFolder(folder)) continue;
                foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { folder }))
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    string name = System.IO.Path.GetFileNameWithoutExtension(path);
                    if (!known.Add(name)) continue;
                    settings.keyIcons.Add(new UISettings.KeyIcon { name = name, icon = AssetDatabase.LoadAssetAtPath<Texture2D>(path) });
                    changed = true;
                }
            }
            return changed;
        }

        /// <summary>Adds the UI root ([UI] with MarginUI) to the open scene, or points an existing one at the settings.</summary>
        public static void EnsureInScene(UISettings settings)
        {
            MarginUI ui = Margin.Core.SceneQuery.FindFirst<MarginUI>();
            if (ui == null) ui = new GameObject("[UI]").AddComponent<MarginUI>();
            var so = new SerializedObject(ui);
            so.FindProperty("settings").objectReferenceValue = settings;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        [MenuItem("Margin/Add HUD and Pause Menu")]
        private static void AddToScene()
        {
            EnsureInScene(EnsureCreated());
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
                UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            Debug.Log("Margin: [UI] added (HUD + pause menu). Tune it in Data/UI/UISettings.");
        }
    }
}
