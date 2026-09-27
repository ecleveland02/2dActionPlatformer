using Margin.Audio;
using Margin.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Margin.EditorTools
{
    /// <summary>
    /// Menu: Margin > Build Title Screen. Generates Scenes/Title.unity: a paper-colored camera, the TitleScreen
    /// (Play / Controls / Quit on a notebook page) and an AudioDirector for the world music and menu clicks.
    /// Puts it first in Build Settings so the built game opens on it; Play loads World 1.
    /// </summary>
    public static partial class GymBuilder
    {
        private const string TitlePath = "Assets/_Project/Scenes/Title.unity";

        [MenuItem("Margin/Build Title Screen")]
        public static void BuildTitleScreen()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(TitlePath) != null &&
                !EditorUtility.DisplayDialog("Build Title Screen", "Title.unity already exists. Rebuild and overwrite it?", "Rebuild", "Cancel"))
                return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            GymAssets assets = LoadAssets();   // after NewScene, so nothing is unloaded with the old scene
            if (assets == null) return;

            var camObject = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = camObject.AddComponent<Camera>();
            cam.orthographic = true;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Paper;
            camObject.transform.position = new Vector3(0f, 0f, -10f);
            camObject.AddComponent<AudioListener>();

            var title = new GameObject("Title Screen").AddComponent<TitleScreen>();
            SetReference(title, "settings", assets.UI);
            SetReference(title, "controls", assets.Controls);

            var audio = new GameObject("[Audio]").AddComponent<AudioDirector>();
            SetReference(audio, "bank", assets.Sound);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, TitlePath);
            AddToBuildSettingsFirst(TitlePath);
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(WorldPath) != null) AddToBuildSettingsFirst(WorldPath);   // right after Title
            Debug.Log("Title screen built at " + TitlePath + " (first in Build Settings). Press Play.");
        }
    }
}
