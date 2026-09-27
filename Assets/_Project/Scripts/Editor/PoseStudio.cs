using Margin.Rendering;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Margin.EditorTools
{
    /// <summary>
    /// Menu: Margin > Open Pose Studio. Opens (creating it the first time) a scene with one stick figure
    /// on a paper background, framed for posing, and selects the rig so the pose editor is ready.
    /// </summary>
    public static class PoseStudio
    {
        private const string ScenePath = "Assets/_Project/Scenes/PoseStudio.unity";
        private const string ProportionsPath = "Assets/_Project/Data/StickFigureProportions.asset";

        [MenuItem("Margin/Open Pose Studio")]
        public static void Open()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null)
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            else
                Create();

            StickFigureRig rig = null;
            foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
                if ((rig = root.GetComponentInChildren<StickFigureRig>()) != null) break;
            if (rig != null)
            {
                Selection.activeGameObject = rig.gameObject;
                FrameSceneView();
            }
        }

        private static void Create()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Load assets AFTER the new scene exists (opening a scene unloads unreferenced assets).
            var proportions = GymBuilder.LoadOrCreate<StickFigureProportions>(ProportionsPath);
            Material ink = GymBuilder.LoadOrCreateInkMaterial();

            var camObject = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = camObject.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 1.3f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color32(0xF2, 0xEE, 0xE3, 0xFF);
            camObject.transform.position = new Vector3(0f, 0f, -10f);

            // Guide lines: ground at foot level and a faint center line.
            var guides = new GameObject("Guides").transform;
            GuideLine(guides, "Ground", new Vector3(-1.5f, -0.9f), new Vector3(1.5f, -0.9f), 0.02f, new Color(0.3f, 0.3f, 0.3f), ink);
            GuideLine(guides, "Center", new Vector3(0f, -0.9f), new Vector3(0f, 1.0f), 0.006f, new Color(0.75f, 0.75f, 0.75f), ink);

            var figure = new GameObject("StickFigure");
            var rig = figure.AddComponent<StickFigureRig>();
            rig.Proportions = proportions;
            rig.LineMaterial = ink;
            rig.Build();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log("Pose Studio created at " + ScenePath + ". Select the StickFigure and click a joint to start posing.");
        }

        private static void GuideLine(Transform parent, string name, Vector3 a, Vector3 b, float width, Color color, Material material)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var line = go.AddComponent<LineRenderer>();
            line.sharedMaterial = material;
            line.useWorldSpace = true;
            line.positionCount = 2;
            line.SetPosition(0, a);
            line.SetPosition(1, b);
            line.widthMultiplier = width;
            line.startColor = line.endColor = color;
            line.sortingOrder = 0;
        }

        private static void FrameSceneView()
        {
            SceneView view = SceneView.lastActiveSceneView;
            if (view == null) return;
            view.in2DMode = true;
            view.LookAt(Vector3.zero, Quaternion.identity, 1.4f);
        }
    }
}
