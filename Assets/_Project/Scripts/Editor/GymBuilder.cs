using System.Collections.Generic;
using System.Linq;
using Margin.Combat;
using Margin.Core;
using Margin.Enemies;
using Margin.FX;
using Margin.Input;
using Margin.Level;
using Margin.Physics;
using Margin.Player;
using Margin.Rendering;
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
    /// Layout (x positions in units, floor top at y = 0; spawn at x = 0):
    ///   -45..-16 sprint gap to the LEFT of spawn: 8 units wide, too far for a run jump (~6), fine for a
    ///            sprint jump (~8.5). Run left from the spawn to build up sprint first.
    ///   -16..20  flat floor, corner-correction ceiling block (x 6-9), coyote ledge (x 12-16, 2 high)
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

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Load assets AFTER creating the scene. Opening a scene in Single mode unloads assets that
            // nothing references yet, which would leave these variables pointing at unloaded objects
            // (that bug left every Player reference empty in the first version of this builder).
            GymAssets assets = LoadAssets();
            if (assets == null) return;
            inkMaterial = assets.Ink;

            new GameObject("GameLoop").AddComponent<GameLoop>();

            // ---- Level geometry ----
            var level = new GameObject("Level").transform;

            // Floors and boundary walls
            Box(level, "Wall Left", -46, -1, -45, 14, groundLayer);
            Box(level, "Floor A0", -45, -1, -24, 0, groundLayer);
            Box(level, "Pit 0", -24, -3, -16, -2, groundLayer);    // 8-unit sprint gap
            Box(level, "Floor A", -16, -1, 20, 0, groundLayer);
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
            Label(labels, "<- SPRINT GAP 8", -20, 1.5f);
            Label(labels, "CORNER CORRECTION", 7.5f, 5.2f);
            Label(labels, "COYOTE", 16, 3);
            Label(labels, "GAP 3", 21.5f, 1.5f);
            Label(labels, "SLOPES 30 / 45", 30.5f, 4.5f);
            Label(labels, "60 (WALL)", 39, 3.2f);
            Label(labels, "GAP 5", 42.5f, 1.5f);
            Label(labels, "ONE-WAY  (DOWN+JUMP DROPS)", 51, 9);
            Label(labels, "WALL JUMP", 62.5f, 13);

            // ---- Player and training dummy ----
            GameObject player = BuildPlayer(playerLayer, assets);
            BuildDummy(new Vector3(4f, 1.5f, 0f), assets);
            BuildDummy(new Vector3(-7f, 1.5f, 0f), assets, sparring: true);

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
            EnsureSceneEffects(assets);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AddToBuildSettings(ScenePath);
            Debug.Log("Movement gym built at " + ScenePath + ". Press Play.");
        }

        /// <summary>
        /// Menu: Margin > Wire Player References. Fills in every data/asset slot on the player(s) in the open
        /// scene from Assets/_Project/Data, without rebuilding the scene. Existing references are replaced.
        /// Also upgrades the old capsule placeholder to the animated stick figure.
        /// </summary>
        [MenuItem("Margin/Wire Player References")]
        public static void WirePlayerReferences()
        {
            Scene scene = SceneManager.GetActiveScene();
            var controllers = new List<PlayerController>();
            foreach (GameObject root in scene.GetRootGameObjects())
                controllers.AddRange(root.GetComponentsInChildren<PlayerController>(true));

            if (controllers.Count == 0)
            {
                EditorUtility.DisplayDialog("Wire Player References", "No PlayerController found in the open scene.", "OK");
                return;
            }

            GymAssets assets = LoadAssets();
            if (assets == null) return;

            foreach (PlayerController controller in controllers)
            {
                WirePlayer(controller, controller.GetComponent<InputReader>(),
                           controller.GetComponent<KinematicBody2D>(), null, assets);
            }
            EnsureSceneEffects(assets);

            EditorSceneManager.MarkSceneDirty(scene);
            if (!string.IsNullOrEmpty(scene.path)) EditorSceneManager.SaveScene(scene);
            Debug.Log($"Wired references on {controllers.Count} player(s) and saved {scene.name}. Press Play.");
        }

        /// <summary>The assets a gym player needs, loaded (or created) from the project.</summary>
        private sealed class GymAssets
        {
            public MovementData Movement;
            public InputBufferSettings BufferSettings;
            public KinematicBodyData BodyData;
            public Object Controls;
            public Material Ink;
            public StickFigureProportions Proportions;
            public PlayerAnimationSet Animations;
            public StarterCombat.Result Combat;
            public FeelSettings Feel;
        }

        /// <summary>Loads the data assets, creating any that are missing (existing tuning is never overwritten).</summary>
        private static GymAssets LoadAssets()
        {
            var assets = new GymAssets
            {
                Movement = LoadOrCreate<MovementData>($"{DataFolder}/MovementData.asset"),
                BufferSettings = LoadOrCreate<InputBufferSettings>($"{DataFolder}/InputBufferSettings.asset"),
                BodyData = LoadOrCreate<KinematicBodyData>($"{DataFolder}/KinematicBodyData.asset"),
                Controls = AssetDatabase.LoadMainAssetAtPath(ControlsPath),
                Ink = LoadOrCreateInkMaterial(),
                Proportions = LoadOrCreate<StickFigureProportions>($"{DataFolder}/StickFigureProportions.asset"),
                Animations = StarterPoses.EnsureCreated(),   // poses, clips and the set; only adds what's missing
                Combat = StarterCombat.EnsureCreated(),      // attacks, Brush Katana, combat settings
                Feel = LoadOrCreate<FeelSettings>($"{DataFolder}/FeelSettings.asset"),
            };

            if (assets.Controls == null)
            {
                EditorUtility.DisplayDialog("Margin",
                    $"Could not load {ControlsPath}. Select it in the Project window and check the Inspector " +
                    "and Console for an import error.", "OK");
                return null;
            }

            // Masks come from layer names so they stay right even if layer numbers change.
            assets.BodyData.solidMask = LayerMask.GetMask("Ground");
            assets.BodyData.oneWayMask = LayerMask.GetMask("OneWayPlatform");
            EditorUtility.SetDirty(assets.BodyData);
            AssetDatabase.SaveAssets();
            return assets;
        }

        /// <summary>Assigns every asset slot on a player's components and logs one summary line.</summary>
        private static void WirePlayer(PlayerController controller, InputReader reader, KinematicBody2D body,
                                       Transform visual, GymAssets assets)
        {
            bool ok = true;
            if (body != null) ok &= SetReference(body, "data", assets.BodyData);
            if (reader != null)
            {
                ok &= SetReference(reader, "actions", assets.Controls);
                ok &= SetReference(reader, "bufferSettings", assets.BufferSettings);
                ok &= SetReference(controller, "inputReader", reader);
            }
            ok &= SetReference(controller, "data", assets.Movement);
            ok &= WireStickFigure(controller, visual, assets);

            string summary = $"{controller.name}: MovementData={assets.Movement.name}, KinematicBodyData={assets.BodyData.name}, " +
                             $"Controls={assets.Controls.name}, BufferSettings={assets.BufferSettings.name}, " +
                             $"Animations={assets.Animations.name}";
            if (ok) Debug.Log("References wired. " + summary, controller);
            else Debug.LogError("Some references did not stick (see errors above). " + summary, controller);
        }

        /// <summary>
        /// Makes sure the player's visual is the animated stick figure: StickFigureRig + PoseAnimator on the
        /// Visual child, PlayerAnimator on the player. Removes the old capsule placeholder if it is still there.
        /// <paramref name="visual"/> may be null: the controller's current visual root (or a child named Visual) is used.
        /// </summary>
        private static bool WireStickFigure(PlayerController controller, Transform visual, GymAssets assets)
        {
            if (visual == null)
            {
                visual = new SerializedObject(controller).FindProperty("visualRoot").objectReferenceValue as Transform;
                if (visual == null) visual = controller.transform.Find("Visual");
                if (visual == null)
                {
                    visual = new GameObject("Visual").transform;
                    visual.SetParent(controller.transform, false);
                }
            }

            // Old placeholder: a capsule LineRenderer on Visual plus an "Eye" child.
            var capsule = visual.GetComponent<LineRenderer>();
            if (capsule != null) Object.DestroyImmediate(capsule);
            Transform eye = visual.Find("Eye");
            if (eye != null) Object.DestroyImmediate(eye.gameObject);
            visual.localRotation = Quaternion.identity;

            var rig = visual.GetComponent<StickFigureRig>();
            if (rig == null) rig = visual.gameObject.AddComponent<StickFigureRig>();
            bool ok = SetReference(rig, "proportions", assets.Proportions);
            ok &= SetReference(rig, "lineMaterial", assets.Ink);
            if (!rig.IsBuilt) rig.Build();

            var poseAnimator = visual.GetComponent<PoseAnimator>();
            if (poseAnimator == null) poseAnimator = visual.gameObject.AddComponent<PoseAnimator>();
            ok &= SetReference(poseAnimator, "rig", rig);

            var playerAnimator = controller.GetComponent<PlayerAnimator>();
            if (playerAnimator == null) playerAnimator = controller.gameObject.AddComponent<PlayerAnimator>();
            ok &= SetReference(playerAnimator, "animations", assets.Animations);
            ok &= SetReference(playerAnimator, "poseAnimator", poseAnimator);

            ok &= SetReference(controller, "visualRoot", visual);
            ok &= WireCombat(controller, visual, rig, assets);
            return ok;
        }

        /// <summary>Screen shake on the main camera and one ink splatter emitter, both using the FeelSettings asset.</summary>
        private static void EnsureSceneEffects(GymAssets assets)
        {
            Camera cam = null;
            foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
                if ((cam = root.GetComponentInChildren<Camera>()) != null) break;
            if (cam != null)
            {
                var shake = cam.GetComponent<CameraShake>();
                if (shake == null) shake = cam.gameObject.AddComponent<CameraShake>();
                SetReference(shake, "settings", assets.Feel);
            }

            InkSplatter splatter = null;
            foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
                if ((splatter = root.GetComponentInChildren<InkSplatter>()) != null) break;
            if (splatter == null) splatter = new GameObject("[InkSplatter]").AddComponent<InkSplatter>();
            SetReference(splatter, "settings", assets.Feel);
        }

        /// <summary>Adds PlayerCombat (Brush Katana), PlayerFX, the blade line and the player's hurtbox.</summary>
        private static bool WireCombat(PlayerController controller, Transform visual, StickFigureRig rig, GymAssets assets)
        {
            Transform bladeObject = visual.Find("Blade");
            if (bladeObject == null)
            {
                bladeObject = new GameObject("Blade").transform;
                bladeObject.SetParent(visual, false);
            }
            var blade = bladeObject.GetComponent<WeaponLine>();
            if (blade == null) blade = bladeObject.gameObject.AddComponent<WeaponLine>();
            bladeObject.GetComponent<LineRenderer>().sharedMaterial = assets.Ink;
            bool ok = SetReference(blade, "rig", rig);

            var combat = controller.GetComponent<PlayerCombat>();
            if (combat == null) combat = controller.gameObject.AddComponent<PlayerCombat>();
            ok &= SetReference(combat, "weapon", assets.Combat.Weapon);
            ok &= SetReference(combat, "settings", assets.Combat.Settings);
            ok &= SetReference(combat, "weaponLine", blade);

            var fx = controller.GetComponent<PlayerFX>();
            if (fx == null) fx = controller.gameObject.AddComponent<PlayerFX>();
            ok &= SetReference(fx, "settings", assets.Feel);

            if (controller.GetComponent<PlayerHealth>() == null) controller.gameObject.AddComponent<PlayerHealth>();

            var hurtbox = controller.GetComponent<Hurtbox>();
            if (hurtbox == null) hurtbox = controller.gameObject.AddComponent<Hurtbox>();
            hurtbox.Configure(Faction.Player, Vector2.zero, new Vector2(0.6f, 1.8f));
            EditorUtility.SetDirty(hurtbox);
            return ok;
        }

        /// <summary>Menu: Margin > Add Training Dummy. Puts a dummy 4 units in front of the player in the open scene.</summary>
        [MenuItem("Margin/Add Training Dummy")]
        public static void AddTrainingDummy()
        {
            Scene scene = SceneManager.GetActiveScene();
            PlayerController player = null;
            foreach (GameObject root in scene.GetRootGameObjects())
                if ((player = root.GetComponentInChildren<PlayerController>(true)) != null) break;

            GymAssets assets = LoadAssets();
            if (assets == null) return;

            Vector3 at = player != null ? player.transform.position + new Vector3(4f, 0.5f, 0f) : new Vector3(4f, 1.5f, 0f);
            GameObject dummy = BuildDummy(at, assets);
            Selection.activeGameObject = dummy;
            EditorSceneManager.MarkSceneDirty(scene);
            if (!string.IsNullOrEmpty(scene.path)) EditorSceneManager.SaveScene(scene);
        }

        /// <summary>Menu: Margin > Add Sparring Dummy. A dummy that attacks (jab, jab, unparryable smash) for parry practice.</summary>
        [MenuItem("Margin/Add Sparring Dummy")]
        public static void AddSparringDummy()
        {
            Scene scene = SceneManager.GetActiveScene();
            PlayerController player = null;
            foreach (GameObject root in scene.GetRootGameObjects())
                if ((player = root.GetComponentInChildren<PlayerController>(true)) != null) break;

            GymAssets assets = LoadAssets();
            if (assets == null) return;

            Vector3 at = player != null ? player.transform.position + new Vector3(-6f, 0.5f, 0f) : new Vector3(-6f, 1.5f, 0f);
            GameObject dummy = BuildDummy(at, assets, sparring: true);
            Selection.activeGameObject = dummy;
            EditorSceneManager.MarkSceneDirty(scene);
            if (!string.IsNullOrEmpty(scene.path)) EditorSceneManager.SaveScene(scene);
        }

        private static GameObject BuildDummy(Vector3 position, GymAssets assets, bool sparring = false)
        {
            var dummy = new GameObject(sparring ? "Sparring Dummy" : "Training Dummy");
            dummy.transform.position = position;
            var body = dummy.AddComponent<KinematicBody2D>();   // also adds BoxCollider2D + Rigidbody2D
            dummy.GetComponent<BoxCollider2D>().size = new Vector2(0.6f, 1.8f);
            var rb = dummy.GetComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.interpolation = RigidbodyInterpolation2D.Interpolate;
            SetReference(body, "data", assets.BodyData);

            dummy.AddComponent<Hurtbox>().Configure(Faction.Enemy, Vector2.zero, new Vector2(0.6f, 1.8f));

            // Visual faces the player: a training dummy stands to the player's right (faces left), a sparring dummy
            // to the left (faces right). A sparring dummy turns toward the player whenever it attacks.
            var visual = new GameObject("Visual").transform;
            visual.SetParent(dummy.transform, false);
            visual.localScale = new Vector3(sparring ? 1f : -1f, 1f, 1f);
            var rig = visual.gameObject.AddComponent<StickFigureRig>();
            SetReference(rig, "proportions", assets.Proportions);
            SetReference(rig, "lineMaterial", assets.Ink);
            rig.Build();
            if (assets.Combat.DummyIdle != null) rig.ApplyPose(assets.Combat.DummyIdle.pose);

            var labelObject = new GameObject("Combo Label");
            labelObject.transform.SetParent(dummy.transform, false);
            labelObject.transform.localPosition = new Vector3(0f, 1.35f, 0f);
            var label = labelObject.AddComponent<TextMesh>();
            label.text = sparring ? "sparring: parry my jabs (I / RB)\nred flash = can't parry, dodge!" : "hit me";
            label.anchor = TextAnchor.LowerCenter;
            label.alignment = TextAlignment.Center;
            label.characterSize = 0.05f;
            label.fontSize = 48;
            label.color = new Color32(0x9A, 0x96, 0x8C, 0xFF);
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font != null)
            {
                label.font = font;
                labelObject.GetComponent<MeshRenderer>().sharedMaterial = font.material;
            }

            var trainingDummy = dummy.AddComponent<TrainingDummy>();
            SetReference(trainingDummy, "physics", assets.Movement);
            SetReference(trainingDummy, "settings", assets.Combat.Settings);
            SetReference(trainingDummy, "rig", rig);
            SetReference(trainingDummy, "idlePose", assets.Combat.DummyIdle);
            SetReference(trainingDummy, "hitPose", assets.Combat.DummyHit);
            SetReference(trainingDummy, "label", label);

            if (sparring)
            {
                var attacker = dummy.AddComponent<SparringAttacker>();
                var so = new SerializedObject(attacker);
                SerializedProperty list = so.FindProperty("attacks");
                list.arraySize = assets.Combat.SparringAttacks.Count;
                for (int i = 0; i < list.arraySize; i++)
                    list.GetArrayElementAtIndex(i).objectReferenceValue = assets.Combat.SparringAttacks[i];
                so.FindProperty("settings").objectReferenceValue = assets.Combat.Settings;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            return dummy;
        }

        private static GameObject BuildPlayer(int layer, GymAssets assets)
        {
            var player = new GameObject("Player") { layer = layer };
            player.transform.position = new Vector3(0, 1.5f, 0);

            // Adding KinematicBody2D also adds the BoxCollider2D and Rigidbody2D it requires.
            var body = player.AddComponent<KinematicBody2D>();
            player.GetComponent<BoxCollider2D>().size = new Vector2(0.6f, 1.8f);
            var rb = player.GetComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.interpolation = RigidbodyInterpolation2D.Interpolate;

            var reader = player.AddComponent<InputReader>();

            // Visual root: WirePlayer puts the animated stick figure on it.
            var visual = new GameObject("Visual").transform;
            visual.SetParent(player.transform, false);

            var controller = player.AddComponent<PlayerController>();
            WirePlayer(controller, reader, body, visual, assets);
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


        // ---------------- asset helpers ----------------

        internal static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null) return asset;
            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        internal static Material LoadOrCreateInkMaterial()
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

        /// <summary>Sets a serialized reference field and checks that it actually took. Returns false if not.</summary>
        private static bool SetReference(Object target, string property, Object value)
        {
            var so = new SerializedObject(target);
            SerializedProperty field = so.FindProperty(property);
            if (field == null)
            {
                Debug.LogError($"{target.GetType().Name} has no serialized field '{property}'.", target);
                return false;
            }

            field.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();

            so.Update();
            bool ok = value != null && so.FindProperty(property).objectReferenceValue == value;
            if (!ok) Debug.LogError($"Could not set {target.GetType().Name}.{property} to '{(value != null ? value.name : "null")}'.", target);
            return ok;
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
