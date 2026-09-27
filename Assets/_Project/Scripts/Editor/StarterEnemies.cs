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
            if (grunt.walk == null) grunt.walk = WalkClip("GruntWalk", 7);
            if (grunt.alert == null) grunt.alert = StarterCombat.HoldClip("GruntAlert", poses["GruntAlert"], 0, false);
            if (grunt.hurt == null) grunt.hurt = StarterCombat.HoldClip("GruntHurt", poses["DummyHit"], 0, false);
            if (grunt.defeated == null) grunt.defeated = StarterCombat.HoldClip("GruntDefeated", poses["Defeated"], 3, false);
            EditorUtility.SetDirty(grunt);

            EnemyData lancer = CreateLancer(combat);
            AssetDatabase.SaveAssets();

            return new Result { Grunt = grunt, Lancer = lancer };
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
            if (lancer.walk == null) lancer.walk = WalkClip("LancerWalk", 8);
            if (lancer.alert == null) lancer.alert = StarterCombat.HoldClip("LancerAlert", poses["LancerAlert"], 3, false);
            if (lancer.hurt == null) lancer.hurt = StarterCombat.HoldClip("GruntHurt", poses["DummyHit"], 0, false);
            if (lancer.defeated == null) lancer.defeated = StarterCombat.HoldClip("GruntDefeated", poses["Defeated"], 3, false);
            EditorUtility.SetDirty(lancer);
            return lancer;
        }

        /// <summary>A walk: the run cycle poses at <paramref name="framesPerPose"/> each (the run uses 5).</summary>
        private static PoseClip WalkClip(string name, int framesPerPose)
        {
            PoseClip clip = StarterCombat.LoadOrCreate<PoseClip>($"{ClipFolder}/{name}.asset", out bool isNew);
            if (!isNew) return clip;
            clip.loop = true;
            clip.fadeInFrames = 4;
            clip.entries = new List<PoseClip.Entry>();
            for (int i = 1; i <= 6; i++)
            {
                clip.entries.Add(new PoseClip.Entry
                {
                    pose = AssetDatabase.LoadAssetAtPath<PoseData>(StarterPoses.PathFor("Run" + i)),
                    frames = framesPerPose,
                    easing = PoseEasing.Linear,
                });
            }
            EditorUtility.SetDirty(clip);
            return clip;
        }
    }
}
