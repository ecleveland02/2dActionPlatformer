using System.Collections.Generic;
using Margin.Abilities;
using Margin.Input;
using Margin.Physics;
using Margin.Player;
using Margin.Rendering;
using NUnit.Framework;
using UnityEngine;

namespace Margin.Tests
{
    /// <summary>The right clip plays for each player state, and run/sprint swaps keep the stride phase.</summary>
    public class PlayerAnimatorTests
    {
        private TestWorld world;
        private FakeInput input;
        private MovementData data;
        private PlayerController player;
        private PlayerAnimator animator;
        private PlayerAnimationSet set;
        private readonly List<Object> assets = new List<Object>();

        [SetUp]
        public void SetUp()
        {
            world = new TestWorld();
            input = new FakeInput();
            data = Track(ScriptableObject.CreateInstance<MovementData>());
            set = Track(ScriptableObject.CreateInstance<PlayerAnimationSet>());
            set.idle = Clip("Idle", 1, loop: true);
            set.run = Clip("Run", 6, loop: true);
            set.sprint = Clip("Sprint", 6, loop: true);
            set.jump = Clip("Jump", 1, loop: false);
            set.fall = Clip("Fall", 1, loop: false);
            set.land = Clip("Land", 1, loop: false);
            set.skid = Clip("Skid", 1, loop: false);
            set.dash = Clip("Dash", 1, loop: false);

            world.Box(-50, -1, 50, 0);
            KinematicBody2D body = world.Body(new Vector2(0, 0.05f));
            player = body.gameObject.AddComponent<PlayerController>();
            player.Configure(data, input, new AbilityUnlocks());

            var visual = new GameObject("Visual");
            visual.transform.SetParent(player.transform, false);
            var rig = visual.AddComponent<StickFigureRig>();
            var poseAnimator = visual.AddComponent<PoseAnimator>();
            poseAnimator.Rig = rig;
            animator = player.gameObject.AddComponent<PlayerAnimator>();
            animator.Configure(set, poseAnimator);
        }

        [TearDown]
        public void TearDown()
        {
            world.Destroy();
            foreach (Object o in assets) if (o != null) Object.DestroyImmediate(o);
            assets.Clear();
        }

        private T Track<T>(T o) where T : Object { assets.Add(o); return o; }

        private PoseClip Clip(string name, int poses, bool loop)
        {
            PoseClip clip = Track(ScriptableObject.CreateInstance<PoseClip>());
            clip.name = name;
            clip.loop = loop;
            for (int i = 0; i < poses; i++)
            {
                PoseData pose = Track(ScriptableObject.CreateInstance<PoseData>());
                pose.pose = new FigurePose { shoulderFront = i * 10f };
                clip.entries.Add(new PoseClip.Entry { pose = pose, frames = 5 });
            }
            return clip;
        }

        private void Step(bool pressJump = false)
        {
            input.Clock.Advance();
            if (pressJump) input.Buffer.Record(BufferedAction.Jump);
            player.Tick();
            animator.Tick();
        }

        private PoseClip Playing => animator.PoseAnimator.CurrentClip;

        [Test]
        public void EachStatePlaysItsClip()
        {
            for (int i = 0; i < 10; i++) Step();
            Assert.AreEqual(set.idle, Playing, "idle");

            input.Move = Vector2.right;
            for (int i = 0; i < 5; i++) Step();
            Assert.AreEqual(set.run, Playing, "run");

            input.JumpHeld = true;
            Step(pressJump: true);
            Assert.AreEqual(set.jump, Playing, "jump");

            int guard = 0;
            while (!(player.CurrentState is FallState) && ++guard < 60) Step();
            Step();
            Assert.AreEqual(set.fall, Playing, "fall");
        }

        [Test]
        public void RunToSprint_KeepsStridePhase()
        {
            for (int i = 0; i < 10; i++) Step();
            input.Move = Vector2.right;

            // Run until one tick before sprint starts (sprint begins on the tick the charge reaches the threshold).
            int guard = 0;
            while (player.SprintCharge < data.framesToStartSprint - 1 && ++guard < 200) Step();
            Assert.AreEqual(set.run, Playing);
            float runPhase = (animator.PoseAnimator.ClipTick % 30) / 30f;

            Step();
            Assert.AreEqual(set.sprint, Playing);
            float sprintPhase = ((animator.PoseAnimator.ClipTick - 1) % 30) / 30f;
            Assert.AreEqual(runPhase, sprintPhase, 1f / 30f + 0.001f, "Sprint should continue the stride, not restart it.");
        }
    }
}
