using System.Collections.Generic;
using Margin.Abilities;
using Margin.Input;
using Margin.Physics;
using Margin.Player;
using NUnit.Framework;
using UnityEngine;

namespace Margin.Tests
{
    /// <summary>
    /// The Grapple Line (spec 8): locked until unlocked, hooks a ring in reach, keeps the player on the rope while
    /// swinging, lets go with Jump (with a boost and the air dash back).
    /// </summary>
    public class GrappleTests
    {
        private TestWorld world;
        private FakeInput input;
        private PlayerController player;
        private MovementData data;
        private AbilityUnlocks abilities;
        private GameObject ring;
        private readonly List<Object> made = new List<Object>();

        [SetUp]
        public void SetUp()
        {
            world = new TestWorld();
            input = new FakeInput();
            data = ScriptableObject.CreateInstance<MovementData>();
            made.Add(data);
            world.Box(-30, -1, 0, 0);   // floor ends at x = 0; a pit beyond

            ring = new GameObject("Ring");
            ring.transform.position = new Vector3(4f, 6f, 0f);
            ring.AddComponent<GrappleAnchor>();
            made.Add(ring);

            KinematicBody2D body = world.Body(new Vector2(-1f, 0.05f));
            player = body.gameObject.AddComponent<PlayerController>();
            abilities = new AbilityUnlocks();
            player.Configure(data, input, abilities);
            player.Facing = 1;
            Step(5);
        }

        [TearDown]
        public void TearDown()
        {
            world.Destroy();
            foreach (Object o in made) if (o != null) Object.DestroyImmediate(o);
            made.Clear();
        }

        private void Step(int n = 1, bool grapple = false, bool jump = false)
        {
            for (int i = 0; i < n; i++)
            {
                input.Clock.Advance();
                if (grapple && i == 0) input.Buffer.Record(BufferedAction.Grapple);
                if (jump && i == 0) input.Buffer.Record(BufferedAction.Jump);
                player.Tick();
            }
        }

        [Test]
        public void Locked_UntilTheAbilityIsUnlocked()
        {
            Step(1, grapple: true);
            Assert.IsNotInstanceOf<GrappleState>(player.CurrentState);
        }

        [Test]
        public void HooksARingInReach_AndStaysOnTheRope()
        {
            abilities.grappleLine = true;
            Step(1, grapple: true);
            Assert.IsInstanceOf<GrappleState>(player.CurrentState, "the ring is 5 right, 5 up: in reach and ahead");
            float rope = player.Grapple.RopeLength;
            Assert.Less(rope, 9.5f);

            // Half a swing (the pendulum's period is about 100 frames at this length).
            for (int i = 0; i < 45; i++)
            {
                Step();
                if (!(player.CurrentState is GrappleState)) break;
                float d = Vector2.Distance(player.Body.Position, new Vector2(4f, 6f));
                Assert.LessOrEqual(d, player.Grapple.RopeLength + 0.02f, "never farther than the rope");
            }
            Assert.IsInstanceOf<GrappleState>(player.CurrentState, "still swinging over the pit");
            Assert.Greater(player.Body.Position.y, -1f, "the rope holds the player up over the pit");
        }

        [Test]
        public void JumpLetsGo_WithABoost_AndTheAirDashBack()
        {
            abilities.grappleLine = true;
            Step(1, grapple: true);
            Step(20);
            player.AirDashesLeft = 0;
            float vyBefore = Mathf.Max(player.Velocity.y, 0f);
            Step(1, jump: true);
            Assert.IsInstanceOf<FallState>(player.CurrentState);
            Assert.GreaterOrEqual(player.Velocity.y, vyBefore + data.grappleReleaseBoost - 3f, "boost (minus a tick of gravity)");
            Assert.AreEqual(data.airDashes, player.AirDashesLeft);
            Assert.Greater(player.GrappleCooldown, 0);
        }

        [Test]
        public void RingBehindTheWall_CantBeHooked()
        {
            abilities.grappleLine = true;
            world.Box(1f, 0f, 1.5f, 10f);   // a wall between the player and the ring
            Step(1, grapple: true);
            Assert.IsNotInstanceOf<GrappleState>(player.CurrentState);
        }
    }
}
