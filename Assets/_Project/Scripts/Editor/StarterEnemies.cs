using System.Collections.Generic;
using Margin.Combat;
using Margin.Enemies;
using Margin.Rendering;
using UnityEditor;
using UnityEngine;
using static Margin.EditorTools.StarterPoses;

namespace Margin.EditorTools
{
    /// <summary>
    /// Creates the starter enemy data (spec 9): the Doodle Grunt's and Pencil Lancer's EnemyData, body proportions
    /// and animation clips.
    /// Its attacks and poses come from StarterCombat. Existing assets are kept, so your tuning is never overwritten.
    /// </summary>
    public static class StarterEnemies
    {
        private const string EnemyFolder = "Assets/_Project/Data/Enemies";
        private const string ClipFolder = "Assets/_Project/Data/Animations";

        internal sealed class Result
        {
            public EnemyData Grunt;
            public EnemyData Lancer;
            public EnemyData Bat;
        }

        [MenuItem("Margin/Create Starter Enemy Data")]
        public static void CreateFromMenu()
        {
            Result result = EnsureCreated();
            EditorGUIUtility.PingObject(result.Grunt);
            Debug.Log("Starter enemy data ready: " + AssetDatabase.GetAssetPath(result.Grunt));
        }

        internal static Result EnsureCreated()
        {
            StarterCombat.Result combat = StarterCombat.EnsureCreated();
            StickFigureRigEditor.EnsureFolder(EnemyFolder);
            Dictionary<string, PoseData> poses = combat.Poses;

            // Chunky doodle: bigger head, thicker lines. Leg lengths match the player so the poses stay planted.
            var proportions = StarterCombat.LoadOrCreate<StickFigureProportions>($"{EnemyFolder}/GruntProportions.asset", out bool newProportions);
            if (newProportions)
            {
                proportions.headRadius = 0.22f;
                proportions.lineWidth = 0.085f;
                proportions.upperArm = 0.32f;
                proportions.forearm = 0.30f;
                EditorUtility.SetDirty(proportions);
            }

            var grunt = StarterCombat.LoadOrCreate<EnemyData>($"{EnemyFolder}/DoodleGrunt.asset", out bool newGrunt);
            if (newGrunt)
            {
                grunt.maxHealth = 40;
                grunt.walkSpeed = 2.2f;
                grunt.accelerationFrames = 6;
                grunt.patrolDistance = 2f;
                grunt.patrolPauseFrames = 60;
                grunt.noticeRange = 7f;
                grunt.noticeHeight = 2.5f;
                grunt.giveUpRange = 12f;
                grunt.alertFrames = 24;
                grunt.attackRange = 1.2f;
                grunt.attacks = new List<AttackData> { combat.Attacks["GruntSwing"] };
                grunt.attackCooldownFrames = 50;
                grunt.personalSpace = 0.9f;
                grunt.knockbackTaken = 1f;
                grunt.deathFrames = 40;
                grunt.proportions = proportions;
            }

            // Clips: fill any that are missing (also upgrades an EnemyData made by hand without clips).
            if (grunt.idle == null) grunt.idle = StarterCombat.HoldClip("GruntIdle", poses["GruntIdle"], 6, false);
            if (grunt.walk == null) grunt.walk = WalkClip("GruntWalk", 5);
            if (grunt.alert == null) grunt.alert = StarterCombat.HoldClip("GruntAlert", poses["GruntAlert"], 0, false);
            if (grunt.hurt == null) grunt.hurt = StarterCombat.HoldClip("GruntHurt", poses["DummyHit"], 0, false);
            if (grunt.defeated == null) grunt.defeated = StarterCombat.HoldClip("GruntDefeated", poses["Defeated"], 3, false);
            EditorUtility.SetDirty(grunt);

            EnemyData lancer = CreateLancer(combat);
            EnemyData bat = CreateBat(combat);
            AssetDatabase.SaveAssets();
            if (AssetDatabase.LoadAssetAtPath<PoseClip>($"{ClipFolder}/GruntTurn.asset") == null) ApplyAnimations();

            return new Result { Grunt = grunt, Lancer = lancer, Bat = bat };
        }

        /// <summary>
        /// Pencil Lancer: tall and thin, keeps its distance (backs off when you get inside its spear), attacks from
        /// 2 units away with a lunging thrust. Openers cycle thrust, thrust, unparryable charged thrust.
        /// </summary>
        private static EnemyData CreateLancer(StarterCombat.Result combat)
        {
            Dictionary<string, PoseData> poses = combat.Poses;
            var proportions = StarterCombat.LoadOrCreate<StickFigureProportions>($"{EnemyFolder}/LancerProportions.asset", out bool newProportions);
            if (newProportions)
            {
                proportions.spine = 0.62f;
                proportions.headRadius = 0.15f;
                proportions.lineWidth = 0.05f;
                EditorUtility.SetDirty(proportions);
            }

            var lancer = StarterCombat.LoadOrCreate<EnemyData>($"{EnemyFolder}/PencilLancer.asset", out bool isNew);
            if (isNew)
            {
                lancer.maxHealth = 50;
                lancer.walkSpeed = 1.8f;
                lancer.accelerationFrames = 8;
                lancer.patrolDistance = 1.5f;
                lancer.patrolPauseFrames = 80;
                lancer.noticeRange = 8f;
                lancer.noticeHeight = 2.5f;
                lancer.giveUpRange = 13f;
                lancer.alertFrames = 24;
                lancer.attackRange = 2.0f;
                lancer.minAttackRange = 0.6f;
                lancer.retreatDistance = 1.1f;
                AttackData thrust = combat.Attacks["LancerThrust"];
                lancer.attacks = new List<AttackData> { thrust, thrust, combat.Attacks["LancerCharge"] };
                lancer.attackCooldownFrames = 60;
                lancer.personalSpace = 0.9f;
                lancer.knockbackTaken = 0.9f;
                lancer.deathFrames = 40;
                lancer.proportions = proportions;
                lancer.weaponLength = 1.4f;
            }
            if (lancer.weaponLook == null)
            {
                var pencil = StarterCombat.LoadOrCreate<WeaponLook>($"{EnemyFolder}/PencilLook.asset", out bool newPencil);
                if (newPencil)
                {
                    pencil.style = WeaponStyle.Pencil;
                    pencil.twoHanded = false;
                    pencil.scabbard = false;
                    EditorUtility.SetDirty(pencil);
                }
                lancer.weaponLook = pencil;
            }

            if (lancer.idle == null) lancer.idle = StarterCombat.HoldClip("LancerIdle", poses["LancerIdle"], 6, false);
            if (lancer.walk == null) lancer.walk = WalkClip("LancerWalk", 6, "WalkArmed");
            if (lancer.alert == null) lancer.alert = StarterCombat.HoldClip("LancerAlert", poses["LancerAlert"], 3, false);
            if (lancer.hurt == null) lancer.hurt = StarterCombat.HoldClip("GruntHurt", poses["DummyHit"], 0, false);
            if (lancer.defeated == null) lancer.defeated = StarterCombat.HoldClip("GruntDefeated", poses["Defeated"], 3, false);
            EditorUtility.SetDirty(lancer);
            return lancer;
        }

        /// <summary>
        /// Scribble Bat: fragile (20 HP) and light (knocked around easily). Hovers above and beside you, then dives.
        /// Drawn procedurally by ScribbleBatVisual, so it has no pose clips.
        /// </summary>
        private static EnemyData CreateBat(StarterCombat.Result combat)
        {
            var bat = StarterCombat.LoadOrCreate<EnemyData>($"{EnemyFolder}/ScribbleBat.asset", out bool isNew);
            if (!isNew) return bat;
            bat.maxHealth = 20;
            bat.patrolDistance = 1.5f;
            bat.noticeRange = 7f;
            bat.noticeHeight = 4f;
            bat.giveUpRange = 12f;
            bat.alertFrames = 20;
            bat.attacks = new List<AttackData> { combat.Attacks["BatDive"] };
            bat.attackCooldownFrames = 70;
            bat.knockbackTaken = 1.3f;
            bat.deathFrames = 30;
            bat.hoverHeight = 2.4f;
            bat.hoverSide = 2.2f;
            bat.flySpeed = 3.5f;
            bat.flyAccelerationFrames = 20;
            bat.diveSpeed = 11f;
            EditorUtility.SetDirty(bat);
            return bat;
        }

        // ---------------- full enemy animation set ----------------

        /// <summary>
        /// Multi-key enemy clips: idle loops with breathing, turn, alert (startle then ready), hurt recoil,
        /// knockdown (flat on the back), get-up (sit, kneel, stand), defeat (stagger, kneel, fall face down).
        /// Poses checked offline like the player's: feet and body within 5 cm of the floor through every blend,
        /// and the Lancer's 1.4-unit pencil never under it. Also run by Margin > Upgrade Animations.
        /// Rewrites the starter grunt and lancer clips and fills their EnemyData slots.
        /// </summary>
        internal static void ApplyAnimations()
        {
            foreach (KeyValuePair<string, FigurePose> pose in EnemyPoses()) WritePose(pose.Key, pose.Value);
            AssetDatabase.SaveAssets();
            foreach (KeyValuePair<string, ClipSpec> clip in EnemyClips()) WriteClip(clip.Key, clip.Value);
            AssetDatabase.SaveAssets();

            PoseClip C(string name) => AssetDatabase.LoadAssetAtPath<PoseClip>($"{ClipFolder}/{name}.asset");
            foreach (string enemy in new[] { "Grunt", "Lancer" })
            {
                string file = enemy == "Grunt" ? "DoodleGrunt" : "PencilLancer";
                var data = AssetDatabase.LoadAssetAtPath<EnemyData>($"{EnemyFolder}/{file}.asset");
                if (data == null) continue;
                data.idle = C($"{enemy}Idle");
                data.idleBreathing = C($"{enemy}Breathing");
                data.turn = C($"{enemy}Turn");
                data.alert = C($"{enemy}Alert");
                data.hurt = C($"{enemy}Hurt");
                data.getUp = C($"{enemy}GetUp");
                data.knockdown = C("EnemyKnockdown");
                data.defeated = C("EnemyDefeated");
                EditorUtility.SetDirty(data);
            }
            AssetDatabase.SaveAssets();
            Debug.Log("Enemy animations upgraded: multi-key clips, knockdown and get-up.");
        }

        private static Dictionary<string, FigurePose> EnemyPoses() => new Dictionary<string, FigurePose>
        {
            ["GruntIdleB"] = P(0f, -0.013f, spine: 13, neck: 3, sf: 26, ef: 48, sb: -10, eb: 46, hf: 12, kf: -20, hb: -12, kb: -12),
            ["GruntBreath"] = P(0f, -0.023f, spine: 12, neck: 4, sf: 23, ef: 43, sb: -13, eb: 43, hf: 12, kf: -20, hb: -12, kb: -12),
            ["GruntTurnA"] = P(0f, -0.051f, spine: -5, neck: 5, sf: -20, ef: 60, sb: 30, eb: 60, hf: 15, kf: -40, hb: -30, kb: -15),
            ["GruntTurnB"] = P(0f, -0.039f, spine: 15, neck: 0, sf: 35, ef: 50, sb: -20, eb: 50, hf: 25, kf: -30, hb: -20, kb: -10),
            ["GruntReady"] = P(0f, -0.044f, spine: 12, neck: -5, sf: 60, ef: 110, sb: 45, eb: 120, hf: 25, kf: -35, hb: -25, kb: -20),
            ["GruntHurtSnap"] = P(0f, -0.026f, spine: -30, neck: 25, sf: 150, ef: 30, sb: -40, eb: 40, hf: -15, kf: -25, hb: 20, kb: -15),
            ["GruntHurtStagger"] = P(0f, -0.067f, spine: -10, neck: 10, sf: 40, ef: 50, sb: 10, eb: 50, hf: 15, kf: -45, hb: -15, kb: -30),
            ["LancerIdleB"] = P(0f, -0.044f, spine: 7, neck: -2, sf: 57, ef: 35, sb: 42, eb: 62, hf: 27, kf: -24, hb: -24, kb: -12),
            ["LancerBreath"] = P(0f, -0.048f, spine: 7, neck: -1, sf: 56, ef: 36, sb: 42, eb: 62, hf: 25, kf: -22, hb: -25, kb: -12),
            ["LancerTurnA"] = P(0f, -0.051f, spine: -8, neck: 5, sf: 85, ef: 5, sb: 20, eb: 60, hf: 15, kf: -40, hb: -30, kb: -15),
            ["LancerTurnB"] = P(0f, -0.055f, spine: 10, neck: -3, sf: 70, ef: 30, sb: 45, eb: 55, hf: 30, kf: -25, hb: -25, kb: -12),
            ["LancerReady"] = P(0f, -0.078f, spine: 12, neck: -5, sf: 75, ef: 27, sb: 40, eb: 60, hf: 35, kf: -45, hb: -30, kb: -20),
            ["LancerHurtSnap"] = P(0f, -0.026f, spine: -30, neck: 25, sf: 160, ef: 20, sb: -30, eb: 40, hf: -15, kf: -25, hb: 20, kb: -15),
            ["LancerHurtStagger"] = P(0f, -0.067f, spine: -10, neck: 10, sf: 85, ef: 25, sb: 20, eb: 50, hf: 15, kf: -45, hb: -15, kb: -30),
            ["EnemyKnockImpact"] = P(0f, -0.68f, spine: -10, neck: 15, sf: 150, ef: 20, sb: 120, eb: 30, hf: 60, kf: -40, hb: 40, kb: -60, tilt: -70),
            ["EnemyLieBack"] = P(0f, -0.666f, spine: 0, neck: 10, sf: 170, ef: 10, sb: 150, eb: 20, hf: 10, kf: -15, hb: 5, kb: -30, tilt: -90),
            ["EnemyGetUpSit"] = P(0f, -0.499f, spine: -20, neck: 15, sf: 130, ef: 30, sb: -60, eb: 10, hf: 90, kf: -120, hb: 90, kb: -150, tilt: -40),
            ["EnemyGetUpKneel"] = P(0f, -0.337f, spine: 15, neck: 0, sf: 100, ef: 40, sb: 20, eb: 40, hf: 75, kf: -120, hb: 0, kb: -90, tilt: -10),
            ["EnemyDefeatStagger"] = P(0f, -0.1f, spine: -25, neck: 20, sf: 150, ef: 20, sb: 30, eb: 40, hf: 10, kf: -50, hb: -10, kb: -40),
            ["EnemyDefeatKneel"] = P(0f, -0.4f, spine: 30, neck: 35, sf: 110, ef: 20, sb: 10, eb: 20, hf: 75, kf: -120, hb: 0, kb: -90),
            ["EnemyDefeatFall"] = P(0f, -0.38f, spine: 20, neck: 20, sf: 160, ef: 10, sb: 60, eb: 20, hf: 40, kf: -100, hb: 0, kb: -80, tilt: 40),
            ["EnemyLieFront"] = P(0f, -0.534f, spine: 0, neck: 15, sf: 160, ef: 15, sb: 150, eb: 10, hf: 0, kf: -10, hb: -5, kb: -20, tilt: 90),
        };

        private const PoseEasing Linear = PoseEasing.Linear, In = PoseEasing.EaseIn, Out = PoseEasing.EaseOut,
                                 InOut = PoseEasing.EaseInOut, Smooth = PoseEasing.Smooth;

        /// <summary>Knockdown lies still after its impact; get-up fits CombatSettings.getUpFrames (24); defeat is
        /// flat by frame 28 of the 40 before it vanishes.</summary>
        private static Dictionary<string, ClipSpec> EnemyClips() => new Dictionary<string, ClipSpec>
        {
            ["GruntIdle"] = new ClipSpec(true, 6, ("GruntIdle", 40, Smooth), ("GruntIdleB", 40, Smooth)),
            ["GruntBreathing"] = new ClipSpec(true, 0, ("GruntIdle", 40, InOut), ("GruntBreath", 40, InOut)),
            ["GruntTurn"] = new ClipSpec(false, 1, ("GruntTurnA", 3, Out), ("GruntTurnB", 4, InOut), ("GruntIdle", 1, Linear)),
            ["GruntAlert"] = new ClipSpec(false, 0, ("GruntAlert", 6, Out), ("GruntReady", 12, InOut), ("GruntReady", 1, Linear)),
            ["GruntHurt"] = new ClipSpec(false, 0, ("GruntHurtSnap", 3, Out), ("GruntHurtStagger", 8, InOut), ("GruntHurtStagger", 1, Linear)),
            ["GruntGetUp"] = new ClipSpec(false, 0, ("EnemyLieBack", 5, InOut), ("EnemyGetUpSit", 7, InOut), ("EnemyGetUpKneel", 7, InOut), ("GruntIdle", 5, InOut), ("GruntIdle", 1, Linear)),
            ["LancerIdle"] = new ClipSpec(true, 6, ("LancerIdle", 45, Smooth), ("LancerIdleB", 45, Smooth)),
            ["LancerBreathing"] = new ClipSpec(true, 0, ("LancerIdle", 45, InOut), ("LancerBreath", 45, InOut)),
            ["LancerTurn"] = new ClipSpec(false, 1, ("LancerTurnA", 3, Out), ("LancerTurnB", 4, InOut), ("LancerIdle", 1, Linear)),
            ["LancerAlert"] = new ClipSpec(false, 0, ("LancerAlert", 6, Out), ("LancerReady", 12, InOut), ("LancerReady", 1, Linear)),
            ["LancerHurt"] = new ClipSpec(false, 0, ("LancerHurtSnap", 3, Out), ("LancerHurtStagger", 8, InOut), ("LancerHurtStagger", 1, Linear)),
            ["LancerGetUp"] = new ClipSpec(false, 0, ("EnemyLieBack", 5, InOut), ("EnemyGetUpSit", 7, InOut), ("EnemyGetUpKneel", 7, InOut), ("LancerIdle", 5, InOut), ("LancerIdle", 1, Linear)),
            ["EnemyKnockdown"] = new ClipSpec(false, 0, ("EnemyKnockImpact", 4, Out), ("EnemyLieBack", 1, Linear)),
            ["EnemyDefeated"] = new ClipSpec(false, 2, ("EnemyDefeatStagger", 8, Out), ("EnemyDefeatKneel", 12, InOut), ("EnemyDefeatFall", 8, In), ("EnemyLieFront", 1, Linear)),
        };

        /// <summary>The smooth 8-key walk cycle (Walk1-8 poses) at <paramref name="framesPerKey"/> frames per key.</summary>
        private static PoseClip WalkClip(string name, int framesPerKey, string poses = "Walk")
        {
            var existing = AssetDatabase.LoadAssetAtPath<PoseClip>($"{ClipFolder}/{name}.asset");
            if (existing != null) return existing;
            PoseClip clip = StarterPoses.WriteCycleClip(name, poses, framesPerKey);
            StarterPoses.MeasureStrides();
            return clip;
        }
    }
}
