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
        /// Menu: Margin > Build Enemy Arena. A flat 60-unit room with two one-way platforms and three mixed enemies
        /// (spec M4 acceptance): a Doodle Grunt and a Pencil Lancer to the right, a Scribble Bat to the left.
        /// Die and you respawn at x = 0 with every enemy reset.
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
            StarterEnemies.Result enemyData = StarterEnemies.EnsureCreated();

            new GameObject("GameLoop").AddComponent<GameLoop>();

            var level = new GameObject("Level").transform;
            Box(level, "Wall Left", -31, -1, -30, 12, groundLayer);
            Box(level, "Floor", -30, -1, 30, 0, groundLayer);
            Box(level, "Wall Right", 30, -1, 31, 12, groundLayer);
            OneWay(level, "OneWay Left", -6, -2, 2.5f, oneWayLayer);
            OneWay(level, "OneWay Right", 3, 7, 2.5f, oneWayLayer);

            var labels = new GameObject("Labels").transform;
            Label(labels, "ENEMY ARENA: GRUNT, LANCER, BAT (red flash = can't parry)", 0, 6);
            Label(labels, "parry = F / RB    combo breaker = parry in hitstun (50 ink)", 0, 5.2f);

            GameObject player = BuildPlayer(playerLayer, assets);
            var enemies = new GameObject("Enemies").transform;
            BuildEnemy("Doodle Grunt", new Vector3(8f, 1f, 0f), assets, enemyData.Grunt).transform.SetParent(enemies, true);
            BuildEnemy("Pencil Lancer", new Vector3(13f, 1f, 0f), assets, enemyData.Lancer).transform.SetParent(enemies, true);
            BuildBat(new Vector3(-8f, 3.5f, 0f), assets, enemyData.Bat).transform.SetParent(enemies, true);

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
        public static void AddDoodleGrunt() => AddEnemy("Doodle Grunt", r => r.Grunt);

        /// <summary>Menu: Margin > Add Pencil Lancer. Puts a lancer 6 units to the right of the player in the open scene.</summary>
        [MenuItem("Margin/Add Pencil Lancer")]
        public static void AddPencilLancer() => AddEnemy("Pencil Lancer", r => r.Lancer);

        /// <summary>Menu: Margin > Add Scribble Bat. Puts a bat 5 units right of and 2.5 above the player.</summary>
        [MenuItem("Margin/Add Scribble Bat")]
        public static void AddScribbleBat()
        {
            Scene scene = SceneManager.GetActiveScene();
            PlayerController player = null;
            foreach (GameObject root in scene.GetRootGameObjects())
                if ((player = root.GetComponentInChildren<PlayerController>(true)) != null) break;

            GymAssets assets = LoadAssets();
            if (assets == null) return;
            inkMaterial = assets.Ink;
            EnemyData data = StarterEnemies.EnsureCreated().Bat;

            Vector3 at = player != null ? player.transform.position + new Vector3(5f, 2.5f, 0f) : new Vector3(5f, 3.5f, 0f);
            GameObject go = BuildBat(at, assets, data);
            Selection.activeGameObject = go;
            EditorSceneManager.MarkSceneDirty(scene);
            if (!string.IsNullOrEmpty(scene.path)) EditorSceneManager.SaveScene(scene);
        }

        private static GameObject BuildBat(Vector3 position, GymAssets assets, EnemyData data)
        {
            var go = new GameObject("Scribble Bat");
            go.transform.position = position;
            var body = go.AddComponent<KinematicBody2D>();
            go.GetComponent<BoxCollider2D>().size = new Vector2(0.6f, 0.5f);
            var rb = go.GetComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.interpolation = RigidbodyInterpolation2D.Interpolate;
            SetReference(body, "data", assets.BodyData);

            go.AddComponent<Hurtbox>().Configure(Faction.Enemy, Vector2.zero, new Vector2(0.7f, 0.55f));

            var visualObject = new GameObject("Visual");
            visualObject.transform.SetParent(go.transform, false);
            var visual = visualObject.AddComponent<ScribbleBatVisual>();
            SetReference(visual, "lineMaterial", assets.Ink);

            var enemy = go.AddComponent<FlyingEnemy>();
            SetReference(enemy, "data", data);
            SetReference(enemy, "physics", assets.Movement);
            SetReference(enemy, "settings", assets.Combat.Settings);
            SetReference(enemy, "visual", visual);
            return go;
        }

        private static void AddEnemy(string name, System.Func<StarterEnemies.Result, EnemyData> pick)
        {
            Scene scene = SceneManager.GetActiveScene();
            PlayerController player = null;
            foreach (GameObject root in scene.GetRootGameObjects())
                if ((player = root.GetComponentInChildren<PlayerController>(true)) != null) break;

            GymAssets assets = LoadAssets();
            if (assets == null) return;
            inkMaterial = assets.Ink;
            EnemyData data = pick(StarterEnemies.EnsureCreated());

            Vector3 at = player != null ? player.transform.position + new Vector3(6f, 0.5f, 0f) : new Vector3(6f, 1.5f, 0f);
            GameObject go = BuildEnemy(name, at, assets, data);
            Selection.activeGameObject = go;
            EditorSceneManager.MarkSceneDirty(scene);
            if (!string.IsNullOrEmpty(scene.path)) EditorSceneManager.SaveScene(scene);
        }

        private static GameObject BuildEnemy(string name, Vector3 position, GymAssets assets, EnemyData data, bool faceRight = false)
        {
            var go = new GameObject(name);
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

            // A held weapon (the Lancer's pencil): an ink line continuing the front forearm, hidden with the visual.
            if (data.weaponLength > 0f)
            {
                var weapon = new GameObject("Weapon");
                weapon.transform.SetParent(visual, false);
                var line = weapon.AddComponent<WeaponLine>();
                weapon.GetComponent<LineRenderer>().sharedMaterial = assets.Ink;
                SetReference(line, "rig", rig);
                var so = new SerializedObject(line);
                so.FindProperty("length").floatValue = data.weaponLength;
                so.FindProperty("look").objectReferenceValue = data.weaponLook;
                so.ApplyModifiedPropertiesWithoutUndo();
            }

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
