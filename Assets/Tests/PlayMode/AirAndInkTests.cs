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
    /// <summary>Air attacks and air strings, the ink meter, and the ink-costing projectile special.</summary>
    public class AirAndInkTests
    {
        private TestWorld world;
        private FakeInput input;
        private MovementData data;
        private PlayerController player;
        private PlayerCombat combat;
        private TrainingDummy dummy;
        private readonly List<Object> assets = new List<Object>();
        private AttackData light1, heavy, air1, air2, air3, slam, wave;

        [SetUp]
        public void SetUp()
        {
            world = new TestWorld();
            input = new FakeInput();
            data = Track(ScriptableObject.CreateInstance<MovementData>());

            light1 = Attack("Light1", AttackButton.Light, false, 4, 3, 10, 9, 17);
            light1.inkGain = 5;
            heavy = Attack("Heavy", AttackButton.Heavy, false, 10, 4, 18, 19, 32);
            air1 = Attack("Air1", AttackButton.Light, true, 4, 3, 10, 8, 17);
            air2 = Attack("Air2", AttackButton.Light, true, 4, 3, 10, 8, 17);
            air3 = Attack("Air3", AttackButton.Light, true, 5, 3, 12, 9, 20);
            slam = Attack("Slam", AttackButton.Heavy, true, 6, 4, 16, 13, 26);
            foreach (AttackData a in new[] { air1, air2, air3, slam }) a.airGravityScale = 0.3f;
            air1.cancelsInto = new List<AttackData> { air2, slam };
            air2.cancelsInto = new List<AttackData> { air3, slam };
            air3.cancelsInto = new List<AttackData> { slam };

            wave = Attack("InkWave", AttackButton.Special, false, 8, 3, 16, 20, 27);
            wave.hitboxes.Clear();
            wave.inkCost = 50;
            wave.damage = 20;
            wave.projectileSpeed = 14f;
            wave.projectileLifetimeFrames = 40;

            world.Box(-50, -1, 50, 0);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (InkWaveProjectile p in Margin.Core.SceneQuery.FindAll<InkWaveProjectile>()) Object.DestroyImmediate(p.gameObject);
            world.Destroy();
            foreach (Object o in assets) if (o != null) Object.DestroyImmediate(o);
            assets.Clear();
        }

        private T Track<T>(T o) where T : Object { assets.Add(o); return o; }

        private AttackData Attack(string name, AttackButton button, bool airborne, int s, int a, int r, int cs, int ce)
        {
            AttackData d = Track(ScriptableObject.CreateInstance<AttackData>());
            d.name = name;
            d.button = button;
            d.airborne = airborne;
            d.startupFrames = s; d.activeFrames = a; d.recoveryFrames = r;
            d.cancelWindowStart = cs; d.cancelWindowEnd = ce;
            d.damage = 5; d.hitstopFrames = 3; d.hitstunFrames = 30;
            d.knockback = Vector2.zero;
            d.hitboxes.Add(new HitboxWindow
            {
                boxes = new List<HitboxShape> { new HitboxShape { offset = new Vector2(1.05f, 0.15f), size = new Vector2(1.4f, 0.6f) } },
            });
            return d;
        }

        private void SpawnPlayer(float feetY)
        {
            KinematicBody2D body = world.Body(new Vector2(0, feetY));
            player = body.gameObject.AddComponent<PlayerController>();
            player.Configure(data, input, new AbilityUnlocks());
            combat = body.gameObject.AddComponent<PlayerCombat>();
            combat.Configure(BuildWeapon(), null);
        }

        private WeaponData BuildWeapon()
        {
            var weapon = Track(ScriptableObject.CreateInstance<WeaponData>());
            weapon.light1 = light1; weapon.heavy = heavy;
            weapon.airLight = air1; weapon.airHeavy = slam; weapon.special = wave;
            return weapon;
        }

        private void SpawnDummy(float x)
        {
            GameObject go = world.Body(new Vector2(x, 0.05f)).gameObject;
            go.layer = 0;
            go.AddComponent<Hurtbox>().Configure(Faction.Enemy, Vector2.zero, new Vector2(0.6f, 1.8f));
            dummy = go.AddComponent<TrainingDummy>();
            dummy.Configure(data, null, null);
        }

        private void Step(bool light = false, bool heavyPress = false, bool special = false)
        {
            input.Clock.Advance();
            if (light) input.Buffer.Record(BufferedAction.LightAttack);
            if (heavyPress) input.Buffer.Record(BufferedAction.HeavyAttack);
            if (special) input.Buffer.Record(BufferedAction.Special);
            player.Tick();
            if (dummy != null) dummy.Tick();
            foreach (InkWaveProjectile p in Margin.Core.SceneQuery.FindAll<InkWaveProjectile>()) p.Tick();
        }

        private AttackData Current => (player.CurrentState as AttackState)?.Attack;

        [Test]
        public void InTheAir_LightIsAirLight_HeavyIsSlam()
        {
            SpawnPlayer(10f);
            Step();
            Step(light: true);
            Assert.AreEqual(air1, Current);

            player.ResetTo(new Vector2(0f, 10.9f));
            Step();
            Step(heavyPress: true);
            Assert.AreEqual(slam, Current);
        }

        [Test]
        public void AirString_ChainsLightLightLight_ThenSlam()
        {
            SpawnPlayer(40f);   // plenty of air: this test is about the chain, not the juggle
            Step();
            var seen = new List<AttackData>();
            for (int t = 0; t < 120 && seen.Count < 4; t++)
            {
                bool heavyTime = seen.Count == 3;
                Step(light: !heavyTime && t % 3 == 0, heavyPress: heavyTime && t % 3 == 0);
                if (player.CurrentState is AttackState a && a.Frame == 1) seen.Add(a.Attack);
            }
            CollectionAssert.AreEqual(new[] { air1, air2, air3, slam }, seen);
        }

        [Test]
        public void AirAttacks_FallSlowerThanNormal()
        {
            SpawnPlayer(20f);
            Step();
            Step(light: true);
            for (int i = 0; i < 5; i++) Step();
            float attackingSpeed = player.Velocity.y;

            player.ResetTo(new Vector2(0f, 20.9f));
            for (int i = 0; i < 7; i++) Step();
            Assert.Greater(attackingSpeed, player.Velocity.y, "Reduced gravity during the air attack means a slower fall.");
        }

        [Test]
        public void Hitting_FillsInk()
        {
            SpawnPlayer(0.05f);
            SpawnDummy(1.2f);
            for (int i = 0; i < 10; i++) Step();
            Step(light: true);
            while (player.CurrentState is AttackState) Step();
            Assert.AreEqual(5, combat.Ink.Value);
        }

        [Test]
        public void Special_NeedsFiftyInk_SpendsIt_AndItsWaveHitsFarAway()
        {
            SpawnPlayer(0.05f);
            SpawnDummy(4f);   // out of sword reach
            for (int i = 0; i < 10; i++) Step();

            Step(special: true);
            Assert.IsNull(Current, "No special without 50 ink.");

            combat.Ink.Gain(60);
            for (int i = 0; i < 8; i++) Step();   // let the old press expire
            Step(special: true);
            Assert.AreEqual(wave, Current);
            Assert.AreEqual(10, combat.Ink.Value, "Special spends 50 ink.");

            for (int i = 0; i < 40 && dummy.TotalDamage == 0; i++) Step();
            Assert.AreEqual(20, dummy.TotalDamage, "The Ink Wave flies out and hits the dummy once.");
        }
    }
}
