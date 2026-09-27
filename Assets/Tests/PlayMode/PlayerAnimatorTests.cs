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
            set.turn = Clip("Turn", 2, loop: false);
            set.runStop = Clip("RunStop", 2, loop: false);
            set.idleFidget = Clip("Fidget", 2, loop: false);
            set.hardLand = Clip("HardLand", 2, loop: false);
            set.fidgetAfterFrames = 30;

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

        private void RunRightToFullSpeed()
        {
            for (int i = 0; i < 10; i++) Step();
            input.Move = Vector2.right;
            for (int i = 0; i < 20; i++) Step();
            Assert.IsInstanceOf<RunState>(player.CurrentState);
        }

        [Test]
        public void TurningAroundWhileRunning_PlaysTheTurn()
        {
            RunRightToFullSpeed();
            input.Move = Vector2.left;
            Step();
            Assert.AreEqual(set.turn, animator.OneShot);
            Assert.AreEqual(set.turn, Playing);
        }

        [Test]
        public void ReleasingAtSpeed_PlaysRunStop_AndRunningAgainCancelsIt()
        {
            RunRightToFullSpeed();
            input.Move = Vector2.zero;
            Step();
            Assert.AreEqual(set.runStop, animator.OneShot);

            input.Move = Vector2.right;
            Step();
            Assert.IsNull(animator.OneShot, "Pressing a direction goes straight back to the run.");
            Assert.AreEqual(set.run, Playing);
        }

        [Test]
        public void StandingStill_PlaysAFidget_AndJumpingCancelsIt()
        {
            for (int i = 0; i < 80 && animator.OneShot != set.idleFidget; i++) Step();
            Assert.AreEqual(set.idleFidget, animator.OneShot, "Fidgets after 30 idle frames.");

            input.JumpHeld = true;
            Step(pressJump: true);
            Assert.IsNull(animator.OneShot, "Any move cancels a one-shot.");
            Assert.AreEqual(set.jump, Playing);
        }

        [Test]
        public void RunCycle_PlaysFasterWhenItHasAStrideLength()
        {
            set.run.strideLength = 3f;
            RunRightToFullSpeed();
            float expected = CycleSync.Rate(player.Velocity.x, 3f, set.run.Timeline.TotalFrames, 0.35f, 3f);
            Assert.AreEqual(expected, animator.PoseAnimator.PlaybackRate, 1e-3f);
            Assert.Greater(animator.PoseAnimator.PlaybackRate, 1f);
        }

        [Test]
        public void HardLanding_OnlyAfterAHighFall()
        {
            for (int i = 0; i < 10; i++) Step();
            input.JumpHeld = true;
            Step(pressJump: true);
            for (int i = 0; i < 120 && !(player.CurrentState is LandState); i++) Step();
            Assert.Less(player.LastFallHeight, set.hardLandingHeight, "A normal jump is not a hard landing.");
            Assert.AreNotEqual(set.hardLand, animator.OneShot);

            input.JumpHeld = false;
            for (int i = 0; i < 20; i++) Step();
            player.ResetTo(new Vector2(0f, 8f));   // drop from 8 units up
            for (int i = 0; i < 200 && !(player.CurrentState is LandState); i++) Step();
            Assert.Greater(player.LastFallHeight, set.hardLandingHeight);
            Assert.AreEqual(set.hardLand, animator.OneShot);
        }
    }
}
