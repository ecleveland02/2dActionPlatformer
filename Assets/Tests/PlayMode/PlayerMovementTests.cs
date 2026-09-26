using Margin.Abilities;
using Margin.Core;
using Margin.Input;
using Margin.Physics;
using Margin.Player;
using NUnit.Framework;
using UnityEngine;

namespace Margin.Tests
{
    /// <summary>
    /// Drives the real PlayerController with scripted input, one Step() per simulated tick.
    /// Covers the spec Section 17 PlayMode tests (jump height, coyote frame 6 vs 7) plus buffering, run and dash.
    /// </summary>
    public class PlayerMovementTests
    {
        /// <summary>Scripted input. Set Move/JumpHeld/DownHeld, and pass presses to Step().</summary>
        private sealed class FakeInput : IPlayerInput
        {
            public readonly FrameCounter Clock = new FrameCounter();
            public FakeInput() { Buffer = new InputBuffer(Clock); }
            public Vector2 Move { get; set; }
            public bool JumpHeld { get; set; }
            public bool DownHeld { get; set; }
            public InputBuffer Buffer { get; }
        }

        private TestWorld world;
        private FakeInput input;
        private MovementData data;
        private PlayerController player;

        [SetUp]
        public void SetUp()
        {
            world = new TestWorld();
            input = new FakeInput();
            data = ScriptableObject.CreateInstance<MovementData>();
        }

        [TearDown]
        public void TearDown()
        {
            world.Destroy();
            Object.DestroyImmediate(data);
        }

        private void SpawnPlayer(Vector2 feet)
        {
            KinematicBody2D body = world.Body(feet);
            player = body.gameObject.AddComponent<PlayerController>();
            // Walls off so a late jump near a ledge side can't turn into a wall jump and confuse the test.
            player.Configure(data, input, new AbilityUnlocks { wallCling = false });
        }

        private void Settle()
        {
            for (int i = 0; i < 10; i++) Step();
        }

        /// <summary>One game tick: clock advances, this tick's presses enter the buffer, player ticks.</summary>
        private void Step(bool pressJump = false, bool pressDash = false)
        {
            input.Clock.Advance();
            if (pressJump) input.Buffer.Record(BufferedAction.Jump);
            if (pressDash) input.Buffer.Record(BufferedAction.Dash);
            player.Tick();
        }

        private float Feet => TestWorld.Feet(player.Body);

        /// <summary>Flat floor with the player standing still on it.</summary>
        private void StandOnFloor()
        {
            world.Box(-50, -1, 50, 0);
            SpawnPlayer(new Vector2(0, 0.05f));
            Settle();
            Assert.IsInstanceOf<IdleState>(player.CurrentState, "Setup: player should be idle on the floor.");
        }

        [Test]
        public void FullJump_ReachesConfiguredHeight()
        {
            StandOnFloor();
            input.JumpHeld = true;
            Step(pressJump: true);

            float peak = Feet;
            for (int i = 0; i < 60; i++)
            {
                Step();
                peak = Mathf.Max(peak, Feet);
            }

            // Spec Section 17: within 0.05 units.
            Assert.AreEqual(data.jumpHeight, peak, 0.05f);
        }

        [Test]
        public void TappedJump_ReachesMinimumHeight()
        {
            StandOnFloor();
            input.JumpHeld = true;
            Step(pressJump: true);
            input.JumpHeld = false;

            float peak = Feet;
            for (int i = 0; i < 60; i++)
            {
                Step();
                peak = Mathf.Max(peak, Feet);
            }

            Assert.AreEqual(data.minJumpHeight, peak, 0.05f);
        }

        [Test]
        public void Run_ReachesFullSpeedInFourTicks()
        {
            StandOnFloor();
            input.Move = Vector2.right;

            for (int i = 0; i < 3; i++) Step();
            Assert.Less(player.Velocity.x, data.runSpeed);
            Step();
            Assert.AreEqual(data.runSpeed, player.Velocity.x, 0.001f);
        }

        /// <summary>
        /// Runs right off a ledge at x = 0, then presses Jump on the given airborne frame.
        /// Frame 1 is the first tick that starts with the player already off the ledge.
        /// </summary>
        private bool CoyoteJumpWorksOnFrame(int frame)
        {
            world.Box(-50, -1, 0, 0);          // ledge ends at x = 0
            world.Box(-50, -30, 50, -29);      // far floor below so nothing falls forever
            SpawnPlayer(new Vector2(-3, 0.05f));
            Settle();
            Assert.IsTrue(player.Grounded, "Setup: should start on the ledge.");
            input.Move = Vector2.right;

            int guard = 0;
            do { Step(); } while (player.Grounded && ++guard < 120);
            Assert.Less(guard, 120, "Setup: player never left the ledge.");

            for (int f = 1; f < frame; f++) Step();
            Step(pressJump: true);
            return player.CurrentState is JumpState && player.Velocity.y > 0f;
        }

        [Test]
        public void CoyoteJump_SucceedsOnFrame6() => Assert.IsTrue(CoyoteJumpWorksOnFrame(6));

        [Test]
        public void CoyoteJump_FailsOnFrame7() => Assert.IsFalse(CoyoteJumpWorksOnFrame(7));

        /// <summary>Drops the player from a height and returns the tick number (from 1) on which it lands.</summary>
        private int LandingTick(int pressJumpOnTick)
        {
            player.ResetTo(new Vector2(0, 4f + TestWorld.HalfHeight));
            for (int tick = 1; tick <= 120; tick++)
            {
                Step(pressJump: tick == pressJumpOnTick);
                if (player.Grounded) return tick;
            }
            Assert.Fail("Setup: player never landed.");
            return -1;
        }

        [Test]
        public void JumpPressedJustBeforeLanding_FiresOnLanding()
        {
            world.Box(-50, -1, 50, 0);
            SpawnPlayer(new Vector2(0, 4f));
            int landing = LandingTick(pressJumpOnTick: -1);

            // Press 3 ticks before touching down; the jump should fire on the very next tick.
            LandingTick(pressJumpOnTick: landing - 3);
            input.JumpHeld = true;
            Step();
            Assert.IsInstanceOf<JumpState>(player.CurrentState);
            Assert.Greater(player.Velocity.y, 0f);
        }

        [Test]
        public void JumpPressedLongBeforeLanding_IsForgotten()
        {
            world.Box(-50, -1, 50, 0);
            SpawnPlayer(new Vector2(0, 4f));
            int landing = LandingTick(pressJumpOnTick: -1);

            LandingTick(pressJumpOnTick: landing - 15);
            Step();
            Assert.IsNotInstanceOf<JumpState>(player.CurrentState);
        }

        [Test]
        public void Dash_MovesAtDashSpeed_ThenReturnsToRunSpeed()
        {
            StandOnFloor();
            input.Move = Vector2.right;
            Step(pressDash: true);
            Assert.IsInstanceOf<DashState>(player.CurrentState);
            Assert.AreEqual(data.dashSpeed, player.Velocity.x, 0.001f);
            Assert.IsTrue(player.IsInvulnerable);

            for (int i = 1; i < data.dashFrames; i++) Step();
            Assert.IsInstanceOf<DashState>(player.CurrentState, "Dash should last dashFrames ticks.");

            Step();
            Assert.IsNotInstanceOf<DashState>(player.CurrentState);
            Assert.AreEqual(data.runSpeed, player.Velocity.x, 0.001f);
            Assert.IsFalse(player.IsInvulnerable);
        }

        [Test]
        public void DownPlusJump_OnOneWay_DropsThrough()
        {
            world.Box(-50, -1, 50, 0);
            world.Box(-3, 2.8f, 3, 3f, oneWay: true);
            SpawnPlayer(new Vector2(0, 3.05f));
            Settle();
            Assert.IsTrue(player.Body.Collisions.OnOneWay, "Setup: should be standing on the one-way platform.");

            input.DownHeld = true;
            Step(pressJump: true);
            input.DownHeld = false;
            for (int i = 0; i < 60; i++) Step();

            Assert.IsTrue(player.Grounded);
            Assert.AreEqual(0f, Feet, 0.002f);
        }
    }
}
