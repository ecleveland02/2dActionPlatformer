using System.Collections.Generic;
using System.Linq;
using Margin.Abilities;
using Margin.Bosses;
using Margin.Core;
using Margin.Level;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Pt = Margin.Rendering.WeaponShape.Pt;

namespace Margin.EditorTools
{
    /// <summary>
    /// Menu: Margin > Build World 2. Generates Scenes/World2.unity, World 2, Graph Paper (spec 11.2): grid-snapped
    /// moving blocks, Tack Turrets, Eraser Crawlers and erasable tiles, the Stapler Titan, and the Double Jump.
    /// World 1's last door leads here. Same room kit as World 1 (resize LevelBlock colliders and the ink follows);
    /// solid ground is drawn as graph paper squares and the page behind is a blue grid.
    ///
    ///   1 Graph Paper        a moving block ferries you over a pit; first Eraser Crawler
    ///   2 Plotted Course     ride a lift up past two Tack Turrets
    ///   3 Erasure            a bridge of tiles the crawlers rub out (a line above is the safe way)
    ///   4 Ink Pot            checkpoint; a turret on a pillar and a crawler
    ///   5 Gridlock           moving steps and a grapple ring over a long drop, a turret on the ceiling
    ///   6 Staple Remover     ink pot before the boss
    ///   7 The Stapler Titan  boss arena: two moving platforms (stapled in phase 2), three perches (torn out)
    ///   8 Spring Step        steps too tall for one jump: the Double Jump's reward room; end of World 2
    /// </summary>
    public static partial class GymBuilder
    {
        private const string World2Scene = "World2";
        private const string World2Path = "Assets/_Project/Scenes/World2.unity";

        // The graph paper behind World 2: faint blue squares, a darker line every 5.
        private static readonly Color GridMinor = new Color32(0xA9, 0xC4, 0xD6, 0x70);
        private static readonly Color GridMajor = new Color32(0x8E, 0xAE, 0xC6, 0xA8);

        [MenuItem("Margin/Build World 2")]
        public static void BuildWorld2()
        {
            int groundLayer = LayerMask.NameToLayer("Ground");
            int oneWayLayer = LayerMask.NameToLayer("OneWayPlatform");
            int playerLayer = LayerMask.NameToLayer("Player");
            if (groundLayer < 0 || oneWayLayer < 0 || playerLayer < 0)
            {
                EditorUtility.DisplayDialog("Build World 2",
                    "Layers Ground, OneWayPlatform and Player must exist (Project Settings > Tags and Layers).", "OK");
                return;
            }
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(World2Path) != null &&
                !EditorUtility.DisplayDialog("Build World 2", "World2.unity already exists. Rebuild and overwrite it?", "Rebuild", "Cancel"))
                return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
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
                World2 = StarterWorld2.EnsureCreated(),
                Stapler = StarterBosses.EnsureStapler(),
                Root = new GameObject("World 2 - Graph Paper").transform,
                Ground = groundLayer,
                OneWay = oneWayLayer,
                SolidStyle = LevelBlock.Style.Grid,
            };

            var rooms = new List<RoomKit>
            {
                GraphPaperRoom(kit, 0), PlottedCourse(kit, 1), Erasure(kit, 2), InkPot2(kit, 3), Gridlock(kit, 4),
                StapleRemover(kit, 5), StaplerArena(kit, 6), SpringStep(kit, 7),
            };
            for (int i = 0; i + 1 < rooms.Count; i++) RoomDoor.Link(rooms[i].RightDoor, rooms[i + 1].LeftDoor);

            // Played straight from the editor (no save): World 1's Grapple Line is owned, the Double Jump isn't yet.
            FinishWorld(kit, rooms, scene, World2Path, levelSettings, cameraSettings, new Dictionary<string, bool>
            {
                { "grappleLine", true }, { "doubleJump", false }, { "wallCling", false },
            });
            AddToBuildSettingsAfter(World2Path, WorldPath);
            Debug.Log($"World 2 built at {World2Path}: {rooms.Count} rooms. World 1's last door leads here once both are " +
                      "in Build Settings (rebuild World 1 if its last room still says END OF THE VERTICAL SLICE).");
        }

        // ================= rooms =================

        private static RoomKit GraphPaperRoom(WorldKit kit, int index)
        {
            var r = new RoomKit(kit, "1 Graph Paper", index, 44f, 16f);
            r.Frame(null, 0f);
            r.Solid("Floor A", -1f, -3f, 14f, 0f);
            r.Solid("Floor B", 24f, -3f, 45f, 0f);
            // A block that ferries you over the pit, one square at a time.
            r.Mover("Ferry", 14f, -1f, 17f, 0f, new[] { V(0, 0), V(7, 0) }, 2.5f, 40, 0, oneWay: false);
            r.Pot(6f, 0f);

            r.Label("WORLD 2  -  GRAPH PAPER", 20f, 10f, 1.4f);
            r.Label("moving blocks carry you", 19f, 4f);
            r.Label("erasers bite: parry them", 34f, 4f);
            r.Crawler(36f, 0f);
            r.GraphPaper(113);
            return r;
        }

        private static RoomKit PlottedCourse(WorldKit kit, int index)
        {
            // A tall room: a lift rises from a shallow shaft to the exit ledge, past two turrets.
            var r = new RoomKit(kit, "2 Plotted Course", index, 40f, 26f);
            r.Frame(0f, 18f);
            r.Solid("Floor A", -1f, -3f, 22f, 0f);
            r.Solid("Shaft", 22f, -3f, 25f, -1f);
            r.Solid("Floor B", 25f, -3f, 41f, 0f);
            r.Mover("Lift", 22f, -1f, 25f, 0f, new[] { V(0, 0), V(0, 18) }, 3f, 60, 0, oneWay: false);
            r.Line("Rest Line", 14f, 20f, 9f);
            r.Solid("Exit Ledge", 27f, 17f, 41f, 18f);

            r.Label("ride the lift up", 14f, 4f);
            r.Label("red tacks can't be parried: dash through them", 14f, 3.2f);
            r.Turret(0.36f, 12f, Vector2.right);
            r.Turret(33f, 23.64f, Vector2.down);
            r.Crawler(33f, 0f);
            r.GraphPaper(127);
            return r;
        }

        private static RoomKit Erasure(WorldKit kit, int index)
        {
            var r = new RoomKit(kit, "3 Erasure", index, 48f, 16f);
            r.Frame(0f, 0f);
            r.Solid("Floor A", -1f, -3f, 10f, 0f);
            r.Solid("Floor B", 34f, -3f, 49f, 0f);
            r.ErasableRow("Bridge", 10f, 34f, 0f);
            r.Line("Safe Line", 15f, 29f, 3f);

            r.Label("erasers rub out the tiles they walk on", 5f, 5f);
            r.Label("(the tiles come back, eventually)", 5f, 4.2f);
            r.Crawler(16f, 0f);
            r.Crawler(27f, 0f);
            r.Crawler(41f, 0f);
            r.GraphPaper(131);
            return r;
        }

        private static RoomKit InkPot2(WorldKit kit, int index)
        {
            var r = new RoomKit(kit, "4 Ink Pot", index, 40f, 16f);
            r.Frame(0f, 0f);
            r.Solid("Floor", -1f, -3f, 41f, 0f);
            r.Solid("Pillar", 30f, 0f, 32f, 3f);
            r.Line("Line", 16f, 22f, 3f);
            r.Pot(7f, 0f);

            r.Turret(31f, 3.36f, Vector2.up);
            r.Crawler(24f, 0f);
            r.GraphPaper(137);
            return r;
        }

        private static RoomKit Gridlock(WorldKit kit, int index)
        {
            // A long drop: moving steps, a grapple ring in the middle, a turret watching from the ceiling.
            var r = new RoomKit(kit, "5 Gridlock", index, 48f, 18f);
            r.Frame(0f, 0f);
            r.Solid("Floor A", -1f, -3f, 8f, 0f);
            r.Solid("Floor B", 40f, -3f, 49f, 0f);
            r.Mover("Step 1", 10f, 1.8f, 13f, 2f, new[] { V(0, 0), V(0, 3) }, 2f, 30, 0, oneWay: true);
            r.Mover("Step 2", 16f, 3.8f, 19f, 4f, new[] { V(0, 0), V(4, 0) }, 2f, 30, 20, oneWay: true);
            r.Anchor(27f, 9f);
            r.Mover("Step 3", 32f, 2.8f, 35f, 3f, new[] { V(0, 0), V(0, 2) }, 2f, 30, 40, oneWay: true);

            r.Label("don't fall: pits cost health", 4f, 5f);
            r.Label("grapple: I / middle click / LT", 27f, 12f);
            r.Turret(22f, 15.64f, Vector2.down);
            r.GraphPaper(139);
            return r;
        }

        private static RoomKit StapleRemover(WorldKit kit, int index)
        {
            var r = new RoomKit(kit, "6 Staple Remover", index, 32f, 16f);
            r.Frame(0f, 0f);
            r.Solid("Floor", -1f, -3f, 33f, 0f);
            r.Pot(10f, 0f);
            r.Label("THE STAPLER TITAN  ->", 24f, 5.4f, 1.2f);
            r.Label("dodge, then punish while it's stuck", 24f, 4.4f);
            r.GraphPaper(149);
            return r;
        }

        private static RoomKit StaplerArena(WorldKit kit, int index)
        {
            var r = new RoomKit(kit, "7 The Stapler Titan", index, 36f, 18f);
            r.Frame(0f, 0f);
            r.Solid("Floor", -1f, -3f, 37f, 0f);

            // Phase 2 staples the two moving platforms in place and tears the three perches out (StaplePlatform).
            r.Mover("Left Platform", 4f, 3.2f, 9f, 3.4f, new[] { V(0, 0), V(3, 0) }, 1.5f, 50, 0, oneWay: true)
             .AddComponent<StaplePlatform>().KeepInPhase2 = true;
            r.Mover("Right Platform", 27f, 3.2f, 32f, 3.4f, new[] { V(0, 0), V(-3, 0) }, 1.5f, 50, 0, oneWay: true)
             .AddComponent<StaplePlatform>().KeepInPhase2 = true;
            r.Line("Perch Left", 1f, 5f, 6.4f).AddComponent<StaplePlatform>();
            r.Line("Perch Middle", 14f, 22f, 6.4f).AddComponent<StaplePlatform>();
            r.Line("Perch Right", 31f, 35f, 6.4f).AddComponent<StaplePlatform>();

            var gate = new GameObject("Boss Gate").transform;
            gate.SetParent(r.Room.transform, false);
            GameObject exitSeal = r.Solid("Exit Seal", 35f, 0f, 36f, DoorHeight + 0.2f);
            exitSeal.transform.SetParent(gate, true);
            GameObject entranceSeal = r.Solid("Entrance Seal", 0f, 0f, 1f, DoorHeight + 0.2f);
            entranceSeal.transform.SetParent(gate, true);
            entranceSeal.SetActive(false);

            StaplerTitanBoss boss = BuildStapler(r, kit.Stapler, kit.Assets);

            // The reward: the Spring Doodle (Double Jump) floats in the middle once the boss is beaten.
            var reward = new GameObject("Double Jump Pickup");
            reward.transform.SetParent(r.Room.transform, false);
            reward.transform.position = r.P(18f, 2.2f);
            reward.AddComponent<AbilityPickup>().Configure(Ability.DoubleJump, "SPRING DOODLE",
                "Jump again in the air: Double Jump\nthe spring under your feet hits enemies below",
                kit.Assets.Ink);
            reward.SetActive(false);
            r.Room.gameObject.AddComponent<BossArena>().Configure(boss, 5f, entranceSeal, exitSeal, reward, new Vector2(2f, 0.95f));
            r.GraphPaper(151);
            return r;
        }

        private static RoomKit SpringStep(WorldKit kit, int index)
        {
            // Steps taller than one jump (3.2): only the Double Jump (about 5.8) climbs them.
            var r = new RoomKit(kit, "8 Spring Step", index, 46f, 22f);
            r.Frame(0f, null);
            r.Solid("Floor", -1f, -3f, 47f, 0f);
            r.Solid("Tall Step", 12f, 0f, 20f, 5f);
            r.Solid("Taller Step", 26f, 0f, 34f, 9f);
            r.Solid("Summit", 38f, 0f, 47f, 12f);

            r.Label("double jump: JUMP again in the air", 6f, 7f);
            r.Label("the spring hits whatever is under you", 6f, 6.2f);
            r.Crawler(23f, 0f);
            r.Crawler(36f, 0f);
            r.Label("END OF WORLD 2  (for now)", 42f, 16f, 1.2f);
            r.Label("thanks for playing!", 42f, 15f);
            r.GraphPaper(157);
            return r;
        }

        private static Vector2Int V(int x, int y) => new Vector2Int(x, y);

        /// <summary>The Stapler Titan: asleep on the right of the arena, facing the door.</summary>
        private static StaplerTitanBoss BuildStapler(RoomKit r, StarterBosses.StaplerResult bossData, GymAssets assets)
        {
            const float halfHeight = 0.85f;
            var go = new GameObject("The Stapler Titan");
            go.transform.SetParent(r.Room.transform, false);
            go.transform.position = r.P(28f, halfHeight + 0.02f);
            var body = go.AddComponent<Margin.Physics.KinematicBody2D>();   // also adds BoxCollider2D + Rigidbody2D
            go.GetComponent<BoxCollider2D>().size = new Vector2(3f, halfHeight * 2f);
            var rb = go.GetComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.interpolation = RigidbodyInterpolation2D.Interpolate;
            SetReference(body, "data", assets.BodyData);
            go.AddComponent<Margin.Combat.Hurtbox>().Configure(Margin.Combat.Faction.Enemy, Vector2.zero, bossData.Stapler.bodySize);

            var visualObject = new GameObject("Visual");
            visualObject.transform.SetParent(go.transform, false);
            visualObject.transform.localPosition = new Vector3(0f, -halfHeight, 0f);
            var visual = visualObject.AddComponent<StaplerVisual>();
            SetReference(visual, "lineMaterial", assets.Ink);

            var boss = go.AddComponent<StaplerTitanBoss>();
            SetReference(boss, "data", bossData.Stapler);
            SetReference(boss, "settings", assets.Combat.Settings);
            SetReference(boss, "physics", assets.Movement);
            SetReference(boss, "visual", visual);
            SetReference(boss, "waveAttack", bossData.Shockwave);
            return boss;
        }

        /// <summary>Puts a scene in Build Settings right after <paramref name="after"/> (or at the end).</summary>
        private static void AddToBuildSettingsAfter(string path, string after)
        {
            var scenes = EditorBuildSettings.scenes.Where(s => s.path != path).ToList();
            int i = scenes.FindIndex(s => s.path == after);
            scenes.Insert(i >= 0 ? i + 1 : scenes.Count, new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        // ================= World 2 room kit pieces =================

        private sealed partial class RoomKit
        {
            /// <summary>
            /// A grid-snapped moving block (GridBlock) from (x0, y0) to (x1, y1) at its start, following
            /// <paramref name="path"/> in grid cells. Solid ones carry and push; one-way ones only carry. The drawing is a
            /// child so it can be smoothed between ticks.
            /// </summary>
            public GameObject Mover(string name, float x0, float y0, float x1, float y1, Vector2Int[] path, float speed,
                                    int pause, int delay, bool oneWay)
            {
                var go = new GameObject(name) { layer = oneWay ? kit.OneWay : kit.Ground };
                go.transform.SetParent(geometry, false);
                go.transform.position = P((x0 + x1) * 0.5f, (y0 + y1) * 0.5f);
                var size = new Vector2(x1 - x0, y1 - y0);
                go.AddComponent<BoxCollider2D>().size = size;

                var drawing = new GameObject("Drawing") { layer = go.layer };
                drawing.transform.SetParent(go.transform, false);
                var shape = drawing.AddComponent<BoxCollider2D>();
                shape.size = size;
                shape.enabled = false;   // only tells the LevelBlock what to draw; the parent is the solid one
                drawing.AddComponent<LevelBlock>().Configure(oneWay ? LevelBlock.Style.OneWay : kit.SolidStyle, kit.Assets.Ink);

                go.AddComponent<GridBlock>().Configure(path, speed, pause, delay, false, drawing.transform);
                return go;
            }

            /// <summary>A row of 1-unit erasable tiles (Eraser Crawlers rub them out) with their tops at y.</summary>
            public void ErasableRow(string name, float x0, float x1, float top)
            {
                var row = new GameObject(name).transform;
                row.SetParent(geometry, false);
                int n = 0;
                for (float x = x0; x < x1 - 0.01f; x += 1f, n++)
                {
                    var go = new GameObject($"Tile {n}") { layer = kit.Ground };
                    go.transform.SetParent(row, false);
                    go.transform.position = P(x + 0.5f, top - 0.5f);
                    go.AddComponent<BoxCollider2D>().size = Vector2.one;
                    go.AddComponent<LevelBlock>().Configure(LevelBlock.Style.Grid, kit.Assets.Ink);
                    go.AddComponent<ErasableTile>();
                }
            }

            /// <summary>A Tack Turret centered at (x, y), stuck to a surface facing <paramref name="mount"/>.</summary>
            public GameObject Turret(float x, float y, Vector2 mount) => Place(BuildTurret(P(x, y), mount, kit.Assets, kit.World2.Turret));

            public GameObject Crawler(float x, float floor) => Place(BuildCrawler(P(x, floor), kit.Assets, kit.World2.Crawler));

            /// <summary>
            /// World 2's page: faint blue graph paper squares with a darker line every 5 (scroll 0.9), geometric doodles
            /// (0.6) and eraser smudges in front (1.1).
            /// </summary>
            public void GraphPaper(int seed)
            {
                var random = new System.Random(seed);
                float top = height - 2f;

                Transform grid = Layer("Graph Paper", 0.9f);
                int n = 0;
                for (float x = -5f; x <= width + 5f; x += 1f, n++)
                {
                    bool major = Mathf.RoundToInt(x) % 5 == 0;
                    Stroke(grid, $"Col {n}", new[] { new Pt(x, -4f), new Pt(x, top + 2f) }, major ? GridMajor : GridMinor,
                           major ? 0.03f : 0.018f, -30);
                }
                n = 0;
                for (float y = -4f; y <= top + 2f; y += 1f, n++)
                {
                    bool major = Mathf.RoundToInt(y) % 5 == 0;
                    Stroke(grid, $"Row {n}", new[] { new Pt(-5f, y), new Pt(width + 5f, y) }, major ? GridMajor : GridMinor,
                           major ? 0.03f : 0.018f, -30);
                }

                Transform doodles = Layer("Doodles", 0.6f);
                DoodleKind[] kinds = { DoodleKind.Cube, DoodleKind.Star, DoodleKind.Arrow, DoodleKind.Bolt, DoodleKind.Spiral };
                int count = Mathf.Max(3, Mathf.RoundToInt(width * height / 110f));
                for (int i = 0; i < count; i++)
                {
                    DoodleKind kind = kinds[random.Next(kinds.Length)];
                    float size = 0.9f + (float)random.NextDouble() * 0.9f;
                    var d = new GameObject($"Doodle {kind}").transform;
                    d.SetParent(doodles, false);
                    d.position = P(-4f + (float)random.NextDouble() * (width + 8f), 1.5f + (float)random.NextDouble() * (top - 2f));
                    d.rotation = Quaternion.Euler(0f, 0f, (float)(random.NextDouble() * 30.0 - 15.0));
                    int s = 0;
                    foreach (List<Pt> stroke in LevelArt.Doodle(kind, size))
                        Stroke(d, $"Stroke {s++}", stroke.ToArray(), DoodleGrey, 0.04f, -20);
                }

                Transform smudges = Layer("Smudges", 1.1f);
                for (int i = 0; i < 2; i++)
                {
                    float x = width * (i == 0 ? 0.25f : 0.7f) + (float)random.NextDouble() * 4f;
                    var smudge = new GameObject("Smudge").transform;
                    smudge.SetParent(smudges, false);
                    smudge.position = P(x, -1.3f);
                    Stroke(smudge, "Scribble", LevelArt.Smudge(3f, 0.9f, seed + i).ToArray(), SmudgeGrey, 0.3f, 30);
                }
            }
        }
    }
}
