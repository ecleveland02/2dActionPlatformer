using System.Collections.Generic;
using Margin.Combat;
using Margin.Enemies;
using UnityEditor;
using UnityEngine;

namespace Margin.EditorTools
{
    /// <summary>
    /// World 2's enemy data in Data/Enemies (spec 9): the Tack Turret (unparryable tacks: dash through them) and the
    /// Eraser Crawler (a parryable nibble). Only creates what's missing; tuning done in the Inspector is kept.
    /// </summary>
    public static class StarterWorld2
    {
        private const string Folder = "Assets/_Project/Data/Enemies";

        internal sealed class Result
        {
            public EnemyData Turret;
            public EnemyData Crawler;
        }

        internal static Result EnsureCreated()
        {
            StickFigureRigEditor.EnsureFolder(Folder);

            // Tack: a 24-frame wind-up (pin pulls back, red flash), then a fast shot. Can't be parried: dash through.
            AttackData tack = Attack("TurretTack", 24, 1, 50, damage: 10, hitstun: 16, new Vector2(5f, 4f), parryable: false);
            if (tack.projectileSpeed <= 0f)
            {
                tack.projectileSpeed = 13f;
                tack.projectileLifetimeFrames = 90;
                EditorUtility.SetDirty(tack);
            }
            // Nibble: a quick low bite in front. Parryable.
            AttackData nibble = Attack("CrawlerNibble", 16, 3, 22, damage: 8, hitstun: 16, new Vector2(5f, 3f), parryable: true,
                                       new HitboxShape { offset = new Vector2(0.6f, -0.1f), size = new Vector2(0.7f, 0.45f) });

            var turret = StarterCombat.LoadOrCreate<EnemyData>($"{Folder}/TackTurret.asset", out bool newTurret);
            if (newTurret)
            {
                turret.maxHealth = 30;
                turret.walkSpeed = 0f;
                turret.patrolDistance = 0f;
                turret.noticeRange = 11f;
                turret.noticeHeight = 11f;
                turret.giveUpRange = 14f;
                turret.alertFrames = 20;
                turret.attacks = new List<AttackData> { tack };
                turret.attackCooldownFrames = 70;
                turret.knockbackTaken = 0f;
                turret.grapplePullable = false;
                turret.deathFrames = 30;
                EditorUtility.SetDirty(turret);
            }

            var crawler = StarterCombat.LoadOrCreate<EnemyData>($"{Folder}/EraserCrawler.asset", out bool newCrawler);
            if (newCrawler)
            {
                crawler.maxHealth = 35;
                crawler.walkSpeed = 2.4f;
                crawler.accelerationFrames = 5;
                crawler.patrolDistance = 4f;
                crawler.patrolPauseFrames = 30;
                crawler.noticeRange = 6f;
                crawler.noticeHeight = 2f;
                crawler.giveUpRange = 10f;
                crawler.alertFrames = 18;
                crawler.attackRange = 1.1f;
                crawler.attacks = new List<AttackData> { nibble };
                crawler.attackCooldownFrames = 50;
                crawler.knockbackTaken = 0.8f;
                crawler.deathFrames = 30;
                EditorUtility.SetDirty(crawler);
            }
            AssetDatabase.SaveAssets();
            return new Result { Turret = turret, Crawler = crawler };
        }

        private static AttackData Attack(string name, int startup, int active, int recovery, int damage, int hitstun,
                                         Vector2 knockback, bool parryable, HitboxShape? box = null)
        {
            var a = StarterCombat.LoadOrCreate<AttackData>($"{Folder}/{name}.asset", out bool isNew);
            if (!isNew) return a;
            a.button = AttackButton.Heavy;
            a.startupFrames = startup;
            a.activeFrames = active;
            a.recoveryFrames = recovery;
            a.damage = damage;
            a.hitstopFrames = 5;
            a.hitstunFrames = hitstun;
            a.knockback = knockback;
            a.parryable = parryable;
            a.inkGain = 0;
            a.screenShake = 0.06f;
            a.smear = false;
            a.swingSound = "swing_light";
            a.hitSound = "hit_light";
            a.cancelWindowStart = startup + active + recovery + 1;
            a.cancelWindowEnd = a.cancelWindowStart;
            a.hitboxes = new List<HitboxWindow>();
            if (box.HasValue) a.hitboxes.Add(new HitboxWindow { boxes = new List<HitboxShape> { box.Value } });
            EditorUtility.SetDirty(a);
            return a;
        }
    }
}
