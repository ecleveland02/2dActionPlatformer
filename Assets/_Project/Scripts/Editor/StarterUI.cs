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
            AssetDatabase.SaveAssets();
            return settings;
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
