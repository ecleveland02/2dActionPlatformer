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
    /// <summary>Hit detection, hit-once, hitstop and cancel rules (spec 6.1-6.4) against a training dummy.</summary>
    public class CombatTests
    {
        private TestWorld world;
        private FakeInput input;
        private MovementData data;
        private PlayerController player;
        private TrainingDummy dummy;
        private AttackData light1, heavy;
        private readonly List<Object> assets = new List<Object>();

        [SetUp]
        public void SetUp()
        {
            world = new TestWorld();
            input = new FakeInput();
            data = Track(ScriptableObject.CreateInstance<MovementData>());

            heavy = Attack(AttackButton.Heavy, startup: 10, active: 4, recovery: 18, hitstop: 8, hitstun: 30, cancelStart: 19, cancelEnd: 32);
            light1 = Attack(AttackButton.Light, startup: 4, active: 3, recovery: 10, hitstop: 4, hitstun: 18, cancelStart: 9, cancelEnd: 17);
            light1.cancelsInto.Add(heavy);

            var weapon = Track(ScriptableObject.CreateInstance<WeaponData>());
            weapon.light1 = light1;
            weapon.heavy = heavy;

            world.Box(-50, -1, 50, 0);
            KinematicBody2D body = world.Body(new Vector2(0, 0.05f));
            player = body.gameObject.AddComponent<PlayerController>();
            player.Configure(data, input, new AbilityUnlocks());
            body.gameObject.AddComponent<PlayerCombat>().Configure(weapon, null);

            for (int i = 0; i < 10; i++) Step();   // land and settle
        }

        [TearDown]
        public void TearDown()
        {
            world.Destroy();
            foreach (Object o in assets) if (o != null) Object.DestroyImmediate(o);
            assets.Clear();
        }

        private T Track<T>(T o) where T : Object { assets.Add(o); return o; }

        private AttackData Attack(AttackButton button, int startup, int active, int recovery, int hitstop, int hitstun,
                                  int cancelStart, int cancelEnd)
        {
            AttackData a = Track(ScriptableObject.CreateInstance<AttackData>());
            a.name = button.ToString();
            a.button = button;
            a.startupFrames = startup; a.activeFrames = active; a.recoveryFrames = recovery;
            a.damage = button == AttackButton.Light ? 8 : 18;
            a.hitstopFrames = hitstop; a.hitstunFrames = hitstun;
            a.knockback = Vector2.zero;   // keep the dummy in place so every test is about timing only
            a.cancelWindowStart = cancelStart; a.cancelWindowEnd = cancelEnd;
            a.hitboxes.Add(new HitboxWindow
            {
                boxes = new List<HitboxShape> { new HitboxShape { offset = new Vector2(1.05f, 0.15f), size = new Vector2(1.4f, 0.6f) } },
            });
            return a;
        }

        private void SpawnDummy(float x)
        {
            GameObject go = world.Body(new Vector2(x, 0.05f)).gameObject;
            go.layer = 0;
            go.AddComponent<Hurtbox>().Configure(Faction.Enemy, Vector2.zero, new Vector2(0.6f, 1.8f));
            dummy = go.AddComponent<TrainingDummy>();
            dummy.Configure(data, null, null);
        }

        private void Step(bool light = false, bool heavyPress = false, bool dash = false)
        {
            input.Clock.Advance();
            if (light) input.Buffer.Record(BufferedAction.LightAttack);
            if (heavyPress) input.Buffer.Record(BufferedAction.HeavyAttack);
            if (dash) input.Buffer.Record(BufferedAction.Dash);
            player.Tick();
            if (dummy != null) dummy.Tick();
        }

        private AttackState Attacking => player.CurrentState as AttackState;

        [Test]
        public void Light_HitsOnFirstActiveFrame_AndOnlyOncePerSwing()
        {
            SpawnDummy(1.2f);
            Step(light: true);
            Assert.IsNotNull(Attacking, "Light should start an attack.");
            Assert.AreEqual(light1, Attacking.Attack);

            int hitFrame = -1;
            for (int i = 0; i < 40 && Attacking != null; i++)
            {
                if (hitFrame < 0 && dummy.Combo > 0) hitFrame = Attacking.Frame;
                Step();
            }

            Assert.AreEqual(5, hitFrame, "Startup 4 means the first active frame is 5.");
            Assert.AreEqual(1, dummy.Combo, "One swing hits a target once.");
            Assert.AreEqual(8, dummy.TotalDamage);
        }

        [Test]
        public void Hitstop_FreezesAttackerForHitstopFrames()
        {
            SpawnDummy(1.2f);
            Step(light: true);
            while (dummy.Combo == 0) Step();
            Assert.AreEqual(5, Attacking.Frame);

            for (int i = 0; i < 4; i++)
            {
                Step();
                Assert.AreEqual(5, Attacking.Frame, $"Frozen during hitstop tick {i + 1}.");
            }
            Step();
            Assert.AreEqual(6, Attacking.Frame, "Resumes after 4 hitstop frames.");
        }

        [Test]
        public void OnHit_CancelsIntoHeavy_InsideWindow()
        {
            SpawnDummy(1.2f);
            Step(light: true);
            while (Attacking.Frame < 8) Step();
            Assert.IsTrue(Attacking.HasHit);

            Step(heavyPress: true);   // frame 9 is the first cancel frame
            Assert.AreEqual(heavy, Attacking.Attack, "Heavy should cancel Light on frame 9.");
            Assert.AreEqual(1, Attacking.Frame);
        }

        [Test]
        public void OnWhiff_ChainsIntoAttackOnlyAfterDelay_DashFromWindowStart()
        {
            Step(light: true);
            while (Attacking.Frame < 9) Step(heavyPress: Attacking.Frame == 8);
            Assert.AreEqual(light1, Attacking.Attack, "A missed Light can't chain at the normal window start.");

            Step(dash: true);
            Assert.IsInstanceOf<DashState>(player.CurrentState, "Dash is allowed on whiff from the window start.");
        }

        [Test]
        public void OnWhiff_ChainsIntoFollowUp_FourFramesLate()
        {
            Step(light: true);
            while (Attacking.Frame < 12) Step();
            Step(heavyPress: true);          // frame 13 = window start 9 + whiff delay 4
            Assert.AreEqual(heavy, Attacking.Attack, "A miss still chains, 4 frames later than a hit.");
        }

        [Test]
        public void OnWhiff_ChainBeforeDelay_IsHeldUntilAllowed()
        {
            Step(light: true);
            while (Attacking.Frame < 10) Step();
            Step(heavyPress: true);          // frame 11: too early on a miss, but it stays buffered
            Assert.AreEqual(light1, Attacking.Attack);
            Step();
            Assert.AreEqual(light1, Attacking.Attack);
            Step();                          // frame 13: fires from the buffer
            Assert.AreEqual(heavy, Attacking.Attack);
        }

        [Test]
        public void Whiff_DashBeforeWindowStart_IsIgnored()
        {
            Step(light: true);
            while (Attacking.Frame < 6) Step();
            Step(dash: true);    // frame 7, window starts at 9
            Assert.IsNotNull(Attacking);
            // The press is still buffered for 6 frames, so it fires as soon as the window opens (frame 9).
            Step();
            Assert.IsNotNull(Attacking);
            Step();
            Assert.IsInstanceOf<DashState>(player.CurrentState);
        }

        [Test]
        public void Combo_ResetsAfterHitstunEnds()
        {
            SpawnDummy(1.2f);
            Step(light: true);
            while (player.CurrentState is AttackState) Step();
            Assert.AreEqual(1, dummy.Combo);

            while (dummy.HitstunRemaining > 0) Step();
            Step(light: true);
            while (dummy.TotalDamage < 16) Step();
            Assert.AreEqual(1, dummy.Combo, "Hitstun ran out, so this is a new combo.");
            Assert.AreEqual(1, dummy.BestCombo);
        }
    }
}
