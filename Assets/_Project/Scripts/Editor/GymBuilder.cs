using System.Collections.Generic;
using System.Linq;
using Margin.Core;
using Margin.Input;
using Margin.Level;
using Margin.Physics;
using Margin.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Margin.EditorTools
{
    /// <summary>
    /// Menu: Margin > Build Movement Gym. Generates Assets/_Project/Scenes/Gym.unity from code, so the
    /// test level can be rebuilt any time the layout changes. Also creates any missing data assets.
    ///
    /// Layout (x positions in units, floor top at y = 0; walk right from the spawn):
    ///   -12..20  flat floor, corner-correction ceiling block (x 6-9), coyote ledge (x 12-16, 2 high)
    ///    20..23  3-unit gap with a shallow pit
    ///    23..40  30° up / 45° down hill, then a 60° slope (too steep, acts as a wall)
    ///    40..45  5-unit gap with a shallow pit
    ///    45..78  one-way platform stack (x 48-54), wall-jump chimney (x 61-64) up to a high ledge
    /// </summary>
    public static class GymBuilder
    {
        private const string ScenePath = "Assets/_Project/Scenes/Gym.unity";
        private const string DataFolder = "Assets/_Project/Data";
        private const string InkMaterialPath = "Assets/_Project/Art/InkLine.mat";
        private const string ControlsPath = "Assets/_Project/Scripts/Input/MarginControls.inputactions";

        private static readonly Color Ink = new Color32(0x1A, 0x1A, 0x1A, 0xFF);
        private static readonly Color Pencil = new Color32(0x9A, 0x96, 0x8C, 0xFF);
        private static readonly Color Paper = new Color32(0xF2, 0xEE, 0xE3, 0xFF);
        private const float LineWidth = 0.06f;

        private static Material inkMaterial;

        [MenuItem("Margin/Build Movement Gym")]
        public static void Build()
        {
            int groundLayer = LayerMask.NameToLayer("Ground");
            int oneWayLayer = LayerMask.NameToLayer("OneWayPlatform");
            int playerLayer = LayerMask.NameToLayer("Player");
            if (groundLayer < 0 || oneWayLayer < 0 || playerLayer < 0)
            {
                EditorUtility.DisplayDialog("Build Movement Gym",
                    "Layers Ground, OneWayPlatform and Player must exist (Project Settings > Tags and Layers).", "OK");
                return;
            }

            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null &&
                !EditorUtility.DisplayDialog("Build Movement Gym", "Gym.unity already exists. Rebuild and overwrite it?", "Rebuild", "Cancel"))
                return;

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            // ---- Data assets (created only if missing, so tuning is never overwritten) ----
            var movement = LoadOrCreate<MovementData>($"{DataFolder}/MovementData.asset");
            var bufferSettings = LoadOrCreate<InputBufferSettings>($"{DataFolder}/InputBufferSettings.asset");
            var bodyData = LoadOrCreate<KinematicBodyData>($"{DataFolder}/KinematicBodyData.asset");
            bodyData.solidMask = 1 << groundLayer;
            bodyData.oneWayMask = 1 << oneWayLayer;
            EditorUtility.SetDirty(bodyData);
            inkMaterial = LoadOrCreateInkMaterial();
            Object controls = AssetDatabase.LoadMainAssetAtPath(ControlsPath);
            if (controls == null)
            {
                EditorUtility.DisplayDialog("Build Movement Gym",
                    $"Could not load {ControlsPath}. Select it in the Project window and check the Inspector " +
                    "and Console for an import error.", "OK");
                return;
            }
            Debug.Log($"Gym builder: using controls asset '{controls.name}' ({controls.GetType().Name}).");
            AssetDatabase.SaveAssets();

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            new GameObject("GameLoop").AddComponent<GameLoop>();

            // ---- Level geometry ----
            var level = new GameObject("Level").transform;

            // Floors and boundary walls
            Box(level, "Floor A", -12, -1, 20, 0, groundLayer);
            Box(level, "Wall Left", -13, -1, -12, 14, groundLayer);
            Box(level, "Pit 1", 20, -3, 23, -2, groundLayer);
            Box(level, "Floor B", 23, -1, 40, 0, groundLayer);
            Box(level, "Pit 2", 40, -3, 45, -2, groundLayer);
            Box(level, "Floor C", 45, -1, 78, 0, groundLayer);
            Box(level, "Wall Right", 78, -1, 79, 14, groundLayer);

            // Corner correction: jump so your head clips the left edge of this block.
            Box(level, "Corner Block", 6, 3.5f, 9, 4.5f, groundLayer);
            // Coyote ledge: run off the right edge and jump late.
            Box(level, "Coyote Ledge", 12, 0, 16, 2, groundLayer);

            // Slopes: 30° up (rise 3 over 5.196), plateau, 45° down (rise 3 over 3).
            Polygon(level, "Hill 30-45", groundLayer,
                new Vector2(25, 0), new Vector2(30.196f, 3), new Vector2(33, 3), new Vector2(36, 0),
                new Vector2(36, -0.5f), new Vector2(25, -0.5f));
            // 60° slope: steeper than the 45° limit, so it blocks like a wall. Jump over it.
            Polygon(level, "Steep 60", groundLayer,
                new Vector2(37.5f, 0), new Vector2(38.655f, 2), new Vector2(40, 2),
                new Vector2(40, -0.5f), new Vector2(37.5f, -0.5f));

            // One-way stack: jump up through, Down + Jump to drop.
            OneWay(level, "OneWay 1", 48, 52, 2.5f, oneWayLayer);
            OneWay(level, "OneWay 2", 50, 54, 5f, oneWayLayer);
            OneWay(level, "OneWay 3", 48, 52, 7.5f, oneWayLayer);

            // Wall-jump chimney: walk under the left wall, then wall-jump between x 61 and 64.
            Box(level, "Chimney Left", 60, 2.2f, 61, 12, groundLayer);
            Box(level, "Chimney Right", 64, 0, 65, 10, groundLayer);
            Box(level, "High Ledge", 65, 9.5f, 72, 10, groundLayer);

            // ---- Labels ----
            var labels = new GameObject("Labels").transform;
            Label(labels, "CORNER CORRECTION", 7.5f, 5.2f);
            Label(labels, "COYOTE", 16, 3);
            Label(labels, "GAP 3", 21.5f, 1.5f);
            Label(labels, "SLOPES 30 / 45", 30.5f, 4.5f);
            Label(labels, "60 (WALL)", 39, 3.2f);
            Label(labels, "GAP 5", 42.5f, 1.5f);
            Label(labels, "ONE-WAY  (DOWN+JUMP DROPS)", 51, 9);
            Label(labels, "WALL JUMP", 62.5f, 13);

            // ---- Player ----
            GameObject player = BuildPlayer(playerLayer, movement, bufferSettings, bodyData, controls);

            // ---- Camera ----
            var camObject = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = camObject.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 7f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Paper;
            camObject.transform.position = new Vector3(0, 2, -10);
            camObject.AddComponent<CameraFollow>().Target = player.transform;
            camObject.AddComponent<AudioListener>();

            EditorSceneManager.SaveScene(scene, ScenePath);
            AddToBuildSettings(ScenePath);
            Debug.Log("Movement gym built at " + ScenePath + ". Press Play.");
        }

        private static GameObject BuildPlayer(int layer, MovementData movement, InputBufferSettings bufferSettings,
                                              KinematicBodyData bodyData, Object controls)
        {
            var player = new GameObject("Player") { layer = layer };
            player.transform.position = new Vector3(0, 1.5f, 0);

            // Adding KinematicBody2D also adds the BoxCollider2D and Rigidbody2D it requires.
            var body = player.AddComponent<KinematicBody2D>();
            player.GetComponent<BoxCollider2D>().size = new Vector2(0.6f, 1.8f);
            var rb = player.GetComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.interpolation = RigidbodyInterpolation2D.Interpolate;
            SetReference(body, "data", bodyData);

            var reader = player.AddComponent<InputReader>();
            SetReference(reader, "actions", controls);
            SetReference(reader, "bufferSettings", bufferSettings);

            // Placeholder visual: an ink capsule plus a short "eye" line that shows facing.
            var visual = new GameObject("Visual").transform;
            visual.SetParent(player.transform, false);
            var capsule = AddLine(visual.gameObject, CapsulePoints(0.3f, 0.6f), true, Ink);
            capsule.useWorldSpace = false;
            var eye = new GameObject("Eye");
            eye.transform.SetParent(visual, false);
            AddLine(eye, new[] { new Vector3(0.08f, 0.6f), new Vector3(0.22f, 0.6f) }, false, Ink).useWorldSpace = false;

            var controller = player.AddComponent<PlayerController>();
            SetReference(controller, "data", movement);
            SetReference(controller, "inputReader", reader);
            SetReference(controller, "visualRoot", visual);
            var so = new SerializedObject(controller);
            so.FindProperty("abilities.wallCling").boolValue = true;   // gym has walls unlocked for tuning
            so.ApplyModifiedPropertiesWithoutUndo();

            return player;
        }

        // ---------------- geometry helpers ----------------

        private static void Box(Transform parent, string name, float xMin, float yMin, float xMax, float yMax, int layer)
        {
            var go = new GameObject(name) { layer = layer };
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3((xMin + xMax) / 2f, (yMin + yMax) / 2f, 0);
            var collider = go.AddComponent<BoxCollider2D>();
            collider.size = new Vector2(xMax - xMin, yMax - yMin);

            float hx = collider.size.x / 2f, hy = collider.size.y / 2f;
            AddLine(go, new[] { new Vector3(-hx, -hy), new Vector3(-hx, hy), new Vector3(hx, hy), new Vector3(hx, -hy) }, true, Ink)
                .useWorldSpace = false;
        }

        private static void Polygon(Transform parent, string name, int layer, params Vector2[] points)
        {
            var go = new GameObject(name) { layer = layer };
            go.transform.SetParent(parent, false);
            go.AddComponent<PolygonCollider2D>().points = points;
            AddLine(go, points.Select(p => (Vector3)p).ToArray(), true, Ink).useWorldSpace = false;
        }

        private static void OneWay(Transform parent, string name, float xMin, float xMax, float top, int layer)
        {
            const float thickness = 0.2f;
            var go = new GameObject(name) { layer = layer };
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3((xMin + xMax) / 2f, top - thickness / 2f, 0);
            go.AddComponent<BoxCollider2D>().size = new Vector2(xMax - xMin, thickness);

            // Drawn as a single notebook line on the top surface.
            float hx = (xMax - xMin) / 2f;
            AddLine(go, new[] { new Vector3(-hx, thickness / 2f), new Vector3(hx, thickness / 2f) }, false, Ink)
                .useWorldSpace = false;
        }

        private static void Label(Transform parent, string text, float x, float y)
        {
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font == null) return;

            var go = new GameObject("Label " + text);
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(x, y, 0);
            var mesh = go.AddComponent<TextMesh>();
            mesh.text = text;
            mesh.font = font;
            mesh.fontSize = 48;
            mesh.characterSize = 0.06f;
            mesh.anchor = TextAnchor.MiddleCenter;
            mesh.color = Pencil;
            go.GetComponent<MeshRenderer>().sharedMaterial = font.material;
        }

        private static LineRenderer AddLine(GameObject go, Vector3[] points, bool loop, Color color)
        {
            var line = go.AddComponent<LineRenderer>();
            line.sharedMaterial = inkMaterial;
            line.loop = loop;
            line.positionCount = points.Length;
            line.SetPositions(points);
            line.widthMultiplier = LineWidth;
            line.startColor = color;
            line.endColor = color;
            line.numCapVertices = 4;
            line.numCornerVertices = 3;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
            return line;
        }

        private static Vector3[] CapsulePoints(float radius, float halfSpan)
        {
            const int segments = 12;
            var points = new List<Vector3>();
            // Top half-circle (right to left), then bottom half-circle (left to right).
            for (int i = 0; i <= segments; i++)
            {
                float a = Mathf.PI * i / segments;
                points.Add(new Vector3(Mathf.Cos(a) * radius, halfSpan + Mathf.Sin(a) * radius));
            }
            for (int i = 0; i <= segments; i++)
            {
                float a = Mathf.PI + Mathf.PI * i / segments;
                points.Add(new Vector3(Mathf.Cos(a) * radius, -halfSpan + Mathf.Sin(a) * radius));
            }
            return points.ToArray();
        }

        // ---------------- asset helpers ----------------

        private static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null) return asset;
            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        private static Material LoadOrCreateInkMaterial()
        {
            // URP's unlit sprite shader renders pink under the built-in renderer, so pick by active pipeline.
            bool urpActive = GraphicsSettings.currentRenderPipeline != null;
            Shader shader = urpActive ? Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default") : null;
            if (shader == null) shader = Shader.Find("Sprites/Default");

            var material = AssetDatabase.LoadAssetAtPath<Material>(InkMaterialPath);
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, InkMaterialPath);
            }
            else if (material.shader != shader)
            {
                material.shader = shader;
                EditorUtility.SetDirty(material);
            }
            return material;
        }

        private static void SetReference(Object target, string property, Object value)
        {
            var so = new SerializedObject(target);
            so.FindProperty(property).objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void AddToBuildSettings(string path)
        {
            var scenes = EditorBuildSettings.scenes.ToList();
            if (scenes.Any(s => s.path == path)) return;
            scenes.Add(new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
