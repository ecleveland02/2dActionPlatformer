using System.Collections.Generic;
using Margin.Abilities;
using Margin.Combat;
using Margin.Enemies;
using Margin.Input;
using Margin.Physics;
using Margin.Player;
using Margin.Weapons;
using NUnit.Framework;
using UnityEngine;

namespace Margin.Tests
{
    /// <summary>Milestone 4: EnemyBase (Doodle Grunt data), attack slots, and player death and respawn.</summary>
    public class EnemyTests
    {
        private TestWorld world;
        private FakeInput input;
        private MovementData movement;
        private CombatSettings settings;
        private PlayerController player;
        private PlayerHealth health;
        private EnemyData gruntData;
        private readonly List<EnemyBase> enemies = new List<EnemyBase>();
        private readonly List<Object> assets = new List<Object>();

        [SetUp]
        public void SetUp()
        {
            world = new TestWorld();
            input = new FakeInput();
            movement = Track(ScriptableObject.CreateInstance<MovementData>());
            settings = Track(ScriptableObject.CreateInstance<CombatSettings>());

            world.Box(-30, -1, 30, 0);
            KinematicBody2D body = world.Body(new Vector2(0f, 0.05f));
            player = body.gameObject.AddComponent<PlayerController>();
            player.Configure(movement, input, new AbilityUnlocks());
            body.gameObject.AddComponent<PlayerCombat>().Configure(Track(ScriptableObject.CreateInstance<WeaponData>()), settings);
            health = body.gameObject.AddComponent<PlayerHealth>();
            body.gameObject.AddComponent<Hurtbox>().Configure(Faction.Player, Vector2.zero, new Vector2(0.6f, 1.8f));

            // Doodle Grunt numbers (see StarterCombat / StarterEnemies), standing still so timing is predictable.
            AttackData shove = Attack("GruntShove", startup: 10, active: 3, recovery: 24, damage: 8, hitstop: 8, hitstun: 30,
                                      knockback: new Vector2(8f, 5f), center: new Vector2(0.65f, 0.3f), size: new Vector2(0.7f, 0.7f));
            AttackData swing = Attack("GruntSwing", startup: 20, active: 4, recovery: 22, damage: 12, hitstop: 7, hitstun: 22,
                                      knockback: new Vector2(2f, 0f), center: new Vector2(0.6f, 0.15f), size: new Vector2(0.8f, 0.7f));
            swing.cancelWindowStart = 26; swing.cancelWindowEnd = 34;
            swing.cancelsInto.Add(shove);

            gruntData = Track(ScriptableObject.CreateInstance<EnemyData>());
            gruntData.maxHealth = 40;
            gruntData.patrolDistance = 0f;
            gruntData.attackRange = 1.2f;
            gruntData.attacks.Add(swing);
            Step();
        }

        [TearDown]
        public void TearDown()
        {
            world.Destroy();
            foreach (Object o in assets) if (o != null) Object.DestroyImmediate(o);
            assets.Clear();
            enemies.Clear();
        }

        private T Track<T>(T o) where T : Object { assets.Add(o); return o; }

        private AttackData Attack(string name, int startup, int active, int recovery, int damage, int hitstop, int hitstun,
                                  Vector2 knockback, Vector2 center, Vector2 size)
        {
            AttackData a = Track(ScriptableObject.CreateInstance<AttackData>());
            a.name = name;
            a.startupFrames = startup; a.activeFrames = active; a.recoveryFrames = recovery;
            a.damage = damage; a.hitstopFrames = hitstop; a.hitstunFrames = hitstun; a.knockback = knockback;
            a.cancelWindowStart = startup + active + recovery; a.cancelWindowEnd = a.cancelWindowStart;
            a.hitboxes.Add(new HitboxWindow { boxes = new List<HitboxShape> { new HitboxShape { offset = center, size = size } } });
            return a;
        }

        private EnemyBase Grunt(float x)
        {
            GameObject go = world.Body(new Vector2(x, 0.05f)).gameObject;
            go.layer = 0;
            go.AddComponent<Hurtbox>().Configure(Faction.Enemy, Vector2.zero, new Vector2(0.6f, 1.8f));
            var enemy = go.AddComponent<EnemyBase>();
            enemy.Configure(gruntData, movement, settings);
            enemy.SetTarget(player);
            enemies.Add(enemy);
            enemy.Tick();   // starts its state machine
            return enemy;
        }

        private void Step(bool parry = false)
        {
            input.Clock.Advance();
            if (parry) input.Buffer.Record(BufferedAction.Parry);
            player.Tick();
            health.Tick();
            foreach (EnemyBase e in enemies) e.Tick();
        }

        private void StepUntil(System.Func<bool> condition, int max = 600)
        {
            for (int i = 0; i < max && !condition(); i++) Step();
            Assert.IsTrue(condition(), "Condition not reached.");
        }

        [Test]
        public void Grunt_NoticesApproachesAndCombosThePlayer()
        {
            EnemyBase grunt = Grunt(4f);
            var seen = new List<EnemyState>();
            int mostHits = 0;
            for (int i = 0; i < 600; i++)
            {
                Step();
                if (!seen.Contains(grunt.CurrentState)) seen.Add(grunt.CurrentState);
                mostHits = Mathf.Max(mostHits, health.ComboTaken.Hits);
                if (mostHits == 2 && !(player.CurrentState is HitstunState)) break;
            }

            CollectionAssert.IsSubsetOf(new EnemyState[] { grunt.Alert, grunt.Approach, grunt.Attack }, seen);
            Assert.AreEqual(2, mostHits, "Swing and shove land as one combo.");
            Assert.AreEqual(100 - 12 - 7, health.Health.Current, "12, then 8 at 85% (6.8 -> 7).");
        }

        [Test]
        public void Grunt_MissedSwing_DoesNotChain()
        {
            EnemyBase grunt = Grunt(3f);
            player.IsInvulnerable = true;   // every swing whiffs
            int longestString = 0;
            for (int i = 0; i < 400; i++)
            {
                Step();
                longestString = Mathf.Max(longestString, grunt.Runner.IsAttacking ? grunt.Runner.StringLength : 0);
            }
            Assert.AreEqual(1, longestString, "It swung, but never chained into the shove.");
            Assert.AreEqual(100, health.Health.Current);
        }

        [Test]
        public void Parry_StaggersTheGrunt()
        {
            EnemyBase grunt = Grunt(3f);
            StepUntil(() => grunt.Runner.IsAttacking && grunt.Runner.Frame == 17);
            Step(parry: true);   // parry frames 1-6 = swing frames 18-23; the swing is active 21-24
            Steps(6);

            Assert.AreEqual(100, health.Health.Current);
            Assert.AreEqual(grunt.Hitstun, grunt.CurrentState, "Staggered after being parried.");
            Assert.IsFalse(grunt.Runner.IsAttacking);
        }

        private void Steps(int n) { for (int i = 0; i < n; i++) Step(); }

        [Test]
        public void Grunt_DiesAtZeroHealth_ThenVanishes()
        {
            EnemyBase grunt = Grunt(6f);
            AttackData big = Attack("Big", 4, 3, 10, damage: 50, hitstop: 0, hitstun: 20, knockback: Vector2.zero,
                                    center: Vector2.zero, size: Vector2.one);
            Assert.IsTrue(grunt.ReceiveHit(new HitInfo(player, big, Vector2.zero, grunt.Position, false)));

            Assert.IsTrue(grunt.IsDead);
            Assert.IsFalse(grunt.CanBeHit);
            Hurtbox hurtbox = grunt.GetComponent<Hurtbox>();
            Assert.Contains(hurtbox, new List<Hurtbox>(Hurtbox.Active));
            Steps(gruntData.deathFrames);
            Assert.IsFalse(new List<Hurtbox>(Hurtbox.Active).Contains(hurtbox), "Gone after the defeat pose.");
        }

        private int MostAttackingAtOnce(int ticks)
        {
            int most = 0;
            for (int i = 0; i < ticks; i++)
            {
                Step();
                int attacking = 0;
                foreach (EnemyBase e in enemies) if (e.CurrentState == e.Attack) attacking++;
                most = Mathf.Max(most, attacking);
            }
            return most;
        }

        [Test]
        public void AttackSlots_NoLimit_EveryoneAttacks()
        {
            settings.maxEnemyAttackers = 0;
            Grunt(1.1f);
            Grunt(-1.1f);
            player.IsInvulnerable = true;
            Assert.AreEqual(2, MostAttackingAtOnce(200));
        }

        [Test]
        public void AttackSlots_Capped_OthersWait()
        {
            settings.maxEnemyAttackers = 1;
            Grunt(1.1f);
            Grunt(-1.1f);
            player.IsInvulnerable = true;
            Assert.AreEqual(1, MostAttackingAtOnce(200));
        }

        [Test]
        public void PlayerDeath_RespawnsAtSpawn_WithFullHealth_AndResetsEnemies()
        {
            Vector2 spawn = health.SpawnPoint;
            EnemyBase grunt = Grunt(4f);
            grunt.Health.TakeDamage(10, 0);
            health.Health.TakeDamage(95, 0);

            StepUntil(() => player.CurrentState is DefeatedState);
            Assert.IsFalse(health.CanBeHit, "Can't be hit while defeated.");
            Assert.AreNotEqual(grunt.Home.x, grunt.Position.x, "The grunt walked over to attack.");

            StepUntil(() => !(player.CurrentState is DefeatedState), settings.playerDeathFrames + 30);   // + the killing hit's hitstop
            Assert.AreEqual(100, health.Health.Current);
            Assert.AreEqual(spawn.x, player.Body.Position.x, 0.001f);
            Assert.AreEqual(grunt.Home.x, grunt.Position.x, 0.001f, "Enemies reset on respawn.");
            Assert.AreEqual(grunt.Patrol, grunt.CurrentState);
            Assert.AreEqual(gruntData.maxHealth, grunt.Health.Current, "Enemy health refilled.");
        }
    }
}
