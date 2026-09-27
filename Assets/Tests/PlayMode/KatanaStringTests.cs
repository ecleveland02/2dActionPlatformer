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
    /// <summary>
    /// The combo tree built only from cancel lists: L1 > L2 > L3 > L4, and Heavy branching off each light
    /// into a different move. Mashing Light walks the string; the dummy's combo counter proves it chains on hit.
    /// </summary>
    public class KatanaStringTests
    {
        private TestWorld world;
        private FakeInput input;
        private MovementData data;
        private PlayerController player;
        private TrainingDummy dummy;
        private readonly List<Object> assets = new List<Object>();
        private AttackData l1, l2, l3, l4, heavy, h1, h2, h3;

        [SetUp]
        public void SetUp()
        {
            world = new TestWorld();
            input = new FakeInput();
            data = Track(ScriptableObject.CreateInstance<MovementData>());

            l1 = Attack("L1", AttackButton.Light, 4, 3, 10, 9, 17);
            l2 = Attack("L2", AttackButton.Light, 4, 3, 11, 9, 18);
            l3 = Attack("L3", AttackButton.Light, 6, 3, 12, 11, 21);
            l4 = Attack("L4", AttackButton.Light, 7, 5, 18, 18, 30);
            heavy = Attack("Heavy", AttackButton.Heavy, 10, 4, 18, 19, 32);
            h1 = Attack("Iaido", AttackButton.Heavy, 8, 4, 18, 17, 30);
            h2 = Attack("RisingMoon", AttackButton.Heavy, 8, 4, 20, 17, 32);
            h3 = Attack("FallingBlossom", AttackButton.Heavy, 10, 4, 20, 20, 34);
            l1.cancelsInto = new List<AttackData> { l2, h1 };
            l2.cancelsInto = new List<AttackData> { l3, h2 };
            l3.cancelsInto = new List<AttackData> { l4, h3 };

            var weapon = Track(ScriptableObject.CreateInstance<WeaponData>());
            weapon.light1 = l1;
            weapon.heavy = heavy;

            world.Box(-50, -1, 50, 0);
            KinematicBody2D body = world.Body(new Vector2(0, 0.05f));
            player = body.gameObject.AddComponent<PlayerController>();
            player.Configure(data, input, new AbilityUnlocks());
            body.gameObject.AddComponent<PlayerCombat>().Configure(weapon, null);
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

        private AttackData Attack(string name, AttackButton button, int s, int a, int r, int cs, int ce)
        {
            AttackData d = Track(ScriptableObject.CreateInstance<AttackData>());
            d.name = name;
            d.button = button;
            d.startupFrames = s; d.activeFrames = a; d.recoveryFrames = r;
            d.cancelWindowStart = cs; d.cancelWindowEnd = ce;
            d.damage = 5; d.hitstopFrames = 3; d.hitstunFrames = 30;
            d.knockback = Vector2.zero;
            // Wide box so the dummy stays in reach through the whole string.
            d.hitboxes.Add(new HitboxWindow
            {
                boxes = new List<HitboxShape> { new HitboxShape { offset = new Vector2(1.2f, 0f), size = new Vector2(2.4f, 1.6f) } },
            });
            return d;
        }

        private void SpawnDummy(float x)
        {
            GameObject go = world.Body(new Vector2(x, 0.05f)).gameObject;
            go.layer = 0;
            go.AddComponent<Hurtbox>().Configure(Faction.Enemy, Vector2.zero, new Vector2(0.6f, 1.8f));
            dummy = go.AddComponent<TrainingDummy>();
            dummy.Configure(data, null, null);
        }

        private void Step(bool light = false, bool heavyPress = false)
        {
            input.Clock.Advance();
            if (light) input.Buffer.Record(BufferedAction.LightAttack);
            if (heavyPress) input.Buffer.Record(BufferedAction.HeavyAttack);
            player.Tick();
            if (dummy != null) dummy.Tick();
        }

        private AttackData Current => (player.CurrentState as AttackState)?.Attack;

        /// <summary>Presses the button every 3 ticks (like a player mashing) and records each distinct attack started.</summary>
        private List<AttackData> Mash(int ticks, bool heavyAt = false, int heavyAfterAttacks = 0)
        {
            var seen = new List<AttackData>();
            for (int t = 0; t < ticks; t++)
            {
                bool pressHeavy = heavyAt && seen.Count == heavyAfterAttacks;
                Step(light: !pressHeavy && t % 3 == 0, heavyPress: pressHeavy && t % 3 == 0);
                AttackState attack = player.CurrentState as AttackState;
                if (attack != null && attack.Frame == 1) seen.Add(attack.Attack);
            }
            return seen;
        }

        [Test]
        public void MashingLight_OnHit_PlaysTheFourHitString()
        {
            SpawnDummy(1.2f);
            List<AttackData> seen = Mash(150);
            CollectionAssert.AreEqual(new[] { l1, l2, l3, l4 }, seen.GetRange(0, 4));
            Assert.GreaterOrEqual(dummy.BestCombo, 4, "All four hits should land as one combo.");
        }

        [Test]
        public void MashingLight_OnWhiff_StillPlaysTheString()
        {
            List<AttackData> seen = Mash(150);
            CollectionAssert.AreEqual(new[] { l1, l2, l3, l4 }, seen.GetRange(0, 4));
        }

        [TestCase(1, "Iaido")]
        [TestCase(2, "RisingMoon")]
        [TestCase(3, "FallingBlossom")]
        public void HeavyAfterNthLight_UsesThatBranch(int lights, string expected)
        {
            SpawnDummy(1.2f);
            List<AttackData> seen = Mash(150, heavyAt: true, heavyAfterAttacks: lights);
            Assert.AreEqual(expected, seen[lights].name);
        }

        [Test]
        public void HoldingAttackPastTheEnd_WaitsThenBranches()
        {
            // Tap/hold button: Light 1 finishes while Attack is still held and undecided.
            Step(light: true);
            input.AttackHoldPending = true;
            for (int i = 0; i < 30; i++) Step();
            Assert.AreEqual(l1, Current, "Light 1 waits in its last pose while the hold is undecided.");

            // The hold completes: the button becomes a heavy press, which branches from Light 1.
            input.AttackHoldPending = false;
            Step(heavyPress: true);
            Assert.AreEqual(h1, Current, "A hold that finishes after Light 1 still gives the Light 1 branch (Iaido).");
        }

        [Test]
        public void NotHolding_AttackEndsNormally_AndLateHeavyIsNeutral()
        {
            Step(light: true);
            for (int i = 0; i < 25; i++) Step();
            Assert.IsNull(Current, "Without a pending hold, the attack ends on its last frame.");
            Step(heavyPress: true);
            Assert.AreEqual(heavy, Current);
        }

        [Test]
        public void HeavyFromNeutral_IsTheOverheadCleave()
        {
            Step(heavyPress: true);
            Assert.AreEqual(heavy, Current);
        }
    }
}
