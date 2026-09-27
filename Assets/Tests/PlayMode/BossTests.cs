using System.Collections.Generic;
using Margin.Abilities;
using Margin.Bosses;
using Margin.Combat;
using Margin.Physics;
using Margin.Player;
using NUnit.Framework;
using UnityEngine;

namespace Margin.Tests
{
    /// <summary>
    /// The boss engine and the Highlighter (spec 10): intro, rests between moves, phase change at 50% (can't be
    /// hurt during it), parry stagger, defeat, and resetting on a player respawn.
    /// </summary>
    public class BossTests
    {
        private TestWorld world;
        private FakeInput input;
        private PlayerController player;
        private PlayerHealth health;
        private HighlighterBoss boss;
        private HighlighterData data;
        private AttackData swipe;
        private readonly List<Object> made = new List<Object>();

        [SetUp]
        public void SetUp()
        {
            BossBase.ForgetSeen();
            world = new TestWorld();
            input = new FakeInput();
            MovementData movement = Track(ScriptableObject.CreateInstance<MovementData>());
            world.Box(-20, -1, 20, 0);

            swipe = Track(ScriptableObject.CreateInstance<AttackData>());
            swipe.startupFrames = 22; swipe.activeFrames = 6; swipe.recoveryFrames = 30;
            swipe.damage = 14; swipe.parryable = true;
            swipe.hitboxes.Add(new HitboxWindow { boxes = new List<HitboxShape> { new HitboxShape { offset = new Vector2(1.8f, 0.3f), size = new Vector2(3f, 3.4f) } } });

            BossPhase one = Track(ScriptableObject.CreateInstance<BossPhase>());
            one.healthThreshold = 1f;
            one.restFramesMin = one.restFramesMax = 20;
            one.moves.Add(new BossMoveEntry { move = HighlighterBoss.Swipe, attack = swipe, weight = 1 });
            BossPhase two = Track(ScriptableObject.CreateInstance<BossPhase>());
            two.healthThreshold = 0.5f;
            two.transitionFrames = 40;
            two.transitionFramesRepeat = 20;
            two.moves.Add(new BossMoveEntry { move = HighlighterBoss.CapToss, attack = swipe, weight = 1 });

            data = Track(ScriptableObject.CreateInstance<HighlighterData>());
            data.maxHealth = 100;
            data.introFrames = 30;
            data.introFramesRepeat = 10;
            data.parryStaggerFrames = 50;
            data.defeatFrames = 20;
            data.phases = new List<BossPhase> { one, two };

            KinematicBody2D playerBody = world.Body(new Vector2(-6f, 0.05f));
            player = playerBody.gameObject.AddComponent<PlayerController>();
            player.Configure(movement, input, new AbilityUnlocks());
            health = playerBody.gameObject.AddComponent<PlayerHealth>();

            KinematicBody2D bossBody = world.Body(new Vector2(6f, 0.05f));
            bossBody.gameObject.layer = 0;
            bossBody.GetComponent<BoxCollider2D>().size = new Vector2(1.2f, 3f);
            bossBody.Teleport(new Vector2(6f, 1.55f));
            bossBody.gameObject.AddComponent<Hurtbox>().Configure(Faction.Enemy, Vector2.zero, new Vector2(1.3f, 3f));
            boss = bossBody.gameObject.AddComponent<HighlighterBoss>();
            boss.Configure(data, null, movement);
            boss.SetTarget(player);
            boss.ResetBoss();
        }

        [TearDown]
        public void TearDown()
        {
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

        private void EngageAndFinishIntro()
        {
            boss.Engage();
            Step(data.introFrames + 1);
        }

        [Test]
        public void Dormant_UntilEngaged_ThenIntro_ThenRests()
        {
            Step(10);
            Assert.AreEqual(BossMode.Dormant, boss.Mode);
            Assert.IsFalse(boss.ShowBossBar);
            boss.Engage();
            Assert.AreEqual(BossMode.Intro, boss.Mode);
            Assert.IsTrue(boss.ShowBossBar, "spec 10: the health bar shows for the whole fight");
            Assert.IsFalse(boss.CanBeHit, "can't be hurt during the intro");
            Step(data.introFrames);
            Assert.AreEqual(BossMode.Rest, boss.Mode);
            Assert.IsTrue(boss.CanBeHit);
        }

        [Test]
        public void SecondIntro_IsShort()
        {
            EngageAndFinishIntro();
            health.Respawn();   // retry: the boss goes back to sleep
            Assert.AreEqual(BossMode.Dormant, boss.Mode);
            boss.Engage();
            Assert.AreEqual(data.introFramesRepeat, boss.ModeLength);
        }

        [Test]
        public void AttacksAfterResting()
        {
            EngageAndFinishIntro();
            Step(25);
            Assert.AreEqual(BossMode.Attacking, boss.Mode, "rests 20 frames, then picks a move");
            Assert.AreEqual(HighlighterBoss.Swipe, boss.CurrentMove.move);
        }

        [Test]
        public void HalfHealth_StartsPhaseTwo_AndCantBeHurtDuringTheChange()
        {
            EngageAndFinishIntro();
            boss.TakeDamage(50);
            Assert.AreEqual(1, boss.PhaseIndex);
            Assert.AreEqual(BossMode.PhaseShift, boss.Mode);
            Assert.IsFalse(boss.CanBeHit);
            boss.TakeDamage(30);
            Assert.AreEqual(50, boss.Health.Current, "damage during the phase change is ignored");
            Step(boss.ModeLength + 1);
            Assert.AreEqual(BossMode.Rest, boss.Mode);
        }

        [Test]
        public void Parry_StaggersTheBoss()
        {
            EngageAndFinishIntro();
            boss.StartMove(new BossMoveEntry { move = HighlighterBoss.Swipe, attack = swipe, weight = 1 });
            Step(5);
            boss.OnParried(default, 0);
            Assert.AreEqual(BossMode.Staggered, boss.Mode);
            Assert.AreEqual(data.parryStaggerFrames, boss.ModeLength);
            Step(data.parryStaggerFrames + 1);
            Assert.AreEqual(BossMode.Rest, boss.Mode);
        }

        [Test]
        public void Defeat_FiresBeaten_AndStaysBeaten()
        {
            int beaten = 0;
            boss.Beaten += b => beaten++;
            EngageAndFinishIntro();
            boss.TakeDamage(50);   // 50%: phase change
            Step(boss.ModeLength + 1);
            boss.TakeDamage(1000);
            Assert.AreEqual(BossMode.Defeated, boss.Mode);
            Step(data.defeatFrames + 1);
            Assert.AreEqual(BossMode.Gone, boss.Mode);
            Assert.AreEqual(1, beaten);

            health.Respawn();
            boss.ResetForRoom();
            Assert.AreEqual(BossMode.Gone, boss.Mode, "a beaten boss stays beaten");
        }

        [Test]
        public void PlayerRespawn_ResetsTheBoss()
        {
            EngageAndFinishIntro();
            boss.TakeDamage(30);
            health.Respawn();
            Assert.AreEqual(BossMode.Dormant, boss.Mode);
            Assert.AreEqual(data.maxHealth, boss.Health.Current);
            Assert.AreEqual(0, boss.PhaseIndex);
        }
    }
}
