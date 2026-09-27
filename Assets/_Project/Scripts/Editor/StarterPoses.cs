using System.Collections.Generic;
using Margin.Rendering;
using UnityEditor;
using UnityEngine;

namespace Margin.EditorTools
{
    /// <summary>
    /// Menu: Margin > Create Starter Poses. Writes a complete placeholder pose set to Data/Poses so the stick
    /// figure can animate right away. They are deliberately simple; refine them in the Pose Studio.
    /// Existing pose files are only overwritten if you confirm.
    ///
    /// Angles: degrees relative to the parent bone, positive = toward facing. Knees bend negative, elbows positive.
    /// </summary>
    public static class StarterPoses
    {
        private const string Folder = "Assets/_Project/Data/Poses";

        [MenuItem("Margin/Create Starter Poses")]
        public static void Create()
        {
            Dictionary<string, FigurePose> poses = BuildTable();
            StickFigureRigEditor.EnsureFolder(Folder);

            int existing = 0;
            foreach (string name in poses.Keys)
                if (AssetDatabase.LoadAssetAtPath<PoseData>(PathFor(name)) != null) existing++;

            bool overwrite = existing > 0 && EditorUtility.DisplayDialog("Create Starter Poses",
                $"{existing} of these poses already exist in {Folder}. Overwrite them with the starter versions? " +
                "(Choose 'Keep' to only add the missing ones.)", "Overwrite", "Keep");

            int created = 0, updated = 0;
            foreach (KeyValuePair<string, FigurePose> entry in poses)
            {
                string path = PathFor(entry.Key);
                var asset = AssetDatabase.LoadAssetAtPath<PoseData>(path);
                if (asset == null)
                {
                    asset = ScriptableObject.CreateInstance<PoseData>();
                    asset.pose = entry.Value;
                    AssetDatabase.CreateAsset(asset, path);
                    created++;
                }
                else if (overwrite)
                {
                    asset.pose = entry.Value;
                    EditorUtility.SetDirty(asset);
                    updated++;
                }
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"Starter poses: {created} created, {updated} overwritten, in {Folder}.");
        }

        private static string PathFor(string name) => $"{Folder}/{name}.asset";

        internal static Dictionary<string, FigurePose> BuildTable()
        {
            var t = new Dictionary<string, FigurePose>
            {
                ["Neutral"] = FigurePose.Neutral,
                // Grounded poses are Planted(): the hips height is computed so the lowest foot touches the ground.
                ["Idle"] = Planted(P(0, -0.01f, spine: 3, neck: -3, sf: 8, ef: 12, sb: -5, eb: 10, hf: 5, kf: -6, hb: -4, kb: -3)),
                ["IdleBreath"] = Planted(P(0, -0.025f, spine: 5, neck: -4, sf: 11, ef: 14, sb: -2, eb: 12, hf: 5, kf: -8, hb: -4, kb: -5)),
                ["Jump"] = P(0, 0f, spine: 6, neck: -4, sf: 150, ef: 15, sb: 120, eb: 20, hf: 55, kf: -75, hb: -10, kb: -25),
                ["Fall"] = P(0, 0f, spine: 0, neck: 0, sf: 115, ef: 25, sb: 150, eb: 15, hf: 25, kf: -35, hb: -15, kb: -45),
                // Crouch: the legs fold and planting drops the hips so the feet stay on the ground line.
                ["Land"] = Planted(P(0, -0.25f, spine: 15, neck: -10, sf: 25, ef: 40, sb: -20, eb: 30, hf: 50, kf: -90, hb: 40, kb: -85)),
                ["Dash"] = P(0, -0.10f, spine: 30, neck: -20, sf: -70, ef: 10, sb: -90, eb: 5, hf: 45, kf: -70, hb: -35, kb: -40),
                // Facing the new direction while sliding backwards: back leg braced toward the slide.
                ["Skid"] = Planted(P(0, -0.15f, spine: 18, neck: -10, sf: 70, ef: 25, sb: 40, eb: 35, hf: 25, kf: -70, hb: -50, kb: -5)),
                // Facing away from the wall (which is behind): back hand and foot reach toward it.
                ["WallSlide"] = P(0, 0f, spine: -8, neck: 5, sf: 40, ef: 30, sb: -95, eb: 15, hf: 45, kf: -80, hb: -25, kb: -35),
                ["WallJump"] = P(0, 0f, spine: 10, neck: -5, sf: 145, ef: 10, sb: 100, eb: 20, hf: 35, kf: -45, hb: -45, kb: -10),
                ["FastFall"] = P(0, 0f, spine: 10, neck: -10, sf: 170, ef: 0, sb: 165, eb: 0, hf: 5, kf: 0, hb: -5, kb: -5),
            };

            AddCycle(t, "Run", legSwing: 35f, kneeLift: 50f, armSwing: 30f, elbow: 75f, spine: 12f, neck: -8f);
            AddCycle(t, "Sprint", legSwing: 50f, kneeLift: 65f, armSwing: 50f, elbow: 90f, spine: 22f, neck: -15f);
            return t;
        }

        /// <summary>
        /// Six-frame run cycle from sine waves. Legs swing opposite each other, each knee bends while its leg
        /// swings forward, and arms swing opposite their leg. Every frame is planted, so the hips bob up and down
        /// naturally as the legs spread and pass (twice per cycle).
        /// </summary>
        private static void AddCycle(Dictionary<string, FigurePose> table, string name, float legSwing, float kneeLift,
                                     float armSwing, float elbow, float spine, float neck)
        {
            for (int i = 0; i < 6; i++)
            {
                float p = i * Mathf.PI * 2f / 6f;
                float swing = Mathf.Sin(p);
                float forward = Mathf.Cos(p);          // > 0 while the front leg moves forward
                table[$"{name}{i + 1}"] = Planted(P(0f, 0f,
                    spine: spine, neck: neck,
                    sf: -armSwing * swing, ef: elbow,
                    sb: armSwing * swing, eb: elbow,
                    hf: legSwing * swing, kf: -(10f + kneeLift * Mathf.Max(0f, forward)),
                    hb: -legSwing * swing, kb: -(10f + kneeLift * Mathf.Max(0f, -forward))));
            }
        }

        /// <summary>
        /// Sets the hips height so the lower foot rests exactly on the ground line (where it is in the neutral pose).
        /// Uses the default leg lengths. A foot's height below the hips is thigh*cos(hip) + shin*cos(hip + knee).
        /// </summary>
        private static FigurePose Planted(FigurePose pose)
        {
            var defaults = ScriptableObject.CreateInstance<StickFigureProportions>();
            float thigh = defaults.thigh, shin = defaults.shin;
            Object.DestroyImmediate(defaults);

            float Drop(float hip, float knee) =>
                thigh * Mathf.Cos(hip * Mathf.Deg2Rad) + shin * Mathf.Cos((hip + knee) * Mathf.Deg2Rad);

            float lowest = Mathf.Max(Drop(pose.hipFront, pose.kneeFront), Drop(pose.hipBack, pose.kneeBack));
            pose.rootOffsetY = lowest - (thigh + shin);   // 0 when the lowest leg is straight down
            return pose;
        }

        private static FigurePose P(float x, float y, float spine, float neck, float sf, float ef, float sb, float eb,
                              float hf, float kf, float hb, float kb)
        {
            return new FigurePose
            {
                rootOffsetX = x, rootOffsetY = y,
                spine = spine, neck = neck,
                shoulderFront = sf, elbowFront = ef, shoulderBack = sb, elbowBack = eb,
                hipFront = hf, kneeFront = kf, hipBack = hb, kneeBack = kb,
            };
        }
    }
}
