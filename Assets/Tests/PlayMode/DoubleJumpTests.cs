using Margin.Abilities;
using Margin.Input;
using Margin.Physics;
using Margin.Player;
using NUnit.Framework;
using UnityEngine;

namespace Margin.Tests
{
    /// <summary>The Spring Doodle (spec 8): one extra jump in the air, only once unlocked, back after landing.</summary>
    public class DoubleJumpTests
    {
        private TestWorld world;
        private FakeInput input;
        private PlayerController player;
        private AbilityUnlocks abilities;
        private MovementData data;

        [SetUp]
        public void SetUp()
        {
            world = new TestWorld();
            input = new FakeInput();
            data = ScriptableObject.CreateInstance<MovementData>();
            world.Box(-20, -1, 20, 0);
            KinematicBody2D body = world.Body(new Vector2(0f, 0.05f));
            player = body.gameObject.AddComponent<PlayerController>();
            abilities = new AbilityUnlocks();
            player.Configure(data, input, abilities);
            Step(5);
        }

        [TearDown]
        public void TearDown()
        {
            world.Destroy();
            Object.DestroyImmediate(data);
        }

        private void Step(int n = 1, bool jump = false)
        {
            for (int i = 0; i < n; i++)
            {
                input.Clock.Advance();
                if (jump && i == 0) input.Buffer.Record(BufferedAction.Jump);
                player.Tick();
            }
        }

        /// <summary>Jumps from the ground and rises until near the top.</summary>
        private void JumpAndRise()
        {
            input.JumpHeld = true;
            Step(1, jump: true);
            Step(14);
        }

        [Test]
        public void Locked_NoJumpInTheAir()
        {
            JumpAndRise();
            Step(1, jump: true);
            Assert.IsNotInstanceOf<DoubleJumpState>(player.CurrentState);
        }

        [Test]
        public void Unlocked_JumpsAgainInTheAir_OnlyOnce()
        {
            abilities.doubleJump = true;
            JumpAndRise();
            float y = player.Body.Position.y;
            Step(1, jump: true);
            Assert.IsInstanceOf<DoubleJumpState>(player.CurrentState);
            Assert.AreEqual(data.DoubleJumpVelocity, player.Velocity.y, 1.5f, "launched up (minus a tick of gravity)");
            Step(10);
            Assert.Greater(player.Body.Position.y, y + 1f, "higher than where it started");

            Step(1, jump: true);
            Assert.AreEqual(0, player.AirJumpsLeft);
            Assert.IsNotInstanceOf<DoubleJumpState>(player.CurrentState, "no third jump");
        }

        [Test]
        public void Landing_GivesTheAirJumpBack()
        {
            abilities.doubleJump = true;
            JumpAndRise();
            Step(1, jump: true);
            input.JumpHeld = false;
            for (int i = 0; i < 200 && !player.Grounded; i++) Step();
            Step(2);
            Assert.AreEqual(data.airJumps, player.AirJumpsLeft);
        }
    }
}
