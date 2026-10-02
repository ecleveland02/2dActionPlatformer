using System.Collections.Generic;
using System.Linq;
using Margin.Abilities;
using Margin.Bosses;
using Margin.Core;
using Margin.Enemies;
using Margin.Level;
using Margin.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Pt = Margin.Rendering.WeaponShape.Pt;

namespace Margin.EditorTools
{
    /// <summary>
    /// Menu: Margin > Build World 1. Generates Scenes/World1.unity, the vertical slice (spec M5): World 1, the Lined
    /// Notebook, as rooms joined by doors (spec 11). Each room is a Room object with its geometry (LevelBlocks: resize
    /// their colliders in the Scene view and the ink follows), enemies, doors, ink pots and paper decoration
    /// (ruled lines 0.9, doodles 0.6, smudges 1.1 parallax). Rooms sit 200 units apart; only one is on at a time.
    ///
    ///   1 First Page      movement, jumping, notebook lines (one-way platforms)
    ///   2 Margin Notes    first Doodle Grunt: light and heavy attacks
    ///   3 Ruled Lines     climb a tall stack of lines; Scribble Bats (air attacks)
    ///   4 Ink Pot         checkpoint; Pencil Lancer (parry)
    ///   5 Pit Stop        pits, dash, bats over the gaps
    ///   6 Crossfire       mixed fight: grunts, lancer, bat
    ///   7 Margin Call     ink pot before the boss
    ///   8 The Highlighter boss arena (raised lines for phase 2); the exit is sealed until the boss falls
    ///   9 Loose Leaf      after the boss: gaps crossed with the Grapple Line; the far door leads to World 2
    /// </summary>
    public static partial class GymBuilder
    {
        private const string WorldPath = "Assets/_Project/Scenes/World1.unity";
        private const string LevelDataFolder = "Assets/_Project/Data/Level";
        private const float RoomSpacing = 200f;
        private const float DoorHeight = 4.5f;

        // Decoration colors: only the gameplay layer is full black ink (spec 11.3).
        private static readonly Color RuledLine = new Color32(0xA9, 0xBB, 0xCC, 0xA0);
        private static readonly Color MarginLine = new Color32(0xD9, 0x8C, 0x86, 0xA0);
        private static readonly Color DoodleGrey = new Color32(0xC2, 0xBD, 0xB1, 0xFF);
        private static readonly Color SmudgeGrey = new Color32(0x80, 0x7C, 0x74, 0x2C);

        /// <summary>Everything the room builders share.</summary>
        private sealed class WorldKit
        {
            public GymAssets Assets;
            public StarterEnemies.Result Enemies;
            public Transform Root;
            public int Ground, OneWay;
            /// <summary>How solid ground is drawn: World 1 hatched, World 2 graph paper squares.</summary>
            public LevelBlock.Style SolidStyle = LevelBlock.Style.Solid;
            public StarterWorld2.Result World2;
            public StarterBosses.StaplerResult Stapler;
        }

        [MenuItem("Margin/Build World 1")]
        public static void BuildWorld1()
        {
            int groundLayer = LayerMask.NameToLayer("Ground");
            int oneWayLayer = LayerMask.NameToLayer("OneWayPlatform");
            int playerLayer = LayerMask.NameToLayer("Player");
            if (groundLayer < 0 || oneWayLayer < 0 || playerLayer < 0)
            {
                EditorUtility.DisplayDialog("Build World 1",
                    "Layers Ground, OneWayPlatform and Player must exist (Project Settings > Tags and Layers).", "OK");
                return;
            }
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(WorldPath) != null &&
                !EditorUtility.DisplayDialog("Build World 1", "World1.unity already exists. Rebuild and overwrite it?", "Rebuild", "Cancel"))
                return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            // Assets are loaded after NewScene (see Build) so they aren't unloaded with the old scene.
            GymAssets assets = LoadAssets();
            if (assets == null) return;
            inkMaterial = assets.Ink;
            StarterAnimations.EnsureGrapple();
            StickFigureRigEditor.EnsureFolder(LevelDataFolder);
            var levelSettings = LoadOrCreate<LevelSettings>($"{LevelDataFolder}/LevelSettings.asset");
            var cameraSettings = LoadOrCreate<CameraSettings>($"{LevelDataFolder}/CameraSettings.asset");

            new GameObject("GameLoop").AddComponent<GameLoop>();
            var kit = new WorldKit
            {
                Assets = assets,
                Enemies = StarterEnemies.EnsureCreated(),
                Root = new GameObject("World 1 - Lined Notebook").transform,
                Ground = groundLayer,
                OneWay = oneWayLayer,
            };

            var rooms = new List<RoomKit>
            {
                FirstPage(kit, 0), MarginNotes(kit, 1), RuledLines(kit, 2), InkPotRoom(kit, 3), PitStop(kit, 4),
                Crossfire(kit, 5), MarginCall(kit, 6), HighlighterArena(kit, 7), LooseLeaf(kit, 8),
            };
            for (int i = 0; i + 1 < rooms.Count; i++) RoomDoor.Link(rooms[i].RightDoor, rooms[i + 1].LeftDoor);
            FinishWorld(kit, rooms, scene, WorldPath, levelSettings, cameraSettings,
                        new Dictionary<string, bool> { { "wallCling", false } });   // Boss 4's ability, not in World 1
            AddToBuildSettingsFirst(WorldPath);
            Debug.Log($"World 1 built at {WorldPath}: {rooms.Count} rooms. Press Play. (Rooms are switched off except " +
                      "the first; tick one on in the Hierarchy to edit it.)");
        }


        /// <summary>
        /// The parts every world scene shares: the player at the first room's start (<paramref name="abilities"/> sets
        /// AbilityUnlocks fields it begins with when playing the scene straight from the editor; a save overrides
        /// them), the LevelDirector, the camera and scene effects. Saves the scene.
        /// </summary>
        private static void FinishWorld(WorldKit kit, List<RoomKit> rooms, Scene scene, string path, LevelSettings levelSettings,
                                        CameraSettings cameraSettings, Dictionary<string, bool> abilities)
        {
            GymAssets assets = kit.Assets;
            GameObject player = BuildPlayer(LayerMask.NameToLayer("Player"), assets);
            player.transform.position = rooms[0].P(4f, 0.95f);
            var controller = player.GetComponent<PlayerController>();
            var so = new SerializedObject(controller);
            foreach (KeyValuePair<string, bool> a in abilities) so.FindProperty("abilities." + a.Key).boolValue = a.Value;
            so.ApplyModifiedPropertiesWithoutUndo();

            var director = new GameObject("LevelDirector").AddComponent<LevelDirector>();
            SetReference(director, "settings", levelSettings);
            SetReference(director, "startRoom", rooms[0].Room);
            SetReference(director, "player", controller);

            var camObject = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = camObject.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = cameraSettings.orthographicSize > 0f ? cameraSettings.orthographicSize : 7f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Paper;
            camObject.transform.position = player.transform.position + new Vector3(0f, 0f, -10f);
            var follow = camObject.AddComponent<CameraFollow>();
            follow.Target = player.transform;
            SetReference(follow, "settings", cameraSettings);
            camObject.AddComponent<AudioListener>();
            EnsureSceneEffects(assets);

            // Only the first room shows while editing (the director does the same at play time).
            for (int i = 1; i < rooms.Count; i++) rooms[i].Room.gameObject.SetActive(false);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, path);
        }

        // ================= rooms =================

        private static RoomKit FirstPage(WorldKit kit, int index)
        {
            var r = new RoomKit(kit, "1 First Page", index, 48f, 16f);
            r.Frame(null, 0f);
            r.Solid("Floor A", -1f, -3f, 21f, 0f);
            r.Solid("Ditch", 21f, -3f, 25f, -1.2f);               // a dip you can walk out of, not a pit
            r.Solid("Floor B", 25f, -3f, 48f, 0f);
            r.Solid("Step", 13f, 0f, 17f, 1.2f);
            r.Line("Line 1", 29f, 35f, 2.6f);
            r.Line("Line 2", 33f, 39f, 5.2f);
            r.Line("Line 3", 38f, 44f, 7.8f);

            r.Label("WORLD 1  -  THE LINED NOTEBOOK", 13f, 10f, 1.4f);
            r.Label("move: A D / arrows / stick", 5f, 3.2f);
            r.Label("jump: SPACE / A  (hold for higher)", 15f, 3.6f);
            r.Label("notebook lines: jump up through them", 33f, 10f);
            r.Label("DOWN + JUMP drops back down", 33f, 9.2f);
            r.Label("->", 46f, 2.5f);
            r.Paper(11);
            return r;
        }

        private static RoomKit MarginNotes(WorldKit kit, int index)
        {
            var r = new RoomKit(kit, "2 Margin Notes", index, 44f, 16f);
            r.Frame(0f, 0f);
            r.Solid("Floor", -1f, -3f, 45f, 0f);
            r.Solid("Platform", 31f, 0f, 37f, 1.8f);
            r.Line("Line", 26f, 31f, 3.2f);

            r.Label("light attack: J / left click / X", 9f, 5.2f);
            r.Label("heavy attack: K / right click / Y", 9f, 4.4f);
            r.Label("light, light, light... then heavy", 9f, 3.6f);
            r.Grunt(22f, 0f);
            r.Paper(23);
            return r;
        }

        private static RoomKit RuledLines(WorldKit kit, int index)
        {
            // A tall climb: nine notebook lines zigzag up to a ledge and the exit near the top.
            var r = new RoomKit(kit, "3 Ruled Lines", index, 30f, 36f);
            r.Frame(0f, 26.2f);
            r.Solid("Floor", -1f, -3f, 31f, 0f);
            float[,] lines =
            {
                { 3f, 11f, 2.8f }, { 12f, 20f, 5.4f }, { 21f, 28f, 8.0f }, { 13f, 20f, 10.6f }, { 3f, 11f, 13.2f },
                { 8f, 16f, 15.8f }, { 17f, 25f, 18.4f }, { 22f, 28f, 21.0f }, { 13f, 21f, 23.6f },
            };
            for (int i = 0; i < lines.GetLength(0); i++) r.Line($"Line {i + 1}", lines[i, 0], lines[i, 1], lines[i, 2]);
            r.Solid("Exit Ledge", 22f, 25.2f, 31f, 26.2f);

            r.Label("climb the lines", 7f, 5.4f);
            r.Label("attack in the air to hit bats", 19f, 13.2f);
            r.Bat(16f, 12.5f);
            r.Bat(20f, 22.5f);
            r.Paper(37);
            return r;
        }

        private static RoomKit InkPotRoom(WorldKit kit, int index)
        {
            var r = new RoomKit(kit, "4 Ink Pot", index, 40f, 16f);
            r.Frame(0f, 0f);
            r.Solid("Floor", -1f, -3f, 41f, 0f);
            r.Line("Line", 13f, 19f, 2.8f);
            r.Pot(7f, 0f);

            r.Label("ink pots heal you, and you come back to the last one", 9f, 5.8f);
            r.Label("parry: F / RB just before a hit lands", 25f, 6.6f);
            r.Label("red flash = can't be parried: dash away (L / SHIFT / RT)", 25f, 5.8f);
            r.Lancer(30f, 0f);
            r.Paper(41);
            return r;
        }

        private static RoomKit PitStop(WorldKit kit, int index)
        {
            var r = new RoomKit(kit, "5 Pit Stop", index, 56f, 16f);
            r.Frame(0f, 0f);
            r.Solid("Floor A", -1f, -3f, 10f, 0f);
            r.Solid("Floor B", 14f, -3f, 20f, 0f);
            r.Solid("Floor C", 24.5f, -3f, 30f, 1f);
            r.Solid("Floor D", 34f, -3f, 39.5f, 0f);
            r.Solid("Floor E", 45f, -3f, 57f, 0f);

            r.Label("dash: L / SHIFT / RT  (works in the air too)", 7f, 5f);
            r.Label("pits cost health", 12f, 3.2f);
            r.Bat(22f, 4.5f);
            r.Bat(42f, 5f);
            r.Grunt(51f, 0f);
            r.Paper(53);
            return r;
        }

        private static RoomKit Crossfire(WorldKit kit, int index)
        {
            var r = new RoomKit(kit, "6 Crossfire", index, 48f, 18f);
            r.Frame(0f, 0f);
            r.Solid("Floor", -1f, -3f, 49f, 0f);
            r.Solid("Pillar", 22f, 0f, 26f, 1.6f);
            r.Line("Line Left", 6f, 14f, 3f);
            r.Line("Line Right", 34f, 42f, 3f);
            r.Line("Line Top", 19f, 29f, 5.6f);

            r.Label("special: U / LB spends ink", 8f, 8.2f);
            r.Label("full ink? Z / B heals (Redraw)", 8f, 7.4f);
            r.Grunt(16f, 0f);
            r.Grunt(31f, 0f);
            r.Lancer(40f, 0f);
            r.Bat(24f, 8f);
            r.Paper(67);
            return r;
        }

        private static RoomKit MarginCall(WorldKit kit, int index)
        {
            var r = new RoomKit(kit, "7 Margin Call", index, 36f, 16f);
            r.Frame(0f, 0f);
            r.Solid("Floor", -1f, -3f, 37f, 0f);
            r.Pot(10f, 0f);
            r.Label("THE HIGHLIGHTER  ->", 27f, 5.4f, 1.2f);
            r.Label("parry its strokes", 27f, 4.4f);
            r.Paper(71);
            return r;
        }

        private static RoomKit HighlighterArena(WorldKit kit, int index)
        {
            // Flat floor for phase 1; three raised lines to stand on when phase 2 floods the floor with ink.
            var r = new RoomKit(kit, "8 The Highlighter", index, 36f, 18f);
            r.Frame(0f, 0f);
            r.Solid("Floor", -1f, -3f, 37f, 0f);
            r.Line("Line Left", 3f, 9f, 3f);
            r.Line("Line Middle", 14f, 22f, 5.2f);
            r.Line("Line Right", 27f, 33f, 3f);

            var gate = new GameObject("Boss Gate").transform;
            gate.SetParent(r.Room.transform, false);
            GameObject exitSeal = r.Solid("Exit Seal", 35f, 0f, 36f, DoorHeight + 0.2f);
            exitSeal.transform.SetParent(gate, true);
            GameObject entranceSeal = r.Solid("Entrance Seal", 0f, 0f, 1f, DoorHeight + 0.2f);
            entranceSeal.transform.SetParent(gate, true);
            entranceSeal.SetActive(false);

            StarterBosses.Result bossData = StarterBosses.EnsureCreated();
            InkFlood flood = BuildFlood(r, bossData, kit.Assets);
            HighlighterBoss boss = BuildHighlighter(r, bossData, flood, kit.Assets);

            // The reward: the Grapple Line pen floats in the middle of the arena once the boss is beaten.
            var reward = new GameObject("Grapple Line Pickup");
            reward.transform.SetParent(r.Room.transform, false);
            reward.transform.position = r.P(18f, 2.2f);
            reward.AddComponent<AbilityPickup>().Configure(Ability.GrappleLine, "GRAPPLE LINE",
                "I / middle click / LT: hook a ring and swing\nJump lets go   Up/Down reel in and out\nalso yanks enemies to you",
                kit.Assets.Ink);
            reward.SetActive(false);
            r.Room.gameObject.AddComponent<BossArena>().Configure(boss, 5f, entranceSeal, exitSeal, reward, new Vector2(2f, 0.95f));
            r.Paper(83);
            return r;
        }

        /// <summary>The phase 2 ink pool across the whole arena floor (empty until the boss floods it).</summary>
        private static InkFlood BuildFlood(RoomKit r, StarterBosses.Result bossData, GymAssets assets)
        {
            var go = new GameObject("Ink Flood");
            go.transform.SetParent(r.Room.transform, false);
            go.transform.position = r.P(18f, 0f);
            var flood = go.AddComponent<InkFlood>();
            flood.Configure(bossData.Flood, assets.Combat.Settings, 38f, 1.4f);
            SetReference(flood, "lineMaterial", assets.Ink);
            return flood;
        }

        /// <summary>The Highlighter: asleep on the right of the arena, facing the door.</summary>
        private static HighlighterBoss BuildHighlighter(RoomKit r, StarterBosses.Result bossData, InkFlood flood, GymAssets assets)
        {
            const float halfHeight = 1.5f;
            var go = new GameObject("The Highlighter");
            go.transform.SetParent(r.Room.transform, false);
            go.transform.position = r.P(28f, halfHeight);
            var body = go.AddComponent<Margin.Physics.KinematicBody2D>();   // also adds BoxCollider2D + Rigidbody2D
            go.GetComponent<BoxCollider2D>().size = new Vector2(1.2f, halfHeight * 2f);
            var rb = go.GetComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.interpolation = RigidbodyInterpolation2D.Interpolate;
            SetReference(body, "data", assets.BodyData);
            go.AddComponent<Margin.Combat.Hurtbox>().Configure(Margin.Combat.Faction.Enemy, Vector2.zero, bossData.Highlighter.bodySize);

            var visualObject = new GameObject("Visual");
            visualObject.transform.SetParent(go.transform, false);
            visualObject.transform.localPosition = new Vector3(0f, -halfHeight, 0f);
            var visual = visualObject.AddComponent<HighlighterVisual>();
            SetReference(visual, "lineMaterial", assets.Ink);

            var boss = go.AddComponent<HighlighterBoss>();
            SetReference(boss, "data", bossData.Highlighter);
            SetReference(boss, "settings", assets.Combat.Settings);
            SetReference(boss, "physics", assets.Movement);
            SetReference(boss, "visual", visual);
            SetReference(boss, "flood", flood);
            SetReference(boss, "lineMaterial", assets.Ink);
            return boss;
        }

        private static RoomKit LooseLeaf(WorldKit kit, int index)
        {
            // Two gaps too wide to jump: the Grapple Line (won from the Highlighter) swings across.
            var r = new RoomKit(kit, "9 Loose Leaf", index, 64f, 18f);
            r.Frame(0f, 0f);
            r.RightDoor.ConfigureSceneExit(World2Scene);   // on to World 2 (Margin > Build World 2 adds it)
            r.Solid("Floor A", -1f, -3f, 14f, 0f);
            r.Solid("Floor B", 30f, -3f, 40f, 0f);
            r.Solid("Floor C", 52f, -3f, 65f, 0f);

            r.Label("too far to jump...", 10f, 4f);
            r.Label("grapple: I / middle click / LT at a ring", 7f, 6.4f);
            r.Label("Jump lets go, Left/Right pump the swing", 7f, 5.6f);
            r.Anchor(18.5f, 8.5f);
            r.Anchor(25.5f, 8.5f);
            r.Anchor(46f, 8.5f);
            r.Grunt(36f, 0f);
            r.Label("WORLD 2: GRAPH PAPER  ->", 57f, 6f, 1.2f);
            r.Paper(97);
            return r;
        }

        // ================= room kit =================

        /// <summary>Builds one room: geometry, doors, enemies and decoration, in the room's own coordinates
        /// (x from 0 at the left edge of the camera bounds, floor top usually at y = 0, bounds bottom at -2).</summary>
        private sealed partial class RoomKit
        {
            public readonly Room Room;
            public RoomDoor LeftDoor, RightDoor;
            private readonly WorldKit kit;
            private readonly Transform geometry, doors, enemies, decor, labels;
            private readonly Vector2 origin;
            private readonly float width, height;

            public RoomKit(WorldKit worldKit, string title, int index, float roomWidth, float roomHeight)
            {
                kit = worldKit;
                width = roomWidth;
                height = roomHeight;
                origin = new Vector2(index * RoomSpacing, 0f);
                var go = new GameObject("Room " + title);
                go.transform.SetParent(kit.Root, false);
                go.transform.position = origin;
                Room = go.AddComponent<Room>();
                Room.Configure(title, new Rect(0f, -2f, roomWidth, roomHeight));
                geometry = Child("Geometry");
                doors = Child("Doors");
                enemies = Child("Enemies");
                decor = Child("Decor");
                labels = Child("Labels");
            }

            /// <summary>Room coordinates to world.</summary>
            public Vector3 P(float x, float y) => new Vector3(origin.x + x, origin.y + y, 0f);

            private Transform Child(string name)
            {
                var t = new GameObject(name).transform;
                t.SetParent(Room.transform, false);
                return t;
            }

            /// <summary>Walls down both sides (with a door-sized gap where there's a door) and a ceiling.</summary>
            public void Frame(float? leftDoor, float? rightDoor)
            {
                float top = height - 2f;
                Wall("Wall Left", -1f, 0f, leftDoor);
                Wall("Wall Right", width, width + 1f, rightDoor);
                Solid("Ceiling", -1f, top, width + 1f, top + 1f);
                if (leftDoor.HasValue)
                {
                    LeftDoor = Door(RoomDoor.Side.Left, leftDoor.Value);
                    Solid("Doorstep Left", -4f, leftDoor.Value - 1f, 0f, leftDoor.Value);
                }
                if (rightDoor.HasValue)
                {
                    RightDoor = Door(RoomDoor.Side.Right, rightDoor.Value);
                    Solid("Doorstep Right", width, rightDoor.Value - 1f, width + 4f, rightDoor.Value);
                }
            }

            private void Wall(string name, float x0, float x1, float? doorFloor)
            {
                float bottom = -3f, top = height - 1f;
                if (!doorFloor.HasValue)
                {
                    Solid(name, x0, bottom, x1, top);
                    return;
                }
                float gapBottom = doorFloor.Value, gapTop = gapBottom + DoorHeight;
                if (gapBottom > bottom) Solid(name + " Low", x0, bottom, x1, gapBottom);
                if (gapTop < top) Solid(name + " High", x0, gapTop, x1, top);
            }

            /// <summary>A door in the wall gap. The player arrives 1.6 units inside, standing on the door's floor.</summary>
            private RoomDoor Door(RoomDoor.Side side, float floor)
            {
                bool left = side == RoomDoor.Side.Left;
                var go = new GameObject("Door " + side);
                go.transform.SetParent(doors, false);
                go.transform.position = P(left ? -0.3f : width + 0.3f, floor + 2f);
                var door = go.AddComponent<RoomDoor>();
                door.Configure(side, new Vector2(1f, 4f), new Vector2(left ? 1.9f : -1.9f, -2f + 0.95f));
                return door;
            }

            /// <summary>Solid ground (Ground layer): an inked, hatched block from (x0, y0) to (x1, y1).</summary>
            public GameObject Solid(string name, float x0, float y0, float x1, float y1)
            {
                var go = new GameObject(name) { layer = kit.Ground };
                go.transform.SetParent(geometry, false);
                go.transform.position = P((x0 + x1) * 0.5f, (y0 + y1) * 0.5f);
                go.AddComponent<BoxCollider2D>().size = new Vector2(x1 - x0, y1 - y0);
                go.AddComponent<LevelBlock>().Configure(kit.SolidStyle, kit.Assets.Ink);
                return go;
            }

            /// <summary>A notebook line: a one-way platform (jump up through, Down + Jump to drop) with its top at y.</summary>
            public GameObject Line(string name, float x0, float x1, float top)
            {
                const float thickness = 0.2f;
                var go = new GameObject(name) { layer = kit.OneWay };
                go.transform.SetParent(geometry, false);
                go.transform.position = P((x0 + x1) * 0.5f, top - thickness * 0.5f);
                go.AddComponent<BoxCollider2D>().size = new Vector2(x1 - x0, thickness);
                go.AddComponent<LevelBlock>().Configure(LevelBlock.Style.OneWay, kit.Assets.Ink);
                return go;
            }

            public void Label(string text, float x, float y, float scale = 1f)
            {
                int before = labels.childCount;
                GymBuilder.Label(labels, text, origin.x + x, origin.y + y);
                if (labels.childCount > before) labels.GetChild(labels.childCount - 1).localScale = Vector3.one * (scale * 0.8f);
            }

            public GameObject Grunt(float x, float floor) => Place(BuildEnemy("Doodle Grunt", P(x, floor + 1f), kit.Assets, kit.Enemies.Grunt));
            public GameObject Lancer(float x, float floor) => Place(BuildEnemy("Pencil Lancer", P(x, floor + 1f), kit.Assets, kit.Enemies.Lancer));
            public GameObject Bat(float x, float y) => Place(BuildBat(P(x, y), kit.Assets, kit.Enemies.Bat));

            private GameObject Place(GameObject enemy)
            {
                enemy.transform.SetParent(enemies, true);
                return enemy;
            }

            /// <summary>A Grapple Line ring pinned to the page.</summary>
            public GrappleAnchor Anchor(float x, float y)
            {
                var go = new GameObject("Grapple Ring");
                go.transform.SetParent(geometry, false);
                go.transform.position = P(x, y);
                var anchor = go.AddComponent<GrappleAnchor>();
                SetReference(anchor, "lineMaterial", kit.Assets.Ink);
                return anchor;
            }

            /// <summary>An ink pot checkpoint standing on the floor at x.</summary>
            public Checkpoint Pot(float x, float floor)
            {
                var go = new GameObject("Ink Pot");
                go.transform.SetParent(Room.transform, false);
                go.transform.position = P(x, floor);
                var visual = go.AddComponent<InkPotVisual>();
                SetReference(visual, "lineMaterial", kit.Assets.Ink);
                var checkpoint = go.AddComponent<Checkpoint>();
                SetReference(checkpoint, "visual", visual);
                return checkpoint;
            }

            /// <summary>
            /// The page itself (spec 11.3): pale ruled lines and a red margin (scroll 0.9), grey doodles (0.6) and a
            /// couple of eraser smudges in front (1.1). Covers a bit past the room so parallax never shows an edge.
            /// </summary>
            public void Paper(int seed)
            {
                var random = new System.Random(seed);
                float top = height - 2f;

                Transform lines = Layer("Paper Lines", 0.9f);
                int n = 0;
                for (float y = -4f; y <= top + 2f; y += 0.9f, n++)
                    Stroke(lines, $"Rule {n}", new[] { new Pt(-5f, y), new Pt(width + 5f, y) }, RuledLine, 0.025f, -30);
                Stroke(lines, "Margin", new[] { new Pt(2.4f, -4f), new Pt(2.4f, top + 2f) }, MarginLine, 0.03f, -29);

                Transform doodles = Layer("Doodles", 0.6f);
                int count = Mathf.Max(3, Mathf.RoundToInt(width * height / 90f));
                var kinds = (DoodleKind[])System.Enum.GetValues(typeof(DoodleKind));
                for (int i = 0; i < count; i++)
                {
                    DoodleKind kind = kinds[random.Next(kinds.Length)];
                    float size = 0.9f + (float)random.NextDouble() * 0.9f;
                    var d = new GameObject($"Doodle {kind}").transform;
                    d.SetParent(doodles, false);
                    d.position = P(-4f + (float)random.NextDouble() * (width + 8f), 1.5f + (float)random.NextDouble() * (top - 2f));
                    d.rotation = Quaternion.Euler(0f, 0f, (float)(random.NextDouble() * 40.0 - 20.0));
                    int s = 0;
                    foreach (List<Pt> stroke in LevelArt.Doodle(kind, size))
                        Stroke(d, $"Stroke {s++}", stroke.ToArray(), DoodleGrey, 0.04f, -20);
                }

                Transform smudges = Layer("Smudges", 1.1f);
                for (int i = 0; i < 2; i++)
                {
                    float x = width * (i == 0 ? 0.2f : 0.75f) + (float)random.NextDouble() * 4f;
                    var smudge = new GameObject("Smudge").transform;
                    smudge.SetParent(smudges, false);
                    smudge.position = P(x, -1.3f);
                    Stroke(smudge, "Scribble", LevelArt.Smudge(3f, 0.9f, seed + i).ToArray(), SmudgeGrey, 0.3f, 30);
                }
            }

            private Transform Layer(string name, float scroll)
            {
                var t = new GameObject(name).transform;
                t.SetParent(decor, false);
                t.position = P(0f, 0f);
                t.gameObject.AddComponent<ParallaxLayer>().Scroll = scroll;
                return t;
            }

            /// <summary>One pen stroke, points in the parent's space.</summary>
            private void Stroke(Transform parent, string name, Pt[] points, Color color, float width, int order)
            {
                var go = new GameObject(name);
                go.transform.SetParent(parent, false);
                var line = go.AddComponent<LineRenderer>();
                line.useWorldSpace = false;
                line.sharedMaterial = kit.Assets.Ink;
                line.widthMultiplier = width;
                line.startColor = line.endColor = color;
                line.numCapVertices = 2;
                line.numCornerVertices = 2;
                line.sortingOrder = order;
                line.shadowCastingMode = ShadowCastingMode.Off;
                line.receiveShadows = false;
                line.positionCount = points.Length;
                for (int i = 0; i < points.Length; i++) line.SetPosition(i, new Vector3(points[i].X, points[i].Y, 0f));
            }
        }

        /// <summary>Puts a scene first in Build Settings (the slice starts in World 1).</summary>
        /// <summary>Puts a scene at the front of Build Settings, after the title screen if there is one.</summary>
        private static void AddToBuildSettingsFirst(string path)
        {
            var scenes = EditorBuildSettings.scenes.Where(s => s.path != path).ToList();
            bool titleFirst = path != TitlePath && scenes.Count > 0 && scenes[0].path == TitlePath;
            scenes.Insert(titleFirst ? 1 : 0, new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
