using System.Collections.Generic;
using Margin.Abilities;
using Margin.Bosses;
using Margin.Combat;
using Margin.Core;
using Margin.Physics;
using Margin.Player;
using NUnit.Framework;
using UnityEngine;

namespace Margin.Tests
{
    /// <summary>
    /// The Stapler Titan (spec 10, Boss 2): Hop Slam lands where the player stood and sends two shockwaves, a
    /// shockwave hurts a grounded player, and phase 2 staples the kept platforms in place and tears out the others
    /// (put back on a retry).
    /// </summary>
    public class StaplerTitanTests
    {
        private TestWorld world;
        private FakeInput input;
        private PlayerController player;
        private PlayerHealth health;
        private StaplerTitanBoss boss;
        private StaplerData data;
        private AttackData hop, wave;
        private readonly List<Object> made = new List<Object>();

        [SetUp]
        public void SetUp()
        {
            BossBase.ForgetSeen();
            world = new TestWorld();
            input = new FakeInput();
            MovementData movement = Track(ScriptableObject.CreateInstance<MovementData>());
            world.Box(-20, -1, 20, 0);

            hop = Track(ScriptableObject.CreateInstance<AttackData>());
            hop.startupFrames = 28; hop.activeFrames = 40; hop.recoveryFrames = 52;
            hop.damage = 18; hop.parryable = false;
            wave = Track(ScriptableObject.CreateInstance<AttackData>());
            wave.startupFrames = 1; wave.activeFrames = 1;
            wave.damage = 12; wave.hitstunFrames = 18; wave.parryable = false;

            BossPhase one = Track(ScriptableObject.CreateInstance<BossPhase>());
            one.healthThreshold = 1f;
            one.restFramesMin = one.restFramesMax = 500;   // only the moves a test starts
            one.moves.Add(new BossMoveEntry { move = StaplerTitanBoss.HopSlam, attack = hop, weight = 1 });
            BossPhase two = Track(ScriptableObject.CreateInstance<BossPhase>());
            two.healthThreshold = 0.5f;
            two.restFramesMin = two.restFramesMax = 500;
            two.transitionFrames = 60;
            two.transitionFramesRepeat = 30;

            data = Track(ScriptableObject.CreateInstance<StaplerData>());
            data.maxHealth = 100;
            data.introFrames = 30;
            data.introFramesRepeat = 10;
            data.bodySize = new Vector2(3.2f, 1.7f);
            data.phases = new List<BossPhase> { one, two };

            KinematicBody2D playerBody = world.Body(new Vector2(-2f, 0.05f));
            player = playerBody.gameObject.AddComponent<PlayerController>();
            player.Configure(movement, input, new AbilityUnlocks());
            health = playerBody.gameObject.AddComponent<PlayerHealth>();

            KinematicBody2D bossBody = world.Body(new Vector2(6f, 0.05f));
            bossBody.gameObject.layer = 0;
            bossBody.GetComponent<BoxCollider2D>().size = new Vector2(3.2f, 1.7f);
            bossBody.Teleport(new Vector2(6f, 0.9f));
            bossBody.gameObject.AddComponent<Hurtbox>().Configure(Faction.Enemy, Vector2.zero, data.bodySize);
            boss = bossBody.gameObject.AddComponent<StaplerTitanBoss>();
            boss.Configure(data, null, movement);
            boss.ConfigureLook(null, wave);
            boss.SetTarget(player);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (ShockwaveProjectile w in SceneQuery.FindAll<ShockwaveProjectile>()) Object.DestroyImmediate(w.gameObject);
            world.Destroy();
            foreach (Object o in made) if (o != null) Object.DestroyImmediate(o);
            made.Clear();
        }

        private T Track<T>(T o) where T : Object { made.Add(o); return o; }

        private void Step(int n = 1)
        {
            for (int i = 0; i < n; i++)
            {
                input.Clock.Advance();
                player.Tick();
                health.Tick();
                boss.Tick();
            }
        }

        private void Fight()
        {
            boss.ResetBoss();
            boss.Engage();
            Step(data.introFrames + 1);
            Assert.AreEqual(BossMode.Rest, boss.Mode);
        }

        [Test]
        public void HopSlam_LandsWhereThePlayerStood_AndSendsTwoShockwaves()
        {
            Fight();
            boss.StartMove(data.phases[0].moves[0]);
            Step(hop.startupFrames + 1);
            Assert.IsTrue(boss.Hopping, "leaps when the telegraph ends");
            int air = 0;
            while (boss.Hopping && air < 120)
            {
                Step();
                air++;
            }
            Assert.IsFalse(boss.Hopping, "comes back down");
            Assert.AreEqual(data.hopAirFrames, air, 3, "in the air for about hopAirFrames");
            Assert.AreEqual(-2f, boss.Position.x, 0.6f, "lands where the player stood at the jump");
            Assert.AreEqual(2, SceneQuery.FindAll<ShockwaveProjectile>().Count, "one wave each way");
            Assert.AreEqual(BossMode.Attacking, boss.Mode, "jammed after landing: the punish window");
        }

        [Test]
        public void Shockwave_HurtsAGroundedPlayer()
        {
            int before = health.Health.Current;
            LayerMask ground = 1 << world.GroundLayer;
            ShockwaveProjectile w = ShockwaveProjectile.Spawn(boss, wave, null, 2f, 0f, -1, 11f, 0.8f, 120, ground, ground);
            for (int i = 0; i < 40 && health.Health.Current == before; i++)
            {
                Step();
                w.Tick();
            }
            Assert.AreEqual(before - wave.damage, health.Health.Current);
        }

        [Test]
        public void PhaseTwo_PinsTheKeptPlatforms_AndTearsOutTheOthers_UntilARetry()
        {
            var kept = world.Box(-8f, 3f, -5f, 3.3f, oneWay: true).AddComponent<StaplePlatform>();
            kept.KeepInPhase2 = true;
            GameObject torn = world.Box(5f, 6f, 8f, 6.3f, oneWay: true);
            var tornPlatform = torn.AddComponent<StaplePlatform>();

            Fight();
            boss.TakeDamage(50);
            Assert.AreEqual(BossMode.PhaseShift, boss.Mode);
            Step(data.phases[1].transitionFrames + 1);
            Assert.IsTrue(kept.Pinned, "stapled in place");
            Assert.IsFalse(kept.Removed);
            Assert.IsTrue(tornPlatform.Removed, "torn out");
            Assert.IsFalse(torn.GetComponent<BoxCollider2D>().enabled, "and no longer solid");

            health.Respawn();   // retry: everything goes back
            Assert.IsFalse(kept.Pinned);
            Assert.IsFalse(tornPlatform.Removed);
            Assert.IsTrue(torn.GetComponent<BoxCollider2D>().enabled);
        }

    }
}
