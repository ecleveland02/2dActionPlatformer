using System.Collections.Generic;
using Margin.Combat;
using Margin.Rendering;
using Margin.Weapons;
using UnityEditor;
using UnityEngine;

namespace Margin.EditorTools
{
    /// <summary>
    /// Creates the starter Brush Katana moveset (Milestone 3): a four-hit light string plus a heavy branch off
    /// each of the first three lights, with poses, clips, hitboxes and CombatSettings.
    ///
    ///   L1 Descending cut > L2 Rising cut > L3 Stepping thrust > L4 Sweeping finisher
    ///   Heavy from neutral: Overhead cleave
    ///   L1 > Heavy: Iaido lunge    L2 > Heavy: Rising Moon (launcher)    L3 > Heavy: Falling Blossom (leaping cleave)
    ///   Air: Air Light 1 > 2 > 3, Heavy in the air: Air Slam (spikes the target). Rising Moon chains into Air Light 1.
    ///   Special (50 ink): Ink Wave projectile
    ///
    /// Each strike pose was checked with the blade drawn and the hitbox fitted around it. Every move's recover
    /// pose leads into the next move's wind-up so the string flows. Existing assets are kept unless you choose
    /// Overwrite in the menu version.
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
            /// <summary>Every starter attack by name (e.g. "GruntSwing").</summary>
            public Dictionary<string, AttackData> Attacks;
            /// <summary>Every starter combat pose by name (e.g. "GruntIdle").</summary>
            public Dictionary<string, PoseData> Poses;
            /// <summary>The sparring dummy's attack pattern: jab, jab, unparryable smash. A jab that hits becomes jab > cross > kick.</summary>
            public List<AttackData> SparringAttacks;
        }

        private sealed class Move
        {
            public string Name;
            public AttackButton Button;
            public int Startup, Active, Recovery;
            public int Damage, Hitstop, Hitstun, Ink;
            public Vector2 Knockback;
            public bool Launches;
            public int CancelStart, CancelEnd;
            public float Lunge;
            public int LungeFirst = 1;
            public float Hop;
            public int HopFrame = 1;
            public Vector2 BoxCenter, BoxSize;
            public float Shake;
            public int FadeIn = 1;
            public string Swing = "swing_light", Hit = "hit_light";
            public bool Airborne;
            public bool Unparryable;
            public float AirGravity = 1f, Hover;
            public int InkCost;
            /// <summary>No pose clip (the attacker isn't a stick figure, e.g. the Scribble Bat).</summary>
            public bool NoClip;
            public float ProjectileSpeed;
            public int ProjectileLifetime = 40;
            public Vector2 ProjectileSize = new Vector2(0.9f, 1.3f), ProjectileOffset = new Vector2(0.8f, 0.2f);
        }

        // Light hitstun is 6 frames longer than the spec example so a held (hold-to-heavy) branch still combos.
        private static readonly Move[] Moves =
        {
            new Move { Name = "KatanaLight1", Button = AttackButton.Light, Startup = 4, Active = 3, Recovery = 10,
                Damage = 8, Hitstop = 4, Hitstun = 24, Ink = 5, Knockback = new Vector2(4f, 1f), CancelStart = 9, CancelEnd = 17,
                Lunge = 1.5f, BoxCenter = new Vector2(1.05f, 0.15f), BoxSize = new Vector2(1.4f, 0.6f), Shake = 0.05f, FadeIn = 2 },
            new Move { Name = "KatanaLight2", Button = AttackButton.Light, Startup = 4, Active = 3, Recovery = 11,
                Damage = 9, Hitstop = 4, Hitstun = 26, Ink = 5, Knockback = new Vector2(3f, 2f), CancelStart = 9, CancelEnd = 18,
                Lunge = 1.5f, BoxCenter = new Vector2(0.85f, 0.5f), BoxSize = new Vector2(1.2f, 1.8f), Shake = 0.05f },
            new Move { Name = "KatanaLight3", Button = AttackButton.Light, Startup = 6, Active = 3, Recovery = 12,
                Damage = 11, Hitstop = 5, Hitstun = 28, Ink = 6, Knockback = new Vector2(5f, 1f), CancelStart = 11, CancelEnd = 21,
                Lunge = 7f, LungeFirst = 3, BoxCenter = new Vector2(1.25f, 0.2f), BoxSize = new Vector2(1.6f, 0.4f), Shake = 0.07f },
            new Move { Name = "KatanaLight4", Button = AttackButton.Light, Startup = 7, Active = 5, Recovery = 18,
                Damage = 16, Hitstop = 7, Hitstun = 28, Ink = 8, Knockback = new Vector2(9f, 3f), CancelStart = 18, CancelEnd = 30,
                Lunge = 3f, BoxCenter = new Vector2(0.9f, 0.3f), BoxSize = new Vector2(2.0f, 1.4f), Shake = 0.12f,
                Swing = "swing_heavy", Hit = "hit_heavy" },
            new Move { Name = "KatanaHeavy", Button = AttackButton.Heavy, Startup = 10, Active = 4, Recovery = 18,
                Damage = 18, Hitstop = 8, Hitstun = 30, Ink = 10, Knockback = new Vector2(9f, 4f), CancelStart = 19, CancelEnd = 32,
                Lunge = 4f, BoxCenter = new Vector2(1.05f, 0.15f), BoxSize = new Vector2(1.5f, 1.5f), Shake = 0.15f, FadeIn = 2,
                Swing = "swing_heavy", Hit = "hit_heavy" },
            new Move { Name = "KatanaIaido", Button = AttackButton.Heavy, Startup = 8, Active = 4, Recovery = 18,
                Damage = 16, Hitstop = 8, Hitstun = 30, Ink = 10, Knockback = new Vector2(10f, 2f), CancelStart = 17, CancelEnd = 30,
                Lunge = 12f, LungeFirst = 9, BoxCenter = new Vector2(1.35f, -0.05f), BoxSize = new Vector2(1.8f, 0.5f), Shake = 0.15f,
                Swing = "swing_heavy", Hit = "hit_heavy" },
            // Launcher: you rise with the target (hop 11) and can chain straight into the air string on hit.
            new Move { Name = "KatanaRisingMoon", Button = AttackButton.Heavy, Startup = 8, Active = 4, Recovery = 20,
                Damage = 14, Hitstop = 8, Hitstun = 40, Ink = 10, Knockback = new Vector2(1f, 12f), Launches = true,
                CancelStart = 17, CancelEnd = 32, Hop = 11f, HopFrame = 9,
                BoxCenter = new Vector2(0.75f, 0.6f), BoxSize = new Vector2(1.3f, 2.0f), Shake = 0.15f,
                Swing = "swing_heavy", Hit = "hit_heavy" },
            new Move { Name = "KatanaFallingBlossom", Button = AttackButton.Heavy, Startup = 10, Active = 4, Recovery = 20,
                Damage = 22, Hitstop = 10, Hitstun = 35, Ink = 12, Knockback = new Vector2(6f, 1f), CancelStart = 20, CancelEnd = 34,
                Hop = 9f, HopFrame = 1, BoxCenter = new Vector2(1.0f, -0.1f), BoxSize = new Vector2(1.6f, 1.8f), Shake = 0.2f,
                Swing = "swing_heavy", Hit = "hit_heavy" },

            // Air string: each hit bumps the target up a little (knockback y 5) and holds you level with it
            // (low gravity while attacking, hover on hit). Air Slam spikes the target back to the ground.
            new Move { Name = "KatanaAirLight1", Button = AttackButton.Light, Airborne = true, Startup = 4, Active = 3, Recovery = 10,
                Damage = 6, Hitstop = 4, Hitstun = 24, Ink = 4, Knockback = new Vector2(1.5f, 5f), CancelStart = 8, CancelEnd = 17,
                AirGravity = 0.3f, Hover = 2.5f, BoxCenter = new Vector2(1.05f, 0.3f), BoxSize = new Vector2(1.5f, 0.8f), Shake = 0.05f, FadeIn = 2 },
            new Move { Name = "KatanaAirLight2", Button = AttackButton.Light, Airborne = true, Startup = 4, Active = 3, Recovery = 10,
                Damage = 6, Hitstop = 4, Hitstun = 24, Ink = 4, Knockback = new Vector2(1.5f, 5f), CancelStart = 8, CancelEnd = 17,
                AirGravity = 0.3f, Hover = 2.5f, BoxCenter = new Vector2(0.8f, 0.8f), BoxSize = new Vector2(1.3f, 1.8f), Shake = 0.05f },
            new Move { Name = "KatanaAirLight3", Button = AttackButton.Light, Airborne = true, Startup = 5, Active = 3, Recovery = 12,
                Damage = 8, Hitstop = 5, Hitstun = 26, Ink = 5, Knockback = new Vector2(2f, 5f), CancelStart = 9, CancelEnd = 20,
                AirGravity = 0.3f, Hover = 2.5f, BoxCenter = new Vector2(1.25f, 0.45f), BoxSize = new Vector2(1.6f, 0.5f), Shake = 0.07f },
            new Move { Name = "KatanaAirSlam", Button = AttackButton.Heavy, Airborne = true, Startup = 6, Active = 4, Recovery = 16,
                Damage = 14, Hitstop = 10, Hitstun = 40, Ink = 8, Knockback = new Vector2(2f, -22f), CancelStart = 13, CancelEnd = 26,
                AirGravity = 0.3f, BoxCenter = new Vector2(0.9f, -0.2f), BoxSize = new Vector2(1.6f, 1.6f), Shake = 0.2f, FadeIn = 2,
                Swing = "swing_heavy", Hit = "hit_heavy" },

            // Sparring dummy attacks (spec 9: at least 12 frames of visible startup). The smash can't be parried
            // and flashes red while winding up (spec 6.5).
            // The jab opens a string (jab > cross > kick) when it hits: small knockback keeps the player in reach,
            // and each follow-up connects while the player is still in hitstun. A missed jab never chains.
            new Move { Name = "DummyJab", Button = AttackButton.Light, Startup = 16, Active = 3, Recovery = 20,
                Damage = 8, Hitstop = 6, Hitstun = 16, Ink = 0, Knockback = JabKnockback, CancelStart = 20, CancelEnd = 28,
                BoxCenter = new Vector2(0.75f, 0.35f), BoxSize = new Vector2(0.9f, 0.5f), Shake = 0.06f, FadeIn = 0 },
            new Move { Name = "DummyJab2", Button = AttackButton.Light, Startup = 8, Active = 3, Recovery = 18,
                Damage = 6, Hitstop = 6, Hitstun = 18, Ink = 0, Knockback = JabKnockback, CancelStart = 13, CancelEnd = 20,
                BoxCenter = new Vector2(0.8f, 0.35f), BoxSize = new Vector2(1.0f, 0.5f), Shake = 0.06f, FadeIn = 0 },
            new Move { Name = "DummyKick", Button = AttackButton.Heavy, Startup = 10, Active = 4, Recovery = 24,
                Damage = 10, Hitstop = 8, Hitstun = 30, Ink = 0, Knockback = new Vector2(7f, 6f), CancelStart = 39, CancelEnd = 39,
                BoxCenter = new Vector2(0.75f, -0.2f), BoxSize = new Vector2(1.0f, 0.6f), Shake = 0.12f, FadeIn = 1,
                Swing = "swing_heavy", Hit = "hit_heavy" },
            new Move { Name = "DummySmash", Button = AttackButton.Heavy, Startup = 24, Active = 4, Recovery = 28,
                Damage = 18, Hitstop = 8, Hitstun = 24, Ink = 0, Knockback = new Vector2(8f, 4f), CancelStart = 56, CancelEnd = 56,
                BoxCenter = new Vector2(0.8f, 0.1f), BoxSize = new Vector2(1.2f, 1.2f), Shake = 0.15f, FadeIn = 0,
                Unparryable = true },

            // Doodle Grunt (spec 9: walks, 1 slow swing). The haymaker has a long 20-frame wind-up; on hit it chains
            // into a two-handed shove that knocks the player away. Hitboxes sit slightly inside the arm's reach.
            new Move { Name = "GruntSwing", Button = AttackButton.Heavy, Startup = 20, Active = 4, Recovery = 22,
                Damage = 12, Hitstop = 7, Hitstun = 22, Ink = 0, Knockback = new Vector2(2f, 0f), CancelStart = 26, CancelEnd = 34,
                BoxCenter = new Vector2(0.6f, 0.15f), BoxSize = new Vector2(0.8f, 0.7f), Shake = 0.1f, FadeIn = 2,
                Swing = "swing_heavy", Hit = "hit_heavy" },
            new Move { Name = "GruntShove", Button = AttackButton.Heavy, Startup = 10, Active = 3, Recovery = 24,
                Damage = 8, Hitstop = 8, Hitstun = 30, Ink = 0, Knockback = new Vector2(8f, 5f), CancelStart = 37, CancelEnd = 37,
                BoxCenter = new Vector2(0.65f, 0.3f), BoxSize = new Vector2(0.7f, 0.7f), Shake = 0.15f, FadeIn = 1,
                Swing = "swing_heavy", Hit = "hit_heavy" },

            // Pencil Lancer (spec 9: long telegraphed thrust, teaches parry). The thrust lunges and has a 28-frame
            // wind-up; on hit it chains into a low sweep that pops the player up. Every third opener is a charged
            // thrust that can't be parried (red flash): dash through it or jump.
            new Move { Name = "LancerThrust", Button = AttackButton.Heavy, Startup = 28, Active = 4, Recovery = 26,
                Damage = 14, Hitstop = 7, Hitstun = 24, Ink = 0, Knockback = new Vector2(2f, 0f), CancelStart = 34, CancelEnd = 42,
                Lunge = 5f, LungeFirst = 27, BoxCenter = new Vector2(1.45f, 0.25f), BoxSize = new Vector2(1.3f, 0.3f),
                Shake = 0.1f, FadeIn = 2, Swing = "swing_heavy", Hit = "hit_heavy" },
            new Move { Name = "LancerSweep", Button = AttackButton.Heavy, Startup = 9, Active = 4, Recovery = 24,
                Damage = 8, Hitstop = 8, Hitstun = 32, Ink = 0, Knockback = new Vector2(3f, 10f), Launches = true,
                CancelStart = 37, CancelEnd = 37, BoxCenter = new Vector2(1.3f, -0.35f), BoxSize = new Vector2(1.2f, 0.5f),
                Shake = 0.12f, FadeIn = 1, Swing = "swing_heavy", Hit = "hit_heavy" },
            new Move { Name = "LancerCharge", Button = AttackButton.Heavy, Startup = 36, Active = 5, Recovery = 30,
                Damage = 18, Hitstop = 9, Hitstun = 28, Ink = 0, Knockback = new Vector2(7f, 3f), CancelStart = 71, CancelEnd = 71,
                Lunge = 8f, LungeFirst = 33, BoxCenter = new Vector2(1.5f, 0.17f), BoxSize = new Vector2(1.4f, 0.35f),
                Shake = 0.18f, FadeIn = 2, Swing = "swing_heavy", Hit = "hit_heavy", Unparryable = true },

            // Scribble Bat (spec 9: flies, dive attacks). 22-frame wind-up (wings raised), then an 18-frame dive with
            // the hitbox around its body. No follow-up: a single dive.
            new Move { Name = "BatDive", Button = AttackButton.Heavy, Startup = 22, Active = 18, Recovery = 24,
                Damage = 10, Hitstop = 6, Hitstun = 20, Ink = 0, Knockback = new Vector2(4f, 3f), CancelStart = 64, CancelEnd = 64,
                BoxCenter = Vector2.zero, BoxSize = new Vector2(0.8f, 0.6f), Shake = 0.08f, NoClip = true },

            // Special (spec 7): costs 50 ink, throws a crescent of ink. The blade itself has no hitbox.
            new Move { Name = "KatanaInkWave", Button = AttackButton.Special, Startup = 8, Active = 3, Recovery = 16,
                Damage = 20, Hitstop = 6, Hitstun = 30, Ink = 0, InkCost = 50, Knockback = new Vector2(7f, 2f),
                CancelStart = 20, CancelEnd = 27, ProjectileSpeed = 14f, ProjectileLifetime = 40, Shake = 0.12f, FadeIn = 2,
                Swing = "swing_heavy", Hit = "hit_heavy" },
        };

        private static readonly Vector2 JabKnockback = new Vector2(1.5f, 0f);
        private static readonly Vector2 OldJabKnockback = new Vector2(5f, 2f);

        /// <summary>Which moves each move chains into (the combo tree). Heavies end the string.</summary>
        private static readonly Dictionary<string, string[]> Chains = new Dictionary<string, string[]>
        {
            ["KatanaLight1"] = new[] { "KatanaLight2", "KatanaIaido" },
            ["KatanaLight2"] = new[] { "KatanaLight3", "KatanaRisingMoon" },
            ["KatanaLight3"] = new[] { "KatanaLight4", "KatanaFallingBlossom" },
            ["KatanaRisingMoon"] = new[] { "KatanaAirLight1" },
            ["KatanaAirLight1"] = new[] { "KatanaAirLight2", "KatanaAirSlam" },
            ["KatanaAirLight2"] = new[] { "KatanaAirLight3", "KatanaAirSlam" },
            ["KatanaAirLight3"] = new[] { "KatanaAirSlam" },
            // Sparring dummy string. Enemies always take the first entry.
            ["DummyJab"] = new[] { "DummyJab2" },
            ["DummyJab2"] = new[] { "DummyKick" },
            ["GruntSwing"] = new[] { "GruntShove" },
            ["LancerThrust"] = new[] { "LancerSweep" },
        };

        [MenuItem("Margin/Create Starter Combat Data")]
        public static void CreateFromMenu()
        {
            bool anyExists = AssetDatabase.LoadAssetAtPath<AttackData>($"{AttackFolder}/KatanaLight1.asset") != null;
            bool overwrite = anyExists && EditorUtility.DisplayDialog("Create Starter Combat Data",
                "Starter katana data already exists. Overwrite all starter attacks, clips and attack poses with the " +
                "original versions? (Choose 'Keep' to only add what's missing and keep your tuning.)", "Overwrite", "Keep");

            Result result = Create(overwrite);
            EditorGUIUtility.PingObject(result.Weapon);
            Debug.Log("Starter combat data ready: " + AssetDatabase.GetAssetPath(result.Weapon));
        }

        /// <summary>Creates anything missing without asking (used by the gym builder and repair menu).</summary>
        internal static Result EnsureCreated() => Create(overwrite: false);

        private static Result Create(bool overwrite)
        {
            StarterPoses.EnsureCreated();   // the base poses (Idle etc.) must exist first
            foreach (string folder in new[] { AttackFolder, WeaponFolder, ClipFolder })
                StickFigureRigEditor.EnsureFolder(folder);

            Dictionary<string, PoseData> poses = CreatePoses(overwrite);

            var attacks = new Dictionary<string, AttackData>();
            var created = new HashSet<string>();
            foreach (Move move in Moves)
            {
                AttackData attack = LoadOrCreate<AttackData>($"{AttackFolder}/{move.Name}.asset", out bool isNew);
                attacks[move.Name] = attack;
                if (isNew) created.Add(move.Name);
                if (isNew || overwrite) Apply(move, attack, poses, overwrite);
            }

            // Combo tree. Written for new or overwritten attacks. Also upgrades a Light 1 from the first combat
            // chunk (which cancelled into the neutral Heavy) the first time Light 2 appears.
            foreach (KeyValuePair<string, string[]> chain in Chains)
            {
                bool write = created.Contains(chain.Key) || overwrite ||
                             (chain.Key == "KatanaLight1" && created.Contains("KatanaLight2")) ||
                             (chain.Key == "KatanaRisingMoon" && created.Contains("KatanaAirLight1")) ||
                             (chain.Key == "DummyJab" && created.Contains("DummyJab2"));
                if (!write) continue;

                AttackData from = attacks[chain.Key];
                from.cancelsInto = new List<AttackData>();
                foreach (string to in chain.Value) from.cancelsInto.Add(attacks[to]);
                EditorUtility.SetDirty(from);
            }

            // Upgrade a Rising Moon from before air combat existed, unless it was retuned by hand.
            AttackData moon = attacks["KatanaRisingMoon"];
            if (created.Contains("KatanaAirLight1") && !created.Contains("KatanaRisingMoon"))
            {
                if (Mathf.Approximately(moon.hopVelocity, 6f)) moon.hopVelocity = 11f;
                if (moon.knockback == new Vector2(1.5f, 14f)) moon.knockback = new Vector2(1f, 12f);
                EditorUtility.SetDirty(moon);
            }

            // Upgrade a jab from before enemy combos (it knocked the player out of reach and never chained),
            // unless it was retuned by hand.
            AttackData jab = attacks["DummyJab"];
            if (created.Contains("DummyJab2") && !created.Contains("DummyJab") && jab.knockback == OldJabKnockback)
            {
                jab.knockback = JabKnockback;
                jab.cancelWindowStart = 20;
                jab.cancelWindowEnd = 28;
                EditorUtility.SetDirty(jab);
            }

            WeaponData katana = LoadOrCreate<WeaponData>($"{WeaponFolder}/BrushKatana.asset", out _);
            if (katana.light1 == null || overwrite) katana.light1 = attacks["KatanaLight1"];
            if (katana.light2 == null || overwrite) katana.light2 = attacks["KatanaLight2"];
            if (katana.light3 == null || overwrite) katana.light3 = attacks["KatanaLight3"];
            if (katana.light4 == null || overwrite) katana.light4 = attacks["KatanaLight4"];
            if (katana.heavy == null || overwrite) katana.heavy = attacks["KatanaHeavy"];
            if (katana.airLight == null || overwrite) katana.airLight = attacks["KatanaAirLight1"];
            if (katana.airHeavy == null || overwrite) katana.airHeavy = attacks["KatanaAirSlam"];
            if (katana.special == null || overwrite) katana.special = attacks["KatanaInkWave"];
            EditorUtility.SetDirty(katana);

            CombatSettings settings = LoadOrCreate<CombatSettings>(SettingsPath, out _);
            AssetDatabase.SaveAssets();

            FillPlayerAnimationSet(poses, overwrite);
            AssetDatabase.SaveAssets();
            // The multi-key player animations (hurt, Redraw, combo breaker, defeat and the rest) replace the
            // single-pose placeholders above: once for new projects, and again after an overwrite.
            if (overwrite) StarterAnimations.Apply();
            else StarterAnimations.EnsureApplied();

            return new Result
            {
                Weapon = katana,
                Settings = settings,
                DummyIdle = AssetDatabase.LoadAssetAtPath<PoseData>(StarterPoses.PathFor("Idle")),
                DummyHit = poses["DummyHit"],
                Attacks = attacks,
                Poses = poses,
                SparringAttacks = new List<AttackData> { attacks["DummyJab"], attacks["DummyJab"], attacks["DummySmash"] },
            };
        }

        private static void Apply(Move m, AttackData a, Dictionary<string, PoseData> poses, bool overwrite)
        {
            a.button = m.Button;
            a.direction = AttackDirection.Neutral;
            a.airborne = m.Airborne;
            a.parryable = !m.Unparryable;
            a.airGravityScale = m.AirGravity; a.hoverOnHit = m.Hover;
            a.inkCost = m.InkCost;
            a.projectileSpeed = m.ProjectileSpeed; a.projectileLifetimeFrames = m.ProjectileLifetime;
            a.projectileSize = m.ProjectileSize; a.projectileOffset = m.ProjectileOffset;
            a.startupFrames = m.Startup; a.activeFrames = m.Active; a.recoveryFrames = m.Recovery;
            a.damage = m.Damage; a.hitstopFrames = m.Hitstop; a.hitstunFrames = m.Hitstun; a.inkGain = m.Ink;
            a.knockback = m.Knockback; a.launches = m.Launches;
            a.cancelWindowStart = m.CancelStart; a.cancelWindowEnd = m.CancelEnd;
            a.chainsOnWhiff = true; a.whiffChainDelay = 4;
            a.cancelsIntoJump = true; a.cancelsIntoDash = true;
            a.cancelsInto = new List<AttackData>();
            a.lungeSpeed = m.Lunge; a.lungeFirstFrame = m.LungeFirst;
            a.hopVelocity = m.Hop; a.hopFrame = m.HopFrame;
            a.hitboxes = new List<HitboxWindow>();
            if (m.BoxSize != Vector2.zero)   // projectile-only moves have no blade hitbox
                a.hitboxes.Add(new HitboxWindow { boxes = new List<HitboxShape> { new HitboxShape { offset = m.BoxCenter, size = m.BoxSize } } });
            a.screenShake = m.Shake; a.swingSound = m.Swing; a.hitSound = m.Hit;
            a.poseClip = m.NoClip ? null : AttackClip(m, poses, overwrite);
            EditorUtility.SetDirty(a);
        }

        // Right-facing, positive = forward. An arm's world angle also includes the body lean,
        // so thrusts add the spine angle to stay level.
        private static Dictionary<string, PoseData> CreatePoses(bool overwrite)
        {
            var table = new Dictionary<string, FigurePose>
            {
                ["KatanaLight1Windup"] = Planted(-5, 0, 200, 30, -30, 40, 15, -20, -15, -10),
                ["KatanaLight1Strike"] = Planted(15, -10, 95, 0, -40, 30, 40, -45, -25, -10),
                ["KatanaLight1Recover"] = Planted(8, -5, 60, 25, -20, 30, 30, -35, -20, -10),
                ["KatanaLight2Windup"] = Planted(10, -5, 45, 5, -30, 40, 35, -50, -30, -10),
                ["KatanaLight2Strike"] = Planted(-5, 5, 125, 0, -60, 30, 25, -30, -20, -5),
                ["KatanaLight2Recover"] = Planted(0, 0, 165, 10, -50, 30, 25, -35, -20, -10),
                ["KatanaLight3Windup"] = Planted(5, 0, -40, 130, -10, 60, 10, -30, -20, -20),
                ["KatanaLight3Strike"] = Planted(20, -10, 110, 0, -50, 20, 60, -40, -45, 0),
                ["KatanaLight3Recover"] = Planted(12, -5, 90, 10, -40, 30, 45, -40, -35, -5),
                ["KatanaLight4Windup"] = Planted(-10, 5, -100, 20, 40, 30, 40, -80, -30, -60),
                ["KatanaLight4Strike"] = Planted(25, -10, 120, 0, -70, 20, 55, -35, -50, -5),
                ["KatanaLight4Recover"] = Planted(20, -10, 150, 10, -80, 20, 50, -40, -45, -10),
                ["KatanaHeavyWindup"] = Planted(-15, 5, 190, 20, 160, 20, 25, -50, -10, -40),
                ["KatanaHeavyStrike"] = Planted(30, -20, 100, 0, 70, 20, 50, -60, -35, -5),
                ["KatanaHeavyRecover"] = Planted(25, -15, 70, 15, 40, 30, 45, -60, -30, -10),
                ["KatanaIaidoWindup"] = Planted(15, -5, 30, -150, -20, 60, 30, -70, -20, -50),
                ["KatanaIaidoStrike"] = Planted(35, -15, 120, 0, -60, 20, 70, -30, -60, 0),
                ["KatanaIaidoRecover"] = Planted(25, -10, 100, 10, -50, 30, 55, -45, -50, -5),
                ["KatanaRisingMoonWindup"] = Planted(20, -10, -60, 30, -40, 40, 50, -100, -20, -80),
                ["KatanaRisingMoonStrike"] = Air(-10, 5, 140, 0, 150, 10, 20, -10, -30, -20),
                ["KatanaRisingMoonRecover"] = Air(0, 0, 175, 10, 140, 20, 20, -30, -20, -30),
                ["KatanaFallingBlossomWindup"] = Air(-15, 5, 200, 40, 170, 30, 60, -90, 20, -70),
                ["KatanaFallingBlossomStrike"] = Air(40, -15, 95, -5, 60, 20, 45, -60, -30, -20),
                ["KatanaFallingBlossomRecover"] = Planted(30, -10, 110, 0, 40, 30, 50, -90, 35, -85),
                ["DummyHit"] = Planted(-20, 15, 40, 30, 60, 30, -10, -20, 10, -10),
                ["KatanaAirLight1Windup"] = Air(-5, 0, 200, 30, -20, 40, 60, -90, -20, -60),
                ["KatanaAirLight1Strike"] = Air(10, -5, 105, 0, -40, 30, 40, -70, -30, -50),
                ["KatanaAirLight1Recover"] = Air(8, -5, 80, 20, -30, 30, 40, -70, -30, -50),
                ["KatanaAirLight2Windup"] = Air(5, 0, 70, 10, -30, 40, 45, -80, -25, -55),
                ["KatanaAirLight2Strike"] = Air(-10, 5, 120, 0, -50, 30, 35, -60, -30, -40),
                ["KatanaAirLight2Recover"] = Air(-5, 0, 170, 10, -40, 30, 35, -60, -30, -40),
                ["KatanaAirLight3Windup"] = Air(5, 0, -40, 130, -10, 60, 50, -90, -20, -60),
                ["KatanaAirLight3Strike"] = Air(20, -10, 115, 0, -50, 20, 30, -40, -40, -20),
                ["KatanaAirLight3Recover"] = Air(15, -5, 95, 10, -40, 30, 35, -50, -35, -30),
                ["KatanaAirSlamWindup"] = Air(-20, 10, 200, 40, 170, 30, 70, -110, 10, -90),
                ["KatanaAirSlamStrike"] = Air(40, -15, 70, -10, 50, 20, 20, -30, -20, -20),
                ["KatanaAirSlamRecover"] = Air(30, -10, 45, 10, 40, 30, 25, -40, -20, -30),
                // Player defensive poses.
                ["Parry"] = Planted(-8, 5, 100, 40, 60, 40, 30, -40, -25, -20),
                ["Redraw"] = Planted(10, 10, 60, 30, 10, 30, 80, -120, -30, -90),
                ["Hurt"] = Planted(-25, 20, -30, 40, 60, 30, -15, -25, 15, -15),
                // Sparring dummy (no sword): a jab and a two-handed overhead smash.
                ["DummyJabWindup"] = Planted(-10, 5, -60, 90, -30, 60, 20, -30, -20, -20),
                ["DummyJabStrike"] = Planted(15, -5, 100, 0, -40, 50, 35, -30, -30, -10),
                ["DummyJabRecover"] = Planted(8, 0, 60, 30, -30, 50, 25, -25, -20, -10),
                // Cross: the back hand punches (the jab's front hand pulls back). Kick: knee chambers, then extends.
                ["DummyJab2Windup"] = Planted(-5, 0, 40, 60, -50, 90, 25, -30, -25, -15),
                ["DummyJab2Strike"] = Planted(15, -5, -30, 60, 100, 0, 30, -25, -35, -10),
                ["DummyJab2Recover"] = Planted(8, 0, -20, 60, 70, 30, 25, -25, -25, -10),
                ["DummyKickWindup"] = Planted(-15, 5, 60, 40, -40, 50, 70, -110, -10, -15),
                ["DummyKickStrike"] = Planted(-25, 10, 70, 30, -30, 40, 95, 0, -15, -5),
                ["DummyKickRecover"] = Planted(-10, 5, 50, 40, -30, 40, 40, -40, -15, -10),
                // Doodle Grunt: hunched brute. Haymaker from behind the head, then a two-handed shove.
                ["GruntIdle"] = Planted(10, 5, 20, 40, -15, 40, 12, -18, -12, -10),
                ["GruntAlert"] = Planted(-10, -10, 150, 40, 140, 40, 15, -20, -15, -15),
                ["GruntSwingWindup"] = Planted(-15, 5, 200, 30, 30, 40, 20, -25, -20, -10),
                ["GruntSwingStrike"] = Planted(30, -10, 90, 0, -20, 40, 45, -50, -30, -5),
                ["GruntSwingRecover"] = Planted(20, -5, 50, 20, -10, 40, 35, -40, -25, -8),
                ["GruntShoveWindup"] = Planted(-5, 0, 10, 110, 0, 110, 25, -30, -25, -10),
                ["GruntShoveStrike"] = Planted(25, -5, 95, 0, 90, 5, 45, -20, -35, 0),
                ["GruntShoveRecover"] = Planted(12, 0, 70, 30, 65, 35, 30, -25, -25, -5),
                // Pencil Lancer: the pencil continues the front forearm, so a level thrust needs
                // shoulder + elbow = 90 + spine lean.
                ["LancerIdle"] = Planted(5, 0, 55, 35, 40, 60, 25, -20, -25, -10),
                ["LancerAlert"] = Planted(-5, -5, 120, 20, 60, 60, 25, -20, -25, -10),
                ["LancerThrustWindup"] = Planted(-10, 5, -20, 100, -30, 90, 30, -40, -30, -20),
                ["LancerThrustStrike"] = Planted(20, -10, 110, 0, 60, 20, 50, -30, -40, 0),
                ["LancerThrustRecover"] = Planted(12, -5, 90, 10, 45, 40, 40, -30, -30, -5),
                ["LancerSweepWindup"] = Planted(-5, 0, 160, -40, 100, 20, 30, -35, -25, -15),
                ["LancerSweepStrike"] = Planted(25, -10, 82, 5, 70, 20, 45, -40, -35, -5),
                ["LancerSweepRecover"] = Planted(15, -5, 70, 10, 55, 30, 35, -30, -30, -5),
                ["LancerChargeWindup"] = Planted(-20, 10, -35, 105, -40, 95, 35, -60, -35, -30),
                ["LancerChargeStrike"] = Planted(25, -12, 115, 0, 60, 15, 55, -25, -45, 0),
                ["LancerChargeRecover"] = Planted(15, -5, 90, 10, 45, 40, 40, -30, -30, -5),
                // Kneeling, leaning on the planted sword (part of the player's defeat animation).
                ["Defeated"] = StarterPoses.P(0f, -0.4f, spine: 25, neck: 25, sf: 70, ef: 10, sb: 20, eb: 30, hf: 75, kf: -120, hb: 0, kb: -90),
                // Combo breaker: arms flung wide, body upright.
                ["ComboBreaker"] = Planted(-5, 0, 110, 10, -110, 10, 20, -20, -20, -20),
                ["DummySmashWindup"] = Planted(-20, 10, 185, 10, 175, 10, 25, -40, -15, -30),
                ["DummySmashStrike"] = Planted(35, -15, 90, 0, 85, 0, 50, -60, -30, -10),
                ["DummySmashRecover"] = Planted(25, -10, 40, 20, 35, 20, 45, -60, -30, -10),
                ["KatanaInkWaveWindup"] = Planted(-10, 5, -110, 20, 60, 30, 35, -70, -25, -50),
                ["KatanaInkWaveStrike"] = Planted(25, -10, 115, 0, -70, 20, 55, -40, -50, -5),
                ["KatanaInkWaveRecover"] = Planted(18, -8, 130, 10, -60, 20, 50, -45, -45, -10),
            };

            var result = new Dictionary<string, PoseData>();
            foreach (KeyValuePair<string, FigurePose> entry in table)
            {
                PoseData pose = LoadOrCreate<PoseData>(StarterPoses.PathFor(entry.Key), out bool isNew);
                if (isNew || overwrite)
                {
                    pose.pose = entry.Value;
                    EditorUtility.SetDirty(pose);
                }
                result[entry.Key] = pose;
            }
            return result;
        }

        /// <summary>Hold clips for the player's parry, Redraw and hurt poses, added to the PlayerAnimationSet.</summary>
        private static void FillPlayerAnimationSet(Dictionary<string, PoseData> poses, bool overwrite)
        {
            var set = AssetDatabase.LoadAssetAtPath<Margin.Player.PlayerAnimationSet>("Assets/_Project/Data/PlayerAnimationSet.asset");
            if (set == null) return;
            if (set.parry == null || overwrite) set.parry = HoldClip("Parry", poses["Parry"], 0, overwrite);
            if (set.redraw == null || overwrite) set.redraw = HoldClip("Redraw", poses["Redraw"], 6, overwrite);
            if (set.hitstun == null || overwrite) set.hitstun = HoldClip("Hurt", poses["Hurt"], 0, overwrite);
            if (set.defeated == null || overwrite) set.defeated = HoldClip("Defeated", poses["Defeated"], 3, overwrite);
            if (set.comboBreaker == null || overwrite) set.comboBreaker = HoldClip("ComboBreaker", poses["ComboBreaker"], 0, overwrite);
            EditorUtility.SetDirty(set);
        }

        internal static PoseClip HoldClip(string name, PoseData pose, int fadeIn, bool overwrite)
        {
            PoseClip clip = LoadOrCreate<PoseClip>($"{ClipFolder}/{name}.asset", out bool isNew);
            if (!isNew && !overwrite) return clip;
            clip.loop = false;
            clip.fadeInFrames = fadeIn;
            clip.entries = new List<PoseClip.Entry> { new PoseClip.Entry { pose = pose, frames = 1 } };
            EditorUtility.SetDirty(clip);
            return clip;
        }

        private static FigurePose Planted(float spine, float neck, float sf, float ef, float sb, float eb,
                                          float hf, float kf, float hb, float kb)
        {
            return StarterPoses.Planted(Air(spine, neck, sf, ef, sb, eb, hf, kf, hb, kb));
        }

        /// <summary>Airborne pose: no foot planting.</summary>
        private static FigurePose Air(float spine, float neck, float sf, float ef, float sb, float eb,
                                      float hf, float kf, float hb, float kb)
        {
            return StarterPoses.P(0f, 0f, spine, neck, sf, ef, sb, eb, hf, kf, hb, kb);
        }

        /// <summary>
        /// Clip timed to the frame data, built to flow:
        ///   hold the wind-up (anticipation), swing into the strike over the last 2 startup frames (accelerating),
        ///   hold the strike for the active frames (crisp, matches the hitbox), then ease into the recover pose.
        /// The recover pose is chosen to lead into the next move's wind-up.
        /// </summary>
        private static PoseClip AttackClip(Move m, Dictionary<string, PoseData> poses, bool overwrite)
        {
            PoseClip clip = LoadOrCreate<PoseClip>($"{ClipFolder}/{m.Name}.asset", out bool isNew);
            if (!isNew && !overwrite) return clip;

            PoseData windup = poses[m.Name + "Windup"], strike = poses[m.Name + "Strike"], recover = poses[m.Name + "Recover"];
            int swing = Mathf.Min(2, m.Startup);
            clip.loop = false;
            clip.fadeInFrames = m.FadeIn;
            clip.entries = new List<PoseClip.Entry>();
            if (m.Startup > swing)
                clip.entries.Add(new PoseClip.Entry { pose = windup, frames = m.Startup - swing, easing = PoseEasing.Snap });
            clip.entries.Add(new PoseClip.Entry { pose = windup, frames = swing, easing = PoseEasing.EaseIn });
            clip.entries.Add(new PoseClip.Entry { pose = strike, frames = m.Active, easing = PoseEasing.Snap });
            clip.entries.Add(new PoseClip.Entry { pose = strike, frames = Mathf.Max(1, m.Recovery), easing = PoseEasing.EaseOut });
            clip.entries.Add(new PoseClip.Entry { pose = recover, frames = 1, easing = PoseEasing.Linear });
            EditorUtility.SetDirty(clip);
            return clip;
        }

        internal static T LoadOrCreate<T>(string path, out bool created) where T : ScriptableObject
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
