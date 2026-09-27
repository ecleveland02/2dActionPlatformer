using System.Collections.Generic;
using Margin.Combat;
using Margin.Enemies;
using Margin.Rendering;
using UnityEditor;
using UnityEngine;

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
