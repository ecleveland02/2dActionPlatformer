using System.Collections.Generic;
using Margin.Combat;
using Margin.Rendering;
using UnityEditor;
using UnityEngine;
using static Margin.EditorTools.StarterPoses;

namespace Margin.EditorTools
{
    /// <summary>
    /// Poses traced from the katana sprite sheets (Katana key-frame sheets 01-07, six keys per animation).
    /// Each drawing was fitted onto the rig offline: head, torso, legs and scabbard matched to the ink, the sword
    /// arm solved so the hand holds the drawn hilt, and the grip angle turning the blade to the drawn blade.
    /// Then the game's rules were applied: planted feet on the floor (flight measured from the drawing), blade never
    /// under the floor, and for attacks the blade crossing the move's existing hitbox on every active frame.
    /// The pose values live in StarterAnimations.TracedData.cs (generated).
    /// </summary>
    public static partial class StarterAnimations
    {
        private const string AttackFolder = "Assets/_Project/Data/Attacks";

        private static void ApplyTraced()
        {
            foreach (KeyValuePair<string, FigurePose> pose in TracedPoses()) WritePose(pose.Key, pose.Value);
            AssetDatabase.SaveAssets();
            foreach (KeyValuePair<string, ClipSpec> clip in TracedClips()) WriteClip(clip.Key, clip.Value);
            ApplyTracedAttacks();
            AssetDatabase.SaveAssets();
            MeasureStrides();   // the traced run and sprint get their own stride lengths for foot lock
        }

        /// <summary>
        /// Attack clips from the traced keys, laid out on the move's own frame data (so a retuned move still lines
        /// up), in a stick-fight style: a quick wind-up that keeps drifting back (a moving hold, never frozen), a
        /// 2-frame accelerating swing that lands the strike on the first active frame, strike blending to its end
        /// across the active frames (unchanged, so the blade still crosses the hitbox), then a follow-through that
        /// overshoots past the end pose and settles into the recovery.
        /// </summary>
        private static void ApplyTracedAttacks()
        {
            foreach (string move in TracedAttackMoves)
            {
                var attack = AssetDatabase.LoadAssetAtPath<AttackData>($"{AttackFolder}/{move}.asset");
                if (attack == null) continue;
                WriteClip(move, AttackSpec(move, attack.startupFrames, attack.activeFrames, attack.recoveryFrames));
                var clip = AssetDatabase.LoadAssetAtPath<PoseClip>($"{ClipFolder}/{move}.asset");
                if (attack.poseClip != clip)
                {
                    attack.poseClip = clip;
                    EditorUtility.SetDirty(attack);
                }
            }
        }

        /// <summary>
        /// Keys: Entry > Windup > WindupDeep (drift) > Strike (first active frame) > StrikeEnd (last active) >
        /// FollowThrough (overshoot) > Recover > Exit. Pose i blends to pose i+1 over its frame count.
        /// Mirrored by the offline checker (timing.py attack_spec); keep them in step.
        /// </summary>
        internal static ClipSpec AttackSpec(string m, int startup, int active, int recovery)
        {
            var e = new List<(string, int, PoseEasing)>();
            int swing = Mathf.Min(2, startup);
            int before = startup - swing;
            if (before >= 2)
            {
                int entry = Mathf.Max(1, before / 2);
                e.Add((m + "Entry", entry, PoseEasing.EaseOut));          // snap into the wind-up
                e.Add((m + "Windup", before - entry, PoseEasing.Linear)); // keep drawing back (anticipation)
            }
            else if (before == 1)
            {
                e.Add((m + "Windup", 1, PoseEasing.Linear));
            }
            e.Add((m + (before >= 1 ? "WindupDeep" : "Windup"), swing, PoseEasing.EaseIn));  // accelerate into the hit
            e.Add((m + "Strike", Mathf.Max(1, active), PoseEasing.Linear));  // first active frame = strike pose
            int r0 = Mathf.Max(1, Mathf.RoundToInt(recovery * 0.2f));
            int r1 = Mathf.Max(1, Mathf.RoundToInt(recovery * 0.35f));
            int r2 = Mathf.Max(1, recovery - r0 - r1);
            e.Add((m + "StrikeEnd", r0, PoseEasing.EaseOut));        // momentum carries past the end pose...
            e.Add((m + "FollowThrough", r1, PoseEasing.Smooth));     // ...then flows back...
            e.Add((m + "Recover", r2, PoseEasing.EaseInOut));        // ...into the recovery
            e.Add((m + "Exit", 1, PoseEasing.Linear));
            return new ClipSpec(false, 1, e.ToArray());
        }
    }
}
