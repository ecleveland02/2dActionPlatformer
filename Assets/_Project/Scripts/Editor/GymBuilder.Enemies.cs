using Margin.Combat;
using Margin.Core;
using Margin.Enemies;
using Margin.Level;
using Margin.Physics;
using Margin.Player;
using Margin.Rendering;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Margin.EditorTools
{
    /// <summary>Enemy test scenes and menus (Milestone 4).</summary>
    public static partial class GymBuilder
    {
        private const string ArenaPath = "Assets/_Project/Scenes/EnemyArena.unity";

        /// <summary>
        /// Menu: Margin > Build Enemy Arena. A flat 60-unit room with two one-way platforms and three Doodle Grunts
        /// (two to the right, one to the left), for testing fights. Die and you respawn at x = 0 with the grunts reset.
        /// </summary>
        [MenuItem("Margin/Build Enemy Arena")]
        public static void BuildEnemyArena()
        {
            int groundLayer = LayerMask.NameToLayer("Ground");
            int oneWayLayer = LayerMask.NameToLayer("OneWayPlatform");
            int playerLayer = LayerMask.NameToLayer("Player");
            if (groundLayer < 0 || oneWayLayer < 0 || playerLayer < 0)
            {
                EditorUtility.DisplayDialog("Build Enemy Arena",
                    "Layers Ground, OneWayPlatform and Player must exist (Project Settings > Tags and Layers).", "OK");
                return;
            }

            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ArenaPath) != null &&
                !EditorUtility.DisplayDialog("Build Enemy Arena", "EnemyArena.unity already exists. Rebuild and overwrite it?", "Rebuild", "Cancel"))
                return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            // Assets are loaded after NewScene (see Build) so they aren't unloaded with the old scene.
            GymAssets assets = LoadAssets();
            if (assets == null) return;
            inkMaterial = assets.Ink;
            EnemyData grunt = StarterEnemies.EnsureCreated().Grunt;

            new GameObject("GameLoop").AddComponent<GameLoop>();

            var level = new GameObject("Level").transform;
            Box(level, "Wall Left", -31, -1, -30, 12, groundLayer);
            Box(level, "Floor", -30, -1, 30, 0, groundLayer);
            Box(level, "Wall Right", 30, -1, 31, 12, groundLayer);
            OneWay(level, "OneWay Left", -6, -2, 2.5f, oneWayLayer);
            OneWay(level, "OneWay Right", 3, 7, 2.5f, oneWayLayer);

            var labels = new GameObject("Labels").transform;
            Label(labels, "ENEMY ARENA: 3 DOODLE GRUNTS", 0, 6);
            Label(labels, "parry = F / RB    combo breaker = parry in hitstun (50 ink)", 0, 5.2f);

            GameObject player = BuildPlayer(playerLayer, assets);
            var enemies = new GameObject("Enemies").transform;
            BuildGrunt(new Vector3(8f, 1f, 0f), assets, grunt).transform.SetParent(enemies, true);
            BuildGrunt(new Vector3(12f, 1f, 0f), assets, grunt).transform.SetParent(enemies, true);
            BuildGrunt(new Vector3(-10f, 1f, 0f), assets, grunt, faceRight: true).transform.SetParent(enemies, true);

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
            EditorSceneManager.SaveScene(scene, ArenaPath);
            AddToBuildSettings(ArenaPath);
            Debug.Log("Enemy arena built at " + ArenaPath + ". Press Play.");
        }

        /// <summary>Menu: Margin > Add Doodle Grunt. Puts a grunt 6 units to the right of the player in the open scene.</summary>
        [MenuItem("Margin/Add Doodle Grunt")]
        public static void AddDoodleGrunt()
        {
            Scene scene = SceneManager.GetActiveScene();
            PlayerController player = null;
            foreach (GameObject root in scene.GetRootGameObjects())
                if ((player = root.GetComponentInChildren<PlayerController>(true)) != null) break;

            GymAssets assets = LoadAssets();
            if (assets == null) return;
            inkMaterial = assets.Ink;
            EnemyData grunt = StarterEnemies.EnsureCreated().Grunt;

            Vector3 at = player != null ? player.transform.position + new Vector3(6f, 0.5f, 0f) : new Vector3(6f, 1.5f, 0f);
            GameObject go = BuildGrunt(at, assets, grunt);
            Selection.activeGameObject = go;
            EditorSceneManager.MarkSceneDirty(scene);
            if (!string.IsNullOrEmpty(scene.path)) EditorSceneManager.SaveScene(scene);
        }

        private static GameObject BuildGrunt(Vector3 position, GymAssets assets, EnemyData data, bool faceRight = false)
        {
            var go = new GameObject("Doodle Grunt");
            go.transform.position = position;
            var body = go.AddComponent<KinematicBody2D>();   // also adds BoxCollider2D + Rigidbody2D
            go.GetComponent<BoxCollider2D>().size = new Vector2(0.6f, 1.8f);
            var rb = go.GetComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.interpolation = RigidbodyInterpolation2D.Interpolate;
            SetReference(body, "data", assets.BodyData);

            go.AddComponent<Hurtbox>().Configure(Faction.Enemy, Vector2.zero, new Vector2(0.6f, 1.8f));

            var visual = new GameObject("Visual").transform;
            visual.SetParent(go.transform, false);
            visual.localScale = new Vector3(faceRight ? 1f : -1f, 1f, 1f);
            var rig = visual.gameObject.AddComponent<StickFigureRig>();
            SetReference(rig, "proportions", data.proportions != null ? data.proportions : assets.Proportions);
            SetReference(rig, "lineMaterial", assets.Ink);
            rig.Build();
            if (data.idle != null && data.idle.entries.Count > 0 && data.idle.entries[0].pose != null)
                rig.ApplyPose(data.idle.entries[0].pose.pose);
            var animator = visual.gameObject.AddComponent<PoseAnimator>();
            SetReference(animator, "rig", rig);

            var enemy = go.AddComponent<EnemyBase>();
            SetReference(enemy, "data", data);
            SetReference(enemy, "physics", assets.Movement);
            SetReference(enemy, "settings", assets.Combat.Settings);
            SetReference(enemy, "rig", rig);
            SetReference(enemy, "animator", animator);
            return go;
        }
    }
}
