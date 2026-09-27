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

        /// <summary>Pencil Lancer numbers (see StarterCombat / StarterEnemies).</summary>
        private EnemyData LancerData(bool chargeOnly = false)
        {
            AttackData sweep = Attack("LancerSweep", startup: 9, active: 4, recovery: 24, damage: 8, hitstop: 8, hitstun: 32,
                                      knockback: new Vector2(3f, 10f), center: new Vector2(1.3f, -0.35f), size: new Vector2(1.2f, 0.5f));
            AttackData thrust = Attack("LancerThrust", startup: 28, active: 4, recovery: 26, damage: 14, hitstop: 7, hitstun: 24,
                                       knockback: new Vector2(2f, 0f), center: new Vector2(1.45f, 0.25f), size: new Vector2(1.3f, 0.3f));
            thrust.lungeSpeed = 5f; thrust.lungeFirstFrame = 27;
            thrust.cancelWindowStart = 34; thrust.cancelWindowEnd = 42;
            thrust.cancelsInto.Add(sweep);
            AttackData charge = Attack("LancerCharge", startup: 36, active: 5, recovery: 30, damage: 18, hitstop: 9, hitstun: 28,
                                       knockback: new Vector2(7f, 3f), center: new Vector2(1.5f, 0.17f), size: new Vector2(1.4f, 0.35f));
            charge.lungeSpeed = 8f; charge.lungeFirstFrame = 33; charge.parryable = false;

            EnemyData d = Track(ScriptableObject.CreateInstance<EnemyData>());
            d.maxHealth = 50;
            d.walkSpeed = 1.8f;
            d.patrolDistance = 0f;
            d.attackRange = 2.0f;
            d.minAttackRange = 0.6f;
            d.retreatDistance = 1.1f;
            d.attacks.Add(chargeOnly ? charge : thrust);
            return d;
        }

        private EnemyBase Grunt(float x) => Enemy(x, gruntData);

        /// <summary>A Scribble Bat (see StarterCombat / StarterEnemies) centered at (x, y).</summary>
        private FlyingEnemy Bat(float x, float y)
        {
            AttackData dive = Attack("BatDive", startup: 22, active: 18, recovery: 24, damage: 10, hitstop: 6, hitstun: 20,
                                     knockback: new Vector2(4f, 3f), center: Vector2.zero, size: new Vector2(0.8f, 0.6f));
            EnemyData d = Track(ScriptableObject.CreateInstance<EnemyData>());
            d.maxHealth = 20;
            d.patrolDistance = 1.5f;
            d.noticeRange = 7f;
            d.noticeHeight = 4f;
            d.alertFrames = 20;
            d.attacks.Add(dive);
            d.attackCooldownFrames = 70;

            KinematicBody2D body = world.Body(new Vector2(x, y - TestWorld.HalfHeight));
            body.GetComponent<BoxCollider2D>().size = new Vector2(0.6f, 0.5f);
            GameObject go = body.gameObject;
            go.layer = 0;
            go.AddComponent<Hurtbox>().Configure(Faction.Enemy, Vector2.zero, new Vector2(0.7f, 0.55f));
            var bat = go.AddComponent<FlyingEnemy>();
            bat.Configure(d, movement, settings);
            bat.SetTarget(player);
            enemies.Add(bat);
            bat.Tick();
            return bat;
        }

        private EnemyBase Enemy(float x, EnemyData data)
        {
            GameObject go = world.Body(new Vector2(x, 0.05f)).gameObject;
            go.layer = 0;
            go.AddComponent<Hurtbox>().Configure(Faction.Enemy, Vector2.zero, new Vector2(0.6f, 1.8f));
            var enemy = go.AddComponent<EnemyBase>();
            enemy.Configure(data, movement, settings);
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
        public void Lancer_ThrustChainsIntoSweep_OnHit()
        {
            EnemyBase lancer = Enemy(5f, LancerData());
            int mostHits = 0;
            for (int i = 0; i < 600; i++)
            {
                Step();
                mostHits = Mathf.Max(mostHits, health.ComboTaken.Hits);
                if (mostHits == 2 && !(player.CurrentState is HitstunState)) break;
            }
            Assert.AreEqual(2, mostHits, "Thrust and sweep land as one combo.");
            Assert.AreEqual(100 - 14 - 7, health.Health.Current, "14, then 8 at 85% (6.8 -> 7).");
        }

        [Test]
        public void Lancer_TooClose_BacksOffBeforeAttacking()
        {
            EnemyBase lancer = Enemy(0.3f, LancerData());
            StepUntil(() => lancer.CurrentState == lancer.Attack);
            Assert.GreaterOrEqual(Mathf.Abs(lancer.Position.x - player.Body.Position.x), 0.6f,
                                  "It stepped back to spear range (0.6+) before thrusting.");
        }

        [Test]
        public void Lancer_ChargedThrust_CannotBeParried()
        {
            EnemyBase lancer = Enemy(3f, LancerData(chargeOnly: true));
            StepUntil(() => lancer.Runner.IsAttacking && lancer.Runner.Frame == 33);
            Step(parry: true);   // parry frames 1-6 = charge frames 34-39; it's active 37-41
            Steps(6);
            Assert.AreEqual(100 - 18, health.Health.Current, "Hit straight through the parry.");
            Assert.IsInstanceOf<HitstunState>(player.CurrentState);
        }

        [Test]
        public void Bat_HoversInsteadOfFalling()
        {
            FlyingEnemy bat = Bat(20f, 4f);   // too far away to notice the player
            float homeY = bat.Home.y;
            Steps(120);
            Assert.AreEqual(bat.Patrol, bat.CurrentState);
            Assert.AreEqual(homeY, bat.Position.y, 0.3f, "Still hovering at its height.");
        }

        [Test]
        public void Bat_TakesPositionAboveThePlayer_ThenDives()
        {
            FlyingEnemy bat = Bat(4f, 3.5f);
            bool dived = false;
            for (int i = 0; i < 600 && health.Health.Current == 100; i++)
            {
                Step();
                if (bat.CurrentState == bat.Attack) dived = true;
            }
            Assert.IsTrue(dived);
            Assert.AreEqual(90, health.Health.Current, "The dive hit for 10.");
        }

        [Test]
        public void Bat_FallsWhileStunned_ThenFliesBackUp()
        {
            FlyingEnemy bat = Bat(15f, 4f);   // beyond give-up range, so it goes back to patrolling
            float homeY = bat.Home.y;
            AttackData hit = Attack("Hit", 4, 3, 10, damage: 1, hitstop: 0, hitstun: 30, knockback: Vector2.zero,
                                    center: Vector2.zero, size: Vector2.one);
            bat.ReceiveHit(new HitInfo(player, hit, Vector2.zero, bat.Position, false));

            Steps(30);
            Assert.Less(bat.Position.y, homeY - 0.3f, "Falls while stunned (juggle gravity).");
            Steps(300);
            Assert.AreEqual(homeY, bat.Position.y, 0.5f, "Flew back up to its patrol height.");
        }

        [Test]
        public void Bat_Defeated_FallsToTheGround()
        {
            FlyingEnemy bat = Bat(12f, 4f);
            AttackData big = Attack("Big", 4, 3, 10, damage: 50, hitstop: 0, hitstun: 20, knockback: Vector2.zero,
                                    center: Vector2.zero, size: Vector2.one);
            bat.ReceiveHit(new HitInfo(player, big, Vector2.zero, bat.Position, false));
            Assert.IsTrue(bat.IsDead);
            StepUntil(() => bat.Grounded, 120);
        }

        private void HitWith(EnemyBase enemy, Vector2 knockback, int hitstop, int hitstun = 30)
        {
            AttackData a = Attack("Hit", 4, 3, 10, damage: 1, hitstop: hitstop, hitstun: hitstun, knockback: knockback,
                                  center: Vector2.zero, size: Vector2.one);
            enemy.ReceiveHit(new HitInfo(player, a, knockback, enemy.Position, hitstop >= settings.globalHitstopThreshold));
        }

        [Test]
        public void Launched_LandsInKnockdown_CantBeHit_ThenGetsUp()
        {
            EnemyBase grunt = Grunt(6f);
            HitWith(grunt, new Vector2(0f, 12f), hitstop: 0);   // a launcher
            StepUntil(() => grunt.CurrentState == grunt.Knockdown, 200);
            Assert.IsTrue(grunt.Grounded);
            Assert.IsFalse(grunt.CanBeHit, "Can't be hit while knocked down.");

            StepUntil(() => grunt.CurrentState != grunt.Knockdown, settings.knockdownFrames + settings.getUpFrames + 5);
            Assert.AreEqual(grunt.Approach, grunt.CurrentState, "Back up and fighting.");
            Assert.IsTrue(grunt.CanBeHit);
        }

        [Test]
        public void GroundHit_WithoutLaunch_IsNotAKnockdown()
        {
            EnemyBase grunt = Grunt(6f);
            HitWith(grunt, new Vector2(3f, 0f), hitstop: 10, hitstun: 20);   // heavy, but on the ground
            for (int i = 0; i < 60; i++)
            {
                Step();
                Assert.AreNotEqual(grunt.Knockdown, grunt.CurrentState);
            }
        }

        [Test]
        public void Bat_KnockedDown_ThenFliesBackUp()
        {
            FlyingEnemy bat = Bat(15f, 4f);
            HitWith(bat, new Vector2(0f, -12f), hitstop: 0);    // spiked down
            StepUntil(() => bat.CurrentState == bat.Knockdown, 200);
            StepUntil(() => bat.CurrentState != bat.Knockdown, settings.knockdownFrames + settings.getUpFrames + 5);
            Steps(240);
            Assert.AreEqual(bat.Home.y, bat.Position.y, 0.5f, "Flew back up.");
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
