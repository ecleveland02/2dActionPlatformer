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
    /// <summary>Parry (spec 6.5), player health and hurt invulnerability (6.7), Redraw heal (6.6), dash i-frames.</summary>
    public class ParryAndHealthTests
    {
        private TestWorld world;
        private FakeInput input;
        private PlayerController player;
        private PlayerHealth health;
        private PlayerCombat combat;
        private TrainingDummy dummy;
        private SparringAttacker attacker;
        private AttackData jab, smash;
        private readonly List<Object> assets = new List<Object>();

        [SetUp]
        public void SetUp()
        {
            world = new TestWorld();
            input = new FakeInput();
            MovementData data = Track(ScriptableObject.CreateInstance<MovementData>());

            jab = EnemyAttack("Jab", startup: 16, active: 3, recovery: 20, damage: 8, parryable: true);
            smash = EnemyAttack("Smash", startup: 24, active: 4, recovery: 28, damage: 18, parryable: false);

            world.Box(-50, -1, 50, 0);
            KinematicBody2D body = world.Body(new Vector2(0f, 0.05f));
            player = body.gameObject.AddComponent<PlayerController>();
            player.Configure(data, input, new AbilityUnlocks());
            combat = body.gameObject.AddComponent<PlayerCombat>();
            combat.Configure(Track(ScriptableObject.CreateInstance<WeaponData>()), null);
            health = body.gameObject.AddComponent<PlayerHealth>();
            body.gameObject.AddComponent<Hurtbox>().Configure(Faction.Player, Vector2.zero, new Vector2(0.6f, 1.8f));

            GameObject d = world.Body(new Vector2(1.2f, 0.05f)).gameObject;
            d.layer = 0;
            d.AddComponent<Hurtbox>().Configure(Faction.Enemy, Vector2.zero, new Vector2(0.6f, 1.8f));
            dummy = d.AddComponent<TrainingDummy>();
            dummy.Configure(data, null, null);
            attacker = d.AddComponent<SparringAttacker>();
            attacker.Configure(new List<AttackData>(), null, pause: 9999, attackRange: 0f);   // only attacks when told
            attacker.SetTarget(player);

            for (int i = 0; i < 10; i++) Step();
        }

        [TearDown]
        public void TearDown()
        {
            world.Destroy();
            foreach (Object o in assets) if (o != null) Object.DestroyImmediate(o);
            assets.Clear();
        }

        private T Track<T>(T o) where T : Object { assets.Add(o); return o; }

        private AttackData EnemyAttack(string name, int startup, int active, int recovery, int damage, bool parryable)
        {
            AttackData a = Track(ScriptableObject.CreateInstance<AttackData>());
            a.name = name;
            a.startupFrames = startup; a.activeFrames = active; a.recoveryFrames = recovery;
            a.damage = damage; a.hitstopFrames = 6; a.hitstunFrames = 16;
            a.knockback = new Vector2(5f, 2f);
            a.parryable = parryable;
            a.hitboxes.Add(new HitboxWindow
            {
                boxes = new List<HitboxShape> { new HitboxShape { offset = new Vector2(0.75f, 0.35f), size = new Vector2(0.9f, 0.5f) } },
            });
            return a;
        }

        private void Step(bool parry = false, bool dash = false, bool special = false)
        {
            input.Clock.Advance();
            if (parry) input.Buffer.Record(BufferedAction.Parry);
            if (dash) input.Buffer.Record(BufferedAction.Dash);
            if (special) input.Buffer.Record(BufferedAction.Special);
            player.Tick();
            health.Tick();
            dummy.Tick();
            attacker.Tick();
        }

        private void Steps(int n) { for (int i = 0; i < n; i++) Step(); }

        [Test]
        public void Parry_OnParryableJab_NoDamage_StaggersAttacker_GivesInk_CancelsRecovery()
        {
            attacker.StartAttack(jab);
            Steps(14);
            Step(parry: true);                   // parry active on attacker frames 15-20; the jab is active 17-19
            Steps(2);

            Assert.AreEqual(100, health.Health.Current, "A parried hit deals no damage.");
            Assert.IsTrue(dummy.IsStunned, "The attacker staggers.");
            Assert.IsNull(attacker.Current, "The attack is cancelled.");
            Assert.AreEqual(20, combat.Ink.Value, "Parry gives 20 ink.");

            Step();
            Assert.IsNotInstanceOf<ParryState>(player.CurrentState, "A successful parry cancels the parry recovery.");
        }

        [Test]
        public void UnparryableSmash_HitsThroughParry()
        {
            attacker.StartAttack(smash);
            Steps(22);
            Step(parry: true);
            Steps(4);
            Assert.AreEqual(82, health.Health.Current);
            Assert.IsInstanceOf<HitstunState>(player.CurrentState);
        }

        [Test]
        public void GettingHit_DamagesAndKnocksBack_ThenInvulnerableFor45Frames()
        {
            attacker.StartAttack(jab);
            Steps(17);
            Assert.AreEqual(92, health.Health.Current);
            Assert.IsInstanceOf<HitstunState>(player.CurrentState);
            Assert.IsTrue(health.Health.IsInvulnerable);

            Assert.IsFalse(health.CanBeHit);

            Steps(44);
            Assert.IsFalse(health.CanBeHit, "Frame 45 of the invulnerability window.");
            Step();
            Assert.IsTrue(health.CanBeHit, "Vulnerable again after 45 frames.");
        }

        [Test]
        public void WhiffedParry_Lasts6ActivePlus20RecoveryFrames()
        {
            Step(parry: true);
            Steps(25);
            Assert.IsInstanceOf<ParryState>(player.CurrentState, "Frame 26 is the last recovery frame.");
            Step();
            Assert.IsNotInstanceOf<ParryState>(player.CurrentState);
        }

        [Test]
        public void Dash_IFrames_AvoidTheJab()
        {
            attacker.StartAttack(jab);
            Steps(15);
            Step(dash: true);                    // invulnerable on dash frames 1-8 = attacker frames 16-23
            Steps(6);
            Assert.AreEqual(100, health.Health.Current);
        }

        [Test]
        public void Redraw_Spends100Ink_Heals30Percent_AfterTheAnimation()
        {
            health.Health.TakeDamage(50, 0);
            combat.Ink.Gain(100);
            input.DownHeld = true;
            Step(special: true);
            input.DownHeld = false;
            Assert.IsInstanceOf<RedrawState>(player.CurrentState);
            Assert.AreEqual(0, combat.Ink.Value);

            Steps(43);
            Assert.AreEqual(50, health.Health.Current, "No heal until the animation finishes.");
            Step();
            Assert.AreEqual(80, health.Health.Current);
        }

        [Test]
        public void Redraw_InterruptedByAHit_HealIsLost()
        {
            health.Health.TakeDamage(50, 0);
            combat.Ink.Gain(100);
            attacker.StartAttack(jab);
            input.DownHeld = true;
            Step(special: true);
            input.DownHeld = false;
            Steps(60);
            Assert.AreEqual(42, health.Health.Current, "Hit for 8 during Redraw, and no heal.");
        }
    }
}
