using System.Collections.Generic;
using Margin.Combat;
using Margin.Rendering;
using Margin.Weapons;
using UnityEditor;
using UnityEngine;

namespace Margin.EditorTools
{
    /// <summary>
    /// Creates the starter combat data (Milestone 3, chunk 1): attack poses and clips, the Brush Katana with
    /// Light 1 and Heavy, and CombatSettings. Only missing assets are created, so your tuning is never overwritten.
    /// Hitbox boxes were fitted to the blade in the Strike poses (blade length 0.9).
    /// </summary>
    public static class StarterCombat
    {
        private const string AttackFolder = "Assets/_Project/Data/Attacks";
        private const string WeaponFolder = "Assets/_Project/Data/Weapons";
        private const string ClipFolder = "Assets/_Project/Data/Animations";
        private const string SettingsPath = "Assets/_Project/Data/CombatSettings.asset";

        internal sealed class Result
        {
            public WeaponData Weapon;
            public CombatSettings Settings;
            public PoseData DummyIdle;
            public PoseData DummyHit;
        }

        [MenuItem("Margin/Create Starter Combat Data")]
        public static void CreateFromMenu()
        {
            Result result = EnsureCreated();
            EditorGUIUtility.PingObject(result.Weapon);
            Debug.Log("Starter combat data ready: " + AssetDatabase.GetAssetPath(result.Weapon));
        }

        internal static Result EnsureCreated()
        {
            StarterPoses.EnsureCreated();   // the base poses (Idle etc.) must exist first
            foreach (string folder in new[] { AttackFolder, WeaponFolder, ClipFolder })
                StickFigureRigEditor.EnsureFolder(folder);

            Dictionary<string, PoseData> poses = CreatePoses();

            AttackData light1 = LoadOrCreate<AttackData>($"{AttackFolder}/KatanaLight1.asset", out bool newLight);
            AttackData heavy = LoadOrCreate<AttackData>($"{AttackFolder}/KatanaHeavy.asset", out bool newHeavy);

            if (newLight)
            {
                // Spec 6.2 example values.
                light1.button = AttackButton.Light;
                light1.startupFrames = 4; light1.activeFrames = 3; light1.recoveryFrames = 10;
                light1.damage = 8; light1.hitstopFrames = 4; light1.hitstunFrames = 18;
                light1.knockback = new Vector2(4f, 1f); light1.inkGain = 5;
                light1.cancelWindowStart = 9; light1.cancelWindowEnd = 17;
                light1.cancelsInto = new List<AttackData> { heavy };   // Light 2 arrives in chunk 2
                light1.lungeSpeed = 1.5f; light1.screenShake = 0.05f;
                light1.hitboxes = new List<HitboxWindow> { Window(new Vector2(1.05f, 0.15f), new Vector2(1.4f, 0.6f)) };
                light1.poseClip = AttackClip("KatanaLight1", poses, light1, fadeIn: 2);
                EditorUtility.SetDirty(light1);
            }

            if (newHeavy)
            {
                // Heavy: 10 startup frames (visible wind-up), hitstop 8 = whole-game freeze (spec 6.4).
                heavy.button = AttackButton.Heavy;
                heavy.startupFrames = 10; heavy.activeFrames = 4; heavy.recoveryFrames = 18;
                heavy.damage = 18; heavy.hitstopFrames = 8; heavy.hitstunFrames = 30;
                heavy.knockback = new Vector2(9f, 4f); heavy.inkGain = 10;
                heavy.cancelWindowStart = 19; heavy.cancelWindowEnd = 32;
                heavy.lungeSpeed = 4f; heavy.screenShake = 0.15f;
                heavy.swingSound = "swing_heavy"; heavy.hitSound = "hit_heavy";
                heavy.hitboxes = new List<HitboxWindow> { Window(new Vector2(1.05f, 0.15f), new Vector2(1.5f, 1.5f)) };
                heavy.poseClip = AttackClip("KatanaHeavy", poses, heavy, fadeIn: 2);
                EditorUtility.SetDirty(heavy);
            }

            WeaponData katana = LoadOrCreate<WeaponData>($"{WeaponFolder}/BrushKatana.asset", out _);
            if (katana.light1 == null) katana.light1 = light1;
            if (katana.heavy == null) katana.heavy = heavy;
            EditorUtility.SetDirty(katana);

            CombatSettings settings = LoadOrCreate<CombatSettings>(SettingsPath, out _);
            AssetDatabase.SaveAssets();

            return new Result
            {
                Weapon = katana,
                Settings = settings,
                DummyIdle = AssetDatabase.LoadAssetAtPath<PoseData>(StarterPoses.PathFor("Idle")),
                DummyHit = poses["DummyHit"],
            };
        }

        private static Dictionary<string, PoseData> CreatePoses()
        {
            // Right-facing, positive = forward. Checked visually with the blade and hitboxes drawn on top.
            var table = new Dictionary<string, FigurePose>
            {
                ["KatanaLight1Windup"] = Planted(-5, 0, 200, 30, -30, 40, 15, -20, -15, -10),
                ["KatanaLight1Strike"] = Planted(15, -10, 95, 0, -40, 30, 40, -45, -25, -10),
                ["KatanaLight1Recover"] = Planted(8, -5, 60, 25, -20, 30, 30, -35, -20, -10),
                ["KatanaHeavyWindup"] = Planted(-15, 5, 190, 20, 160, 20, 25, -50, -10, -40),
                ["KatanaHeavyStrike"] = Planted(30, -20, 100, 0, 70, 20, 50, -60, -35, -5),
                ["KatanaHeavyRecover"] = Planted(25, -15, 70, 15, 40, 30, 45, -60, -30, -10),
                ["DummyHit"] = Planted(-20, 15, 40, 30, 60, 30, -10, -20, 10, -10),
            };

            var result = new Dictionary<string, PoseData>();
            foreach (KeyValuePair<string, FigurePose> entry in table)
            {
                PoseData pose = LoadOrCreate<PoseData>(StarterPoses.PathFor(entry.Key), out bool isNew);
                if (isNew)
                {
                    pose.pose = entry.Value;
                    EditorUtility.SetDirty(pose);
                }
                result[entry.Key] = pose;
            }
            return result;
        }

        private static FigurePose Planted(float spine, float neck, float sf, float ef, float sb, float eb,
                                          float hf, float kf, float hb, float kb)
        {
            return StarterPoses.Planted(StarterPoses.P(0f, 0f, spine, neck, sf, ef, sb, eb, hf, kf, hb, kb));
        }

        /// <summary>
        /// Clip timed to the frame data: hold the wind-up for the startup frames, snap to the strike for the
        /// active frames (crisp), then ease into the recover pose over the recovery frames.
        /// </summary>
        private static PoseClip AttackClip(string name, Dictionary<string, PoseData> poses, AttackData attack, int fadeIn)
        {
            PoseClip clip = LoadOrCreate<PoseClip>($"{ClipFolder}/{name}.asset", out bool isNew);
            if (!isNew) return clip;

            clip.loop = false;
            clip.fadeInFrames = fadeIn;
            clip.entries = new List<PoseClip.Entry>
            {
                new PoseClip.Entry { pose = poses[name + "Windup"], frames = attack.startupFrames, easing = PoseEasing.Snap },
                new PoseClip.Entry { pose = poses[name + "Strike"], frames = attack.activeFrames, easing = PoseEasing.Snap },
                new PoseClip.Entry { pose = poses[name + "Strike"], frames = Mathf.Max(1, attack.recoveryFrames), easing = PoseEasing.EaseOut },
                new PoseClip.Entry { pose = poses[name + "Recover"], frames = 1, easing = PoseEasing.Linear },
            };
            EditorUtility.SetDirty(clip);
            return clip;
        }

        private static HitboxWindow Window(Vector2 offset, Vector2 size) => new HitboxWindow
        {
            boxes = new List<HitboxShape> { new HitboxShape { offset = offset, size = size } },
        };

        private static T LoadOrCreate<T>(string path, out bool created) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            created = asset == null;
            if (created)
            {
                asset = ScriptableObject.CreateInstance<T>();
                AssetDatabase.CreateAsset(asset, path);
            }
            return asset;
        }
    }
}
