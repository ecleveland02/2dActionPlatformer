using System.Collections.Generic;
using Margin.Player;
using Margin.Rendering;
using UnityEditor;
using UnityEngine;

namespace Margin.EditorTools
{
    /// <summary>
    /// Menu: Margin > Create Starter Animations. Writes a complete placeholder set so the stick figure animates
    /// right away: poses (Data/Poses), clips (Data/Animations) and the PlayerAnimationSet (Data).
    /// They are deliberately simple; refine the poses in the Pose Studio and the timing in each clip.
    /// Existing files are only overwritten if you confirm.
    ///
    /// Angles: degrees relative to the parent bone, positive = toward facing. Knees bend negative, elbows positive.
    /// </summary>
    public static class StarterPoses
    {
        private const string Folder = "Assets/_Project/Data/Poses";
        private const string ClipFolder = "Assets/_Project/Data/Animations";
        private const string SetPath = "Assets/_Project/Data/PlayerAnimationSet.asset";

        [MenuItem("Margin/Create Starter Animations")]
        public static void CreateFromMenu()
        {
            int existing = 0;
            foreach (string name in BuildTable().Keys)
                if (AssetDatabase.LoadAssetAtPath<PoseData>(PathFor(name)) != null) existing++;

            bool overwrite = existing > 0 && EditorUtility.DisplayDialog("Create Starter Animations",
                $"{existing} starter poses already exist in {Folder}. Overwrite the starter poses and clips with the " +
                "original versions? (Choose 'Keep' to only add what's missing and keep your edits.)", "Overwrite", "Keep");

            PlayerAnimationSet set = CreateAll(overwrite);
            EditorGUIUtility.PingObject(set);
        }

        /// <summary>Creates any missing poses, clips and the animation set without asking. Returns the set.</summary>
        internal static PlayerAnimationSet EnsureCreated()
        {
            // Projects made before the 8-key cycles have a 6-entry Run clip: upgrade the generated cycles once.
            var run = AssetDatabase.LoadAssetAtPath<PoseClip>($"{ClipFolder}/Run.asset");
            if (run != null && run.entries.Count == 6) UpgradeCycles();
            return CreateAll(overwrite: false);
        }

        /// <summary>
        /// Menu: Margin > Upgrade Run Cycles. Rewrites the generated run, sprint and walk poses (Run1-8, Sprint1-8,
        /// Walk1-8) and their clips with the current generator. Other poses and clips are not touched.
        /// </summary>
        [MenuItem("Margin/Upgrade Run Cycles")]
        public static void UpgradeCyclesFromMenu()
        {
            if (!EditorUtility.DisplayDialog("Upgrade Run Cycles",
                "Rewrite the Run, Sprint and Walk poses and clips (and the Grunt/Lancer walk clips) with the smooth " +
                "8-key cycles? Hand edits to those poses will be replaced. Nothing else is changed.", "Upgrade", "Cancel"))
                return;
            UpgradeCycles();
        }

        internal static void UpgradeCycles()
        {
            StickFigureRigEditor.EnsureFolder(Folder);
            StickFigureRigEditor.EnsureFolder(ClipFolder);
            var table = new Dictionary<string, FigurePose>();
            foreach (Gait gait in Gaits) AddCycle(table, gait);
            foreach (KeyValuePair<string, FigurePose> entry in table) WritePose(entry.Key, entry.Value);

            Dictionary<string, ClipSpec> specs = ClipTable();
            foreach (string name in new[] { "Run", "Sprint" }) WriteClip(name, specs[name]);
            foreach ((string clip, string prefix, int frames) in EnemyWalks)
                if (AssetDatabase.LoadAssetAtPath<PoseClip>($"{ClipFolder}/{clip}.asset") != null)
                    WriteCycleClip(clip, prefix, frames);
            MeasureStrides();
            AssetDatabase.SaveAssets();
            Debug.Log("Run, sprint and walk cycles upgraded to smooth 8-key cycles.");
        }

        internal static void WritePose(string name, FigurePose pose)
        {
            string path = PathFor(name);
            var asset = AssetDatabase.LoadAssetAtPath<PoseData>(path);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<PoseData>();
                asset.pose = pose;
                AssetDatabase.CreateAsset(asset, path);
            }
            else
            {
                asset.pose = pose;
                EditorUtility.SetDirty(asset);
            }
        }

        /// <summary>Enemy walk clips and their frames per key (grunt: 40-frame stride, lancer: 48).</summary>
        internal static readonly (string clip, string poses, int framesPerKey)[] EnemyWalks =
            { ("GruntWalk", "Walk", 5), ("LancerWalk", "WalkArmed", 6) };

        /// <summary>
        /// Sets each cycle clip's stride length (body travel per loop with planted feet not sliding), measured from
        /// its poses with the default leg lengths. The animators use it to play cycles in step with speed.
        /// </summary>
        internal static void MeasureStrides()
        {
            var defaults = ScriptableObject.CreateInstance<StickFigureProportions>();
            float thigh = defaults.thigh, shin = defaults.shin;
            Object.DestroyImmediate(defaults);

            var names = new List<string> { "Run", "Sprint" };
            foreach (var (clip, _, _) in EnemyWalks) names.Add(clip);
            foreach (string name in names)
            {
                var clip = AssetDatabase.LoadAssetAtPath<PoseClip>($"{ClipFolder}/{name}.asset");
                if (clip == null) continue;
                clip.InvalidateTimeline();
                if (clip.Timeline == null) continue;
                clip.strideLength = CycleSync.StrideLength(clip.Timeline, thigh, shin);
                EditorUtility.SetDirty(clip);
            }
        }

        /// <summary>Creates or rewrites a looping 8-key cycle clip from poses named prefix1..prefix8.</summary>
        internal static PoseClip WriteCycleClip(string clipName, string posePrefix, int framesPerKey)
        {
            WriteClip(clipName, Cycle(posePrefix, framesPerKey, fadeIn: 4));
            return AssetDatabase.LoadAssetAtPath<PoseClip>($"{ClipFolder}/{clipName}.asset");
        }

        internal static void WriteClip(string name, ClipSpec spec)
        {
            string path = $"{ClipFolder}/{name}.asset";
            var clip = AssetDatabase.LoadAssetAtPath<PoseClip>(path);
            bool isNew = clip == null;
            if (isNew) clip = ScriptableObject.CreateInstance<PoseClip>();
            FillClip(clip, spec);
            clip.InvalidateTimeline();
            if (isNew) AssetDatabase.CreateAsset(clip, path);
            else EditorUtility.SetDirty(clip);
        }

        private static void FillClip(PoseClip clip, ClipSpec spec)
        {
            clip.entries.Clear();
            foreach ((string pose, int frames, PoseEasing easing) in spec.Entries)
            {
                clip.entries.Add(new PoseClip.Entry
                {
                    pose = AssetDatabase.LoadAssetAtPath<PoseData>(PathFor(pose)),
                    frames = frames,
                    easing = easing,
                });
            }
            clip.loop = spec.Loop;
            clip.fadeInFrames = spec.FadeIn;
        }

        private static PlayerAnimationSet CreateAll(bool overwrite)
        {
            CreatePoses(overwrite);
            Dictionary<string, PoseClip> clips = CreateClips(overwrite);
            MeasureStrides();
            PlayerAnimationSet set = CreateSet(clips);
            AssetDatabase.SaveAssets();
            // Overwriting restores the full multi-key set (it needs the combat poses, so only once they exist).
            if (overwrite && AssetDatabase.LoadAssetAtPath<PoseData>(PathFor("Parry")) != null) StarterAnimations.Apply();
            return set;
        }

        private static void CreatePoses(bool overwrite)
        {
            Dictionary<string, FigurePose> poses = BuildTable();
            StickFigureRigEditor.EnsureFolder(Folder);

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

            Debug.Log($"Starter poses: {created} created, {updated} overwritten, in {Folder}.");
        }

        // ---------------- clips ----------------

        internal struct ClipSpec
        {
            public (string pose, int frames, PoseEasing easing)[] Entries;
            public ClipSpec(bool loop, int fadeIn, params (string pose, int frames, PoseEasing easing)[] entries)
            {
                Entries = entries;
                Loop = loop;
                FadeIn = fadeIn;
            }
            public bool Loop;
            public int FadeIn;
        }

        private static ClipSpec Hold(string pose, int fadeIn) => new ClipSpec
        {
            Entries = new[] { (pose, 1, PoseEasing.Linear) }, Loop = false, FadeIn = fadeIn,
        };

        /// <summary>An 8-key cycle, blended with curves (Smooth) so it flows through the keys.</summary>
        internal static ClipSpec Cycle(string prefix, int framesPerPose, int fadeIn)
        {
            var entries = new (string, int, PoseEasing)[CycleKeys];
            for (int i = 0; i < CycleKeys; i++) entries[i] = ($"{prefix}{i + 1}", framesPerPose, PoseEasing.Smooth);
            return new ClipSpec { Entries = entries, Loop = true, FadeIn = fadeIn };
        }

        /// <summary>
        /// Starter timing. Run: 8 keys x 4 frames = a full stride every 0.53 s. Sprint: 8 x 3 = 0.4 s.
        /// Single-pose clips rely on the crossfade (FadeIn frames) for motion; Dash snaps instantly for crispness.
        /// </summary>
        private static Dictionary<string, ClipSpec> ClipTable() => new Dictionary<string, ClipSpec>
        {
            ["Idle"] = new ClipSpec { Entries = new[] { ("Idle", 1, PoseEasing.Linear) }, Loop = true, FadeIn = 6 },
            ["IdleBreathing"] = new ClipSpec
            {
                Entries = new[] { ("Idle", 45, PoseEasing.EaseInOut), ("IdleBreath", 45, PoseEasing.EaseInOut) },
                Loop = true, FadeIn = 0,
            },
            ["Run"] = Cycle("Run", 4, fadeIn: 4),
            ["Sprint"] = Cycle("Sprint", 3, fadeIn: 6),
            ["Skid"] = Hold("Skid", 3),
            ["Jump"] = Hold("Jump", 3),
            ["Fall"] = Hold("Fall", 8),
            ["FastFall"] = Hold("FastFall", 3),
            ["Land"] = Hold("Land", 2),
            ["Dash"] = Hold("Dash", 0),
            ["WallSlide"] = Hold("WallSlide", 4),
            ["WallJump"] = Hold("WallJump", 2),
        };

        private static Dictionary<string, PoseClip> CreateClips(bool overwrite)
        {
            StickFigureRigEditor.EnsureFolder(ClipFolder);
            var result = new Dictionary<string, PoseClip>();
            int created = 0, updated = 0;

            foreach (KeyValuePair<string, ClipSpec> spec in ClipTable())
            {
                string path = $"{ClipFolder}/{spec.Key}.asset";
                var clip = AssetDatabase.LoadAssetAtPath<PoseClip>(path);
                bool isNew = clip == null;
                if (isNew) clip = ScriptableObject.CreateInstance<PoseClip>();

                if (isNew || overwrite)
                {
                    FillClip(clip, spec.Value);

                    if (isNew) { AssetDatabase.CreateAsset(clip, path); created++; }
                    else { EditorUtility.SetDirty(clip); updated++; }
                }
                result[spec.Key] = clip;
            }

            Debug.Log($"Starter clips: {created} created, {updated} overwritten, in {ClipFolder}.");
            return result;
        }

        private static PlayerAnimationSet CreateSet(Dictionary<string, PoseClip> clips)
        {
            var set = AssetDatabase.LoadAssetAtPath<PlayerAnimationSet>(SetPath);
            if (set == null)
            {
                set = ScriptableObject.CreateInstance<PlayerAnimationSet>();
                AssetDatabase.CreateAsset(set, SetPath);
            }

            // Only fill empty slots, so a set you customized keeps your choices.
            PoseClip Fill(PoseClip current, string name) => current != null ? current : clips[name];
            set.idle = Fill(set.idle, "Idle");
            set.idleBreathing = Fill(set.idleBreathing, "IdleBreathing");
            set.run = Fill(set.run, "Run");
            set.sprint = Fill(set.sprint, "Sprint");
            set.skid = Fill(set.skid, "Skid");
            set.jump = Fill(set.jump, "Jump");
            set.fall = Fill(set.fall, "Fall");
            set.fastFall = Fill(set.fastFall, "FastFall");
            set.land = Fill(set.land, "Land");
            set.dash = Fill(set.dash, "Dash");
            set.wallSlide = Fill(set.wallSlide, "WallSlide");
            set.wallJump = Fill(set.wallJump, "WallJump");
            EditorUtility.SetDirty(set);
            return set;
        }

        internal static string PathFor(string name) => $"{Folder}/{name}.asset";

        internal static Dictionary<string, FigurePose> BuildTable()
        {
            var t = new Dictionary<string, FigurePose>
            {
                ["Neutral"] = FigurePose.Neutral,
                // Grounded poses are Planted(): the hips height is computed so the lowest foot touches the ground.
                // Low-ready: katana angled forward-down, tip clear of the floor. (StarterAnimations adds the rest.)
                ["Idle"] = P(0f, -0.004f, spine: 3, neck: -3, sf: 18, ef: 45, sb: -5, eb: 15, hf: 8, kf: -10, hb: -8, kb: -6),
                ["IdleBreath"] = P(0f, -0.02f, spine: 5, neck: -4, sf: 21, ef: 47, sb: -2, eb: 17, hf: 8, kf: -12, hb: -8, kb: -8),
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

            foreach (Gait gait in Gaits) AddCycle(t, gait);
            return t;
        }

        // ---------------- run, sprint and walk cycles ----------------

        private const int CycleKeys = 8;

        /// <summary>Shape of one gait. Angles in degrees; see AddCycle for what each one does.</summary>
        private sealed class Gait
        {
            public string Name;
            public float LegSwing, KneeDrive, ContactBend, StanceBend, Tuck;
            public float ArmSwing, ArmLag, Elbow, ElbowSwing;
            public float Spine, Wobble, Neck, Flight;
            /// <summary>Front (weapon) arm held steady instead of swinging: shoulder, elbow, small swing.
            /// Keeps a sword or spear level instead of sweeping it through the floor.</summary>
            public bool HoldsWeapon;
            public float HeldShoulder, HeldElbow, HeldSwing;
        }

        private static readonly Gait[] Gaits =
        {
            // Leg swings are sized so the stride fits the movement speed (feet lock at about 3 strides/s for run 9 u/s,
            // 3.5 for sprint 13 u/s, 2 for a 2.2 u/s walk) instead of the legs spinning.
            new Gait { Name = "Run", LegSwing = 52, KneeDrive = 22, ContactBend = 12, StanceBend = 28, Tuck = 80,
                ArmSwing = 35, ArmLag = 0.06f, Elbow = 80, ElbowSwing = 20, Spine = 12, Wobble = 3, Neck = -8, Flight = 0.05f,
                HoldsWeapon = true, HeldShoulder = 45, HeldElbow = 65, HeldSwing = 10 },
            new Gait { Name = "Sprint", LegSwing = 64, KneeDrive = 45, ContactBend = 12, StanceBend = 32, Tuck = 105,
                ArmSwing = 55, ArmLag = 0.05f, Elbow = 90, ElbowSwing = 15, Spine = 22, Wobble = 4, Neck = -15, Flight = 0.08f,
                HoldsWeapon = true, HeldShoulder = 45, HeldElbow = 75, HeldSwing = 8 },
            // Enemies walk: always one foot down (no flight), small arm swing.
            new Gait { Name = "Walk", LegSwing = 39, KneeDrive = 8, ContactBend = 4, StanceBend = 10, Tuck = 38,
                ArmSwing = 16, ArmLag = 0.08f, Elbow = 18, ElbowSwing = 12, Spine = 4, Wobble = 1.5f, Neck = -3, Flight = 0f },
            // Walk with a spear held level (Pencil Lancer).
            new Gait { Name = "WalkArmed", LegSwing = 39, KneeDrive = 8, ContactBend = 4, StanceBend = 10, Tuck = 38,
                ArmSwing = 16, ArmLag = 0.08f, Elbow = 18, ElbowSwing = 12, Spine = 4, Wobble = 1.5f, Neck = -3, Flight = 0f,
                HoldsWeapon = true, HeldShoulder = 50, HeldElbow = 40, HeldSwing = 6 },
        };

        /// <summary>
        /// An 8-key gait cycle. Phase 0 = the front foot touches down ahead of the body; the back leg runs half a
        /// cycle behind. Over one stride each leg: lands with a slight bend, sinks under the weight (stance bend),
        /// pushes off nearly straight, tucks its heel up behind (recovery), then drives the knee forward and
        /// reaches for the next step. Arms swing against their leg and trail it slightly (arm lag), bending more on
        /// the forward swing. The lean and head counter-bob twice per stride. Every key is planted (lowest foot on
        /// the ground), then lifted by Flight just before each landing so both feet leave the ground in a run.
        /// Played with Smooth (curved) blending between keys.
        /// </summary>
        private static void AddCycle(Dictionary<string, FigurePose> table, Gait g)
        {
            for (int i = 0; i < CycleKeys; i++)
            {
                float phase = i / (float)CycleKeys;
                (float hf, float kf) = Leg(g, phase);
                (float hb, float kb) = Leg(g, phase + 0.5f);
                (float sf, float ef) = Arm(g, phase);
                if (g.HoldsWeapon)
                {
                    sf = g.HeldShoulder + g.HeldSwing * Mathf.Cos(2f * Mathf.PI * (phase - g.ArmLag));
                    ef = g.HeldElbow;
                }
                (float sb, float eb) = Arm(g, phase + 0.5f);
                float sway = Mathf.Cos(4f * Mathf.PI * (phase - 0.1f));

                FigurePose pose = Planted(P(0f, 0f, spine: g.Spine + g.Wobble * sway, neck: g.Neck - 0.5f * g.Wobble * sway,
                                            sf: sf, ef: ef, sb: sb, eb: eb, hf: hf, kf: kf, hb: hb, kb: kb));
                pose.rootOffsetY += g.Flight * (Bump(phase, 0.42f, 0.06f) + Bump(phase, 0.92f, 0.06f));
                table[$"{g.Name}{i + 1}"] = pose;
            }
        }

        /// <summary>Hip and knee angles of a leg at a phase (0 = this foot lands).</summary>
        private static (float hip, float knee) Leg(Gait g, float phase)
        {
            float hip = g.LegSwing * Mathf.Cos(2f * Mathf.PI * phase) + g.KneeDrive * Bump(phase, 0.85f, 0.1f);
            float knee = -(g.ContactBend + g.StanceBend * Bump(phase, 0.12f, 0.08f) + g.Tuck * Bump(phase, 0.72f, 0.13f));
            return (hip, knee);
        }

        /// <summary>Shoulder and elbow of the arm on the same side as the leg at this phase (it swings opposite).</summary>
        private static (float shoulder, float elbow) Arm(Gait g, float phase)
        {
            float c = Mathf.Cos(2f * Mathf.PI * (phase - g.ArmLag));
            return (-g.ArmSwing * c, g.Elbow + g.ElbowSwing * Mathf.Max(0f, -c));
        }

        /// <summary>A smooth bump (bell curve) centered on a phase, wrapping around the cycle.</summary>
        private static float Bump(float phase, float center, float width)
        {
            float d = Mathf.Repeat(phase - center + 0.5f, 1f) - 0.5f;
            return Mathf.Exp(-d * d / (2f * width * width));
        }

        /// <summary>
        /// Sets the hips height so the lower foot rests exactly on the ground line (where it is in the neutral pose).
        /// Uses the default leg lengths. A foot's height below the hips is thigh*cos(hip) + shin*cos(hip + knee).
        /// </summary>
        internal static FigurePose Planted(FigurePose pose)
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

        internal static FigurePose P(float x, float y, float spine, float neck, float sf, float ef, float sb, float eb,
                              float hf, float kf, float hb, float kb, float tilt = 0f)
        {
            return new FigurePose
            {
                rootOffsetX = x, rootOffsetY = y, rootRotation = tilt,
                spine = spine, neck = neck,
                shoulderFront = sf, elbowFront = ef, shoulderBack = sb, elbowBack = eb,
                hipFront = hf, kneeFront = kf, hipBack = hb, kneeBack = kb,
            };
        }
    }
}
