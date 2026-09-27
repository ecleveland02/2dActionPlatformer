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
        /// Attack clips from six traced keys, laid out on the move's own frame data (so a retuned move still lines
        /// up): entry, held wind-up, a 2-frame accelerating swing that lands the strike on the first active frame,
        /// strike blending to follow-through across the active frames, recovery, exit.
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

        internal static ClipSpec AttackSpec(string m, int startup, int active, int recovery)
        {
            var e = new List<(string, int, PoseEasing)>();
            int swing = Mathf.Min(2, startup);
            int before = startup - swing;
            if (before >= 3)
            {
                int entry = Mathf.Min(2, before - 1);
                e.Add((m + "Entry", entry, PoseEasing.EaseOut));
                e.Add((m + "Windup", before - entry, PoseEasing.Snap));   // hold the wind-up: anticipation
            }
            else if (before >= 1)
            {
                e.Add((m + "Windup", before, PoseEasing.Snap));
            }
            e.Add((m + "Windup", swing, PoseEasing.EaseIn));             // accelerate into the strike
            e.Add((m + "Strike", Mathf.Max(1, active), PoseEasing.Linear));  // first active frame = strike pose
            int r1 = Mathf.Max(1, Mathf.RoundToInt(recovery * 0.4f));
            int r2 = Mathf.Max(1, recovery - r1);
            e.Add((m + "StrikeEnd", r1, PoseEasing.EaseOut));
            e.Add((m + "Recover", r2, PoseEasing.EaseInOut));
            e.Add((m + "Exit", 1, PoseEasing.Linear));
            return new ClipSpec(false, 1, e.ToArray());
        }
    }
}
