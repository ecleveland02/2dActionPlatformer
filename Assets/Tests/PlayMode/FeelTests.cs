using System.Collections;
using System.Collections.Generic;
using Margin.Abilities;
using Margin.Combat;
using Margin.FX;
using Margin.Input;
using Margin.Physics;
using Margin.Player;
using Margin.Rendering;
using Margin.Weapons;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Margin.Tests
{
    /// <summary>Game-feel effects: ink splatter, screen shake, smears, blade trail, dash afterimages.</summary>
    public class FeelTests
    {
        private readonly List<Object> created = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (Object o in created) if (o != null) Object.DestroyImmediate(o);
            created.Clear();
        }

        private T Track<T>(T o) where T : Object { created.Add(o); return o; }

        [Test]
        public void InkSplatter_BurstsOnHit_MoreInkForBiggerHits()
        {
            var splatter = Track(new GameObject("Splatter")).AddComponent<InkSplatter>();
            AttackData attack = Track(ScriptableObject.CreateInstance<AttackData>());
            attack.damage = 10;

            CombatEvents.RaiseHit(new HitInfo(splatter, attack, new Vector2(5f, 1f), Vector2.zero, false), null);

            FeelSettings d = FeelSettings.Defaults;
            Assert.AreEqual(FeelMath.SplatterCount(d.splatterBase, d.splatterPerDamage, 10), splatter.LiveParticles);
        }

        [UnityTest]
        public IEnumerator CameraShake_OffsetsThenSettlesBack()
        {
            var cam = Track(new GameObject("Cam"));
            cam.transform.position = new Vector3(3f, 2f, -10f);
            var shake = cam.AddComponent<CameraShake>();

            CameraShake.Shake(0.2f);
            yield return null;
            Assert.Greater(shake.CurrentOffset.magnitude, 0f, "Camera should be offset right after a hit.");
            Assert.LessOrEqual(shake.CurrentOffset.magnitude, 0.2f + 0.0001f);

            yield return new WaitForSecondsRealtime(0.5f);
            yield return null;
            Assert.AreEqual(Vector3.zero, shake.CurrentOffset);
            Assert.AreEqual(new Vector3(3f, 2f, -10f), cam.transform.position, "Camera returns to its base position.");
        }

        // ---------------- player effects ----------------

        private TestWorld world;
        private FakeInput input;
        private PlayerController player;
        private PlayerAnimator animator;
        private PlayerFX fx;
        private AttackData slash;

        private void SetUpPlayer()
        {
            world = new TestWorld();
            input = new FakeInput();
            MovementData data = Track(ScriptableObject.CreateInstance<MovementData>());

            // A slash whose wind-up (arm raised back) and strike (arm forward) are far apart, so it must smear.
            PoseData windup = Pose(shoulder: 200f), strike = Pose(shoulder: 95f);
            PoseClip clip = Track(ScriptableObject.CreateInstance<PoseClip>());
            clip.loop = false;
            clip.fadeInFrames = 0;
            clip.entries.Add(new PoseClip.Entry { pose = windup, frames = 2, easing = PoseEasing.Snap });
            clip.entries.Add(new PoseClip.Entry { pose = windup, frames = 2, easing = PoseEasing.EaseIn });
            clip.entries.Add(new PoseClip.Entry { pose = strike, frames = 13, easing = PoseEasing.Snap });

            slash = Track(ScriptableObject.CreateInstance<AttackData>());
            slash.startupFrames = 4; slash.activeFrames = 3; slash.recoveryFrames = 10;
            slash.poseClip = clip;
            slash.smear = true;
            var weapon = Track(ScriptableObject.CreateInstance<WeaponData>());
            weapon.light1 = slash;

            var set = Track(ScriptableObject.CreateInstance<PlayerAnimationSet>());
            set.idle = Track(ScriptableObject.CreateInstance<PoseClip>());
            set.idle.entries.Add(new PoseClip.Entry { pose = Pose(0f), frames = 1 });

            world.Box(-50, -1, 50, 0);
            KinematicBody2D body = world.Body(new Vector2(0, 0.05f));
            player = body.gameObject.AddComponent<PlayerController>();
            player.Configure(data, input, new AbilityUnlocks());
            body.gameObject.AddComponent<PlayerCombat>().Configure(weapon, null);

            var visual = new GameObject("Visual");
            visual.transform.SetParent(player.transform, false);
            var rig = visual.AddComponent<StickFigureRig>();
            var poseAnimator = visual.AddComponent<PoseAnimator>();
            poseAnimator.Rig = rig;
            var bladeObject = new GameObject("Blade");
            bladeObject.transform.SetParent(visual.transform, false);
            bladeObject.AddComponent<WeaponLine>().Rig = rig;

            animator = player.gameObject.AddComponent<PlayerAnimator>();
            animator.Configure(set, poseAnimator);
            fx = player.gameObject.AddComponent<PlayerFX>();

            for (int i = 0; i < 10; i++) Step();
        }

        private PoseData Pose(float shoulder)
        {
            PoseData p = Track(ScriptableObject.CreateInstance<PoseData>());
            p.pose = new FigurePose { shoulderFront = shoulder };
            return p;
        }

        private void Step(bool light = false, bool dash = false)
        {
            input.Clock.Advance();
            if (light) input.Buffer.Record(BufferedAction.LightAttack);
            if (dash) input.Buffer.Record(BufferedAction.Dash);
            player.Tick();
            animator.Tick();
            fx.Tick();
        }

        private void TearDownPlayer()
        {
            Object.DestroyImmediate(fx);   // destroys its smear and afterimage objects
            world.Destroy();
        }

        [Test]
        public void Slash_SmearsOnlyDuringTheSwing_AndTrailsWhileAttacking()
        {
            SetUpPlayer();
            try
            {
                Step(light: true);
                var smearFrames = new List<int>();
                bool trailDuringAttack = true;
                while (player.CurrentState is AttackState attack)
                {
                    if (fx.Smear.Visible) smearFrames.Add(attack.Frame);
                    trailDuringAttack &= fx.Blade.TrailEmitting;
                    Step();
                }

                Assert.IsNotEmpty(smearFrames, "The fast swing into the strike should draw a smear.");
                Assert.GreaterOrEqual(smearFrames[0], slash.startupFrames - 1, "No smear before the swing starts.");
                Assert.IsTrue(trailDuringAttack, "Trail draws for the whole attack.");

                Step();
                Assert.IsFalse(fx.Blade.TrailEmitting, "Trail stops after the attack.");
            }
            finally { TearDownPlayer(); }
        }

        [Test]
        public void Dash_LeavesAfterimagesThatFade()
        {
            SetUpPlayer();
            try
            {
                input.Move = Vector2.right;
                Step(dash: true);
                Assert.IsInstanceOf<DashState>(player.CurrentState);
                Assert.AreEqual(1, fx.Afterimages.ActiveCount, "First afterimage on the first dash frame.");

                while (player.CurrentState is DashState) Step();
                Assert.Greater(fx.Afterimages.ActiveCount, 1, "More afterimages during the dash.");

                input.Move = Vector2.zero;
                for (int i = 0; i < 40; i++) Step();
                Assert.AreEqual(0, fx.Afterimages.ActiveCount, "All afterimages fade out.");
            }
            finally { TearDownPlayer(); }
        }
    }
}
