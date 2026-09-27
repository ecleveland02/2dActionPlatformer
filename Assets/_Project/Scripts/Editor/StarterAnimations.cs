using System.Collections.Generic;
using Margin.Player;
using Margin.Rendering;
using UnityEditor;
using UnityEngine;
using static Margin.EditorTools.StarterPoses;

namespace Margin.EditorTools
{
    /// <summary>
    /// The full player animation set: every state animated with several keys instead of a single held pose,
    /// plus the one-shot transition clips (turn, run stop, hard landing, parry flourish, idle fidget).
    ///
    /// Every pose here was checked offline with forward kinematics before being written: planted feet within
    /// 1 cm of the ground, and the blade tip never under the floor at any frame of the blended clip. Poses are
    /// exact (x, y) values, including kneeling and lying ones (tilt = whole-body rotation).
    ///
    /// Menu: Margin > Upgrade Animations rewrites these poses and clips (after asking). Attack clips and your own
    /// clips are not touched. Projects without the new clips get them automatically from the builders.
    /// </summary>
    public static class StarterAnimations
    {
        private const string ClipFolder = "Assets/_Project/Data/Animations";
        private const string SetPath = "Assets/_Project/Data/PlayerAnimationSet.asset";

        [MenuItem("Margin/Upgrade Animations")]
        public static void UpgradeFromMenu()
        {
            if (!EditorUtility.DisplayDialog("Upgrade Animations",
                "Rewrite the starter player animations (idle, jump, fall, land, dash, skid, wall, hurt, parry, " +
                "combo breaker, Redraw, defeat) as multi-key clips, add the transition clips, and upgrade the run " +
                "cycles? Hand edits to those starter poses will be replaced. Attack animations are not touched.",
                "Upgrade", "Cancel"))
                return;
            Apply();
            EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<PlayerAnimationSet>(SetPath));
        }

        /// <summary>Applies the set once to projects that don't have it yet (checked by the Turn clip).</summary>
        internal static void EnsureApplied()
        {
            if (AssetDatabase.LoadAssetAtPath<PoseClip>($"{ClipFolder}/Turn.asset") == null) Apply();
        }

        internal static void Apply()
        {
            UpgradeCycles();
            foreach (KeyValuePair<string, FigurePose> pose in Poses()) WritePose(pose.Key, pose.Value);
            AssetDatabase.SaveAssets();
            foreach (KeyValuePair<string, ClipSpec> clip in Clips()) WriteClip(clip.Key, clip.Value);
            AssetDatabase.SaveAssets();
            FillSet();
            AssetDatabase.SaveAssets();
            Debug.Log("Player animations upgraded: multi-key clips and transitions.");
        }

        private static void FillSet()
        {
            var set = AssetDatabase.LoadAssetAtPath<PlayerAnimationSet>(SetPath);
            if (set == null) return;
            PoseClip C(string name) => AssetDatabase.LoadAssetAtPath<PoseClip>($"{ClipFolder}/{name}.asset");
            set.idle = C("Idle");
            set.idleBreathing = C("IdleBreathing");
            set.run = C("Run");
            set.sprint = C("Sprint");
            set.skid = C("Skid");
            set.jump = C("Jump");
            set.jumpApex = C("JumpApex");
            set.fall = C("Fall");
            set.fastFall = C("FastFall");
            set.land = C("Land");
            set.hardLand = C("HardLand");
            set.dash = C("Dash");
            set.wallSlide = C("WallSlide");
            set.wallJump = C("WallJump");
            set.hitstun = C("Hurt");
            set.parry = C("Parry");
            set.parrySuccess = C("ParrySuccess");
            set.comboBreaker = C("ComboBreaker");
            set.redraw = C("Redraw");
            set.defeated = C("Defeated");
            set.turn = C("Turn");
            set.runStop = C("RunStop");
            set.idleFidget = C("IdleFidget");
            EditorUtility.SetDirty(set);
        }

        // ---------------- poses (right-facing; tilt = whole-body rotation) ----------------

        private static Dictionary<string, FigurePose> Poses() => new Dictionary<string, FigurePose>
        {
                ["Idle"] = P(0f, -0.004f, spine: 3, neck: -3, sf: 18, ef: 45, sb: -5, eb: 15, hf: 8, kf: -10, hb: -8, kb: -6),
                ["IdleShiftA"] = P(0.02f, -0.008f, spine: 4, neck: -4, sf: 20, ef: 47, sb: -3, eb: 16, hf: 10, kf: -16, hb: -6, kb: -4),
                ["IdleShiftB"] = P(-0.02f, -0.002f, spine: 2, neck: -2, sf: 16, ef: 44, sb: -7, eb: 14, hf: 6, kf: -6, hb: -10, kb: -12),
                ["IdleBreath"] = P(0f, -0.02f, spine: 5, neck: -4, sf: 21, ef: 47, sb: -2, eb: 17, hf: 8, kf: -12, hb: -8, kb: -8),
                ["FidgetLook"] = P(0f, -0.002f, spine: -4, neck: 18, sf: 22, ef: 50, sb: -10, eb: 25, hf: 6, kf: -8, hb: -10, kb: -8),
                ["FidgetTwirl"] = P(0f, -0.006f, spine: 2, neck: -6, sf: 95, ef: 70, sb: 10, eb: 40, hf: 10, kf: -12, hb: -8, kb: -6),
                ["FidgetRest"] = P(0f, -0.004f, spine: 6, neck: -8, sf: -15, ef: 100, sb: -8, eb: 20, hf: 8, kf: -10, hb: -8, kb: -6),
                ["TurnPlant"] = P(0f, -0.078f, spine: -12, neck: 6, sf: 40, ef: 60, sb: 45, eb: 50, hf: 10, kf: -45, hb: -35, kb: -10),
                ["TurnPush"] = P(0f, -0.11f, spine: 18, neck: -10, sf: -25, ef: 80, sb: 30, eb: 70, hf: 35, kf: -60, hb: -30, kb: -15),
                ["StopPlant"] = P(0f, -0.146f, spine: -14, neck: 8, sf: 45, ef: 50, sb: 35, eb: 40, hf: 38, kf: -6, hb: -22, kb: -55),
                ["StopCrouch"] = P(0f, -0.062f, spine: 10, neck: -6, sf: 25, ef: 55, sb: 5, eb: 30, hf: 25, kf: -45, hb: -20, kb: -40),
                ["JumpLaunch"] = P(0f, 0f, spine: 8, neck: -5, sf: 140, ef: 20, sb: 110, eb: 25, hf: 12, kf: -12, hb: -18, kb: -22),
                ["JumpApex"] = P(0f, 0f, spine: 6, neck: -4, sf: 105, ef: 35, sb: 75, eb: 40, hf: 62, kf: -95, hb: 18, kb: -80),
                ["FallB"] = P(0f, 0f, spine: -3, neck: 2, sf: 135, ef: 15, sb: 125, eb: 30, hf: 15, kf: -45, hb: -20, kb: -35),
                ["LandRecover"] = P(0f, -0.048f, spine: 8, neck: -5, sf: 20, ef: 48, sb: -8, eb: 20, hf: 25, kf: -45, hb: 20, kb: -40),
                ["HardLandImpact"] = P(0f, -0.4f, spine: 52, neck: -22, sf: 150, ef: 25, sb: 70, eb: 5, hf: 75, kf: -120, hb: 0, kb: -90),
                ["HardLandRise"] = P(0f, -0.171f, spine: 18, neck: -10, sf: 60, ef: 48, sb: 35, eb: 30, hf: 45, kf: -75, hb: -10, kb: -60),
                ["DashBurst"] = P(0f, -0.172f, spine: 40, neck: -25, sf: -60, ef: 20, sb: -95, eb: 10, hf: 55, kf: -60, hb: -45, kb: -20),
                ["DashBrake"] = P(0f, -0.096f, spine: -8, neck: 5, sf: -55, ef: 0, sb: 20, eb: 40, hf: 35, kf: -15, hb: -15, kb: -50),
                ["SkidBrace"] = P(0f, -0.196f, spine: 25, neck: -12, sf: 60, ef: 30, sb: 45, eb: 30, hf: 30, kf: -80, hb: -40, kb: -10),
                ["SkidB"] = P(0f, -0.137f, spine: 15, neck: -8, sf: 75, ef: 20, sb: 35, eb: 40, hf: 22, kf: -65, hb: -52, kb: -2),
                ["WallSlideB"] = P(0f, 0f, spine: -6, neck: 6, sf: 45, ef: 35, sb: -105, eb: 10, hf: 40, kf: -70, hb: -30, kb: -30),
                ["WallKick"] = P(0f, 0f, spine: 15, neck: -8, sf: 120, ef: 20, sb: 60, eb: 30, hf: 40, kf: -70, hb: -70, kb: 0),
                ["FastFallB"] = P(0f, 0f, spine: 12, neck: -12, sf: 160, ef: 8, sb: 175, eb: 5, hf: 8, kf: -5, hb: -8, kb: -8),
                ["HurtSnap"] = P(0f, -0.024f, spine: -35, neck: 25, sf: 120, ef: 20, sb: 75, eb: 30, hf: -20, kf: -20, hb: 20, kb: -20),
                ["HurtStagger"] = P(0f, -0.067f, spine: -15, neck: 12, sf: 10, ef: 60, sb: 40, eb: 45, hf: 15, kf: -45, hb: -15, kb: -30),
                ["HurtRecover"] = P(0f, -0.02f, spine: 5, neck: -4, sf: 30, ef: 55, sb: 0, eb: 25, hf: 15, kf: -25, hb: -12, kb: -15),
                ["ParryFlick"] = P(0f, -0.039f, spine: -5, neck: 0, sf: 175, ef: 5, sb: 30, eb: 40, hf: 25, kf: -30, hb: -25, kb: -15),
                ["ParrySheath"] = P(0f, -0.015f, spine: 5, neck: -5, sf: 60, ef: 80, sb: -10, eb: 30, hf: 15, kf: -20, hb: -15, kb: -10),
                ["BreakerCrouch"] = P(0f, -0.189f, spine: 30, neck: -15, sf: -20, ef: 120, sb: -20, eb: 120, hf: 45, kf: -80, hb: -10, kb: -60),
                ["BreakerSettle"] = P(0f, -0.039f, spine: 5, neck: -3, sf: 60, ef: 40, sb: -30, eb: 30, hf: 25, kf: -30, hb: -25, kb: -15),
                ["RedrawB"] = P(0f, -0.424f, spine: 10, neck: -5, sf: 140, ef: 60, sb: 0, eb: 30, hf: 80, kf: -120, hb: -30, kb: -90),
                ["RedrawC"] = P(0f, -0.424f, spine: 10, neck: -5, sf: 100, ef: 100, sb: 0, eb: 30, hf: 80, kf: -120, hb: -30, kb: -90),
                ["RedrawD"] = P(0f, -0.424f, spine: 10, neck: -5, sf: 60, ef: 70, sb: 0, eb: 30, hf: 80, kf: -120, hb: -30, kb: -90),
                ["DefeatedStagger"] = P(0f, -0.1f, spine: -20, neck: 20, sf: 20, ef: 40, sb: 50, eb: 40, hf: 10, kf: -50, hb: -10, kb: -40),
                ["Defeated"] = P(0f, -0.4f, spine: 25, neck: 25, sf: 70, ef: 10, sb: 20, eb: 30, hf: 75, kf: -120, hb: 0, kb: -90),
                ["DefeatedFall"] = P(0f, -0.272f, spine: 20, neck: 20, sf: 110, ef: 10, sb: 60, eb: 20, hf: 40, kf: -100, hb: 0, kb: -80, tilt: 40),
                ["DefeatedLie"] = P(0f, -0.534f, spine: 0, neck: 15, sf: 160, ef: 15, sb: 150, eb: 10, hf: 0, kf: -10, hb: -5, kb: -20, tilt: 90),
        };

        // ---------------- clips ----------------

        private const PoseEasing Snap = PoseEasing.Snap, Linear = PoseEasing.Linear, In = PoseEasing.EaseIn,
                                 Out = PoseEasing.EaseOut, InOut = PoseEasing.EaseInOut, Smooth = PoseEasing.Smooth;

        /// <summary>
        /// Clip timing. One-shots end on a 1-frame key so they settle exactly on it. Loops use Smooth (curved)
        /// blending. Frame counts match the states they play in (dash 10, combo breaker 14, Redraw 45 frames).
        /// </summary>
        private static Dictionary<string, ClipSpec> Clips() => new Dictionary<string, ClipSpec>
        {
            // Weight shifting from foot to foot, with the breathing layer on top (IdleBreathing).
            ["Idle"] = new ClipSpec(true, 6, ("Idle", 50, Smooth), ("IdleShiftA", 50, Smooth), ("Idle", 50, Smooth), ("IdleShiftB", 50, Smooth)),
            ["IdleBreathing"] = new ClipSpec(true, 0, ("Idle", 45, InOut), ("IdleBreath", 45, InOut)),
            // Glance back, twirl the blade, rest it, back to ready.
            ["IdleFidget"] = new ClipSpec(false, 6, ("Idle", 8, InOut), ("FidgetLook", 22, InOut), ("FidgetTwirl", 10, InOut), ("FidgetRest", 14, InOut), ("Idle", 1, Linear)),
            ["Turn"] = new ClipSpec(false, 1, ("TurnPlant", 3, Out), ("TurnPush", 4, InOut), ("TurnPush", 1, Linear)),
            ["RunStop"] = new ClipSpec(false, 2, ("StopPlant", 4, Out), ("StopCrouch", 8, InOut), ("Idle", 1, Linear)),
            // Legs extend and arms swing up on the first frames of the jump (it has already left the ground).
            ["Jump"] = new ClipSpec(false, 1, ("JumpLaunch", 3, Out), ("Jump", 6, InOut), ("Jump", 1, Linear)),
            ["JumpApex"] = new ClipSpec(false, 5, ("JumpApex", 1, Linear)),
            ["Fall"] = new ClipSpec(true, 8, ("Fall", 10, Smooth), ("FallB", 10, Smooth)),
            ["FastFall"] = new ClipSpec(true, 3, ("FastFall", 6, Smooth), ("FastFallB", 6, Smooth)),
            ["Land"] = new ClipSpec(false, 1, ("Land", 3, Out), ("LandRecover", 6, InOut), ("Idle", 1, Linear)),
            // Three-point landing, sword raised forward (kept forward so the blend never sweeps it through the floor).
            ["HardLand"] = new ClipSpec(false, 0, ("HardLandImpact", 10, InOut), ("HardLandRise", 12, InOut), ("Idle", 1, Linear)),
            ["Dash"] = new ClipSpec(false, 0, ("DashBurst", 2, Out), ("Dash", 5, Linear), ("DashBrake", 3, In), ("DashBrake", 1, Linear)),
            ["Skid"] = new ClipSpec(false, 2, ("SkidBrace", 3, Out), ("Skid", 8, InOut), ("SkidB", 8, InOut), ("Skid", 1, Linear)),
            ["WallSlide"] = new ClipSpec(true, 4, ("WallSlide", 12, Smooth), ("WallSlideB", 12, Smooth)),
            ["WallJump"] = new ClipSpec(false, 1, ("WallKick", 3, Out), ("WallJump", 8, InOut), ("WallJump", 1, Linear)),
            // Knocked back with the sword arm flung up, stagger, then back on guard.
            ["Hurt"] = new ClipSpec(false, 0, ("HurtSnap", 3, Out), ("HurtStagger", 8, InOut), ("HurtRecover", 1, Linear)),
            ["ParrySuccess"] = new ClipSpec(false, 0, ("ParryFlick", 4, Out), ("ParrySheath", 8, InOut), ("Idle", 1, Linear)),
            ["ComboBreaker"] = new ClipSpec(false, 0, ("BreakerCrouch", 2, Out), ("ComboBreaker", 6, Out), ("BreakerSettle", 6, InOut), ("BreakerSettle", 1, Linear)),
            // Kneeling, the brush-blade circles as the ink redraws the body.
            ["Redraw"] = new ClipSpec(true, 6, ("Redraw", 8, Smooth), ("RedrawB", 8, Smooth), ("RedrawC", 8, Smooth), ("RedrawD", 8, Smooth)),
            // Stagger, kneel leaning on the planted sword, fall forward, lie face down.
            ["Defeated"] = new ClipSpec(false, 2, ("DefeatedStagger", 10, Out), ("Defeated", 15, InOut), ("DefeatedFall", 8, In), ("DefeatedLie", 1, Linear)),
        };
    }
}
