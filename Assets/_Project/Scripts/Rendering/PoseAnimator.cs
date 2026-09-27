using Margin.Core;
using UnityEngine;

namespace Margin.Rendering
{
    /// <summary>
    /// Plays PoseClips on a StickFigureRig, one step per gameplay tick (spec 4.2).
    /// - Play(clip) switches clips with a short crossfade (the clip's fadeInFrames).
    /// - An optional additive clip (e.g. breathing) adds its motion on top of the base clip.
    /// - Follow-through (PoseMotionSettings): loose joints (head, free arm) trail and settle via springs.
    /// It does not tick itself: its owner (e.g. PlayerAnimator) calls Tick() once per gameplay tick,
    /// so animation pauses and frame-steps together with the game.
    ///
    /// Smooth at any refresh rate: gameplay ticks 60 times a second, but a 180 Hz screen draws 3 frames per tick.
    /// Between ticks, LateUpdate blends from the previous tick's pose to the current one (the same way Unity
    /// interpolates the Rigidbody2D's position), so limbs glide instead of stepping. Code that reads joint
    /// positions during a tick (smears, hit effects) still sees the exact tick pose.
    /// </summary>
    [DefaultExecutionOrder(-50)]   // LateUpdate before StickFigureRig and WeaponLine draw the pose
    public sealed class PoseAnimator : MonoBehaviour
    {
        [SerializeField] private StickFigureRig rig;
        [Tooltip("Follow-through tuning. Empty = defaults.")]
        [SerializeField] private PoseMotionSettings motion;

        private PoseClip clip;
        private PoseTimeline timeline;
        private float clipTime;   // in clip ticks; advances by PlaybackRate each game tick

        private FigurePose fadeFrom;
        private int fadeFrames;
        private int fadeTick;

        private PoseClip additive;
        private PoseTimeline additiveTimeline;
        private int additiveTick;
        private float additiveWeight;

        private readonly SecondaryMotion secondary = new SecondaryMotion();
        private FigurePose previousOutput;
        private float lastTickFixedTime = -1f;
        private bool hasTicked;

        public StickFigureRig Rig
        {
            get => rig;
            set => rig = value;
        }

        public PoseClip CurrentClip => clip;
        public int ClipTick => (int)clipTime;
        /// <summary>Clip ticks per game tick (1 = normal). Owners set it every tick, e.g. to sync a run to speed.</summary>
        public float PlaybackRate { get; set; } = 1f;
        /// <summary>True once a non-looping clip has played to its end.</summary>
        public bool Finished => timeline != null && !timeline.Loop && clipTime >= timeline.TotalFrames;
        public FigurePose Output { get; private set; }
        /// <summary>Extra forward lean in degrees added to the spine this tick (e.g. leaning into acceleration).</summary>
        public float Lean { get; set; }
        /// <summary>
        /// Owners set this while the character stands on the ground. Blending between two planted keys can
        /// straighten a leg and push its foot under the floor for a frame or two; with this on, the hips are
        /// lifted just enough to keep both feet on (or above) the floor. It never pushes the body down.
        /// </summary>
        public bool KeepFeetOnFloor { get; set; }
        public PoseMotionSettings Motion
        {
            get => motion != null ? motion : PoseMotionSettings.Defaults;
            set => motion = value;
        }

        /// <summary>
        /// Switches to a clip (no-op if it is already playing). keepPhase: continue at the same point of the
        /// cycle (e.g. run to sprint) so the legs don't pop back to the first frame.
        /// </summary>
        public void Play(PoseClip next, bool keepPhase = false)
        {
            if (next == null || next == clip) return;
            Restart(next, keepPhase);
        }

        /// <summary>Starts a clip from the beginning even if it is already playing (e.g. the same attack twice in a row).</summary>
        public void Restart(PoseClip next, bool keepPhase = false)
        {
            if (next == null) return;
            PoseTimeline nextTimeline = next.Timeline;
            if (nextTimeline == null) return;

            int nextTick = 0;
            if (keepPhase && timeline != null)
            {
                float phase = (clipTime % timeline.TotalFrames) / timeline.TotalFrames;
                nextTick = Mathf.RoundToInt(phase * nextTimeline.TotalFrames);
            }

            fadeFrom = Output;
            fadeFrames = clip == null ? 0 : next.fadeInFrames;   // the very first clip appears instantly
            fadeTick = 0;
            clip = next;
            timeline = nextTimeline;
            clipTime = nextTick;
        }

        /// <summary>Layers a clip's motion (relative to its first pose) on top. Pass null to remove.</summary>
        public void SetAdditive(PoseClip layer, float weight = 1f)
        {
            additiveWeight = weight;
            if (layer == additive) return;
            additive = layer;
            additiveTimeline = layer != null ? layer.Timeline : null;
            additiveTick = 0;
        }

        /// <summary>Advances one tick and poses the rig.</summary>
        public void Tick()
        {
            if (timeline == null || rig == null) return;

            previousOutput = hasTicked ? Output : timeline.Sample(clipTime);
            FigurePose pose = timeline.Sample(clipTime);
            clipTime += Mathf.Max(0f, PlaybackRate);

            if (additiveTimeline != null && additiveWeight > 0f)
            {
                FigurePose delta = FigurePose.Subtract(additiveTimeline.Sample(additiveTick++), additiveTimeline.FirstPose);
                pose = FigurePose.Add(pose, delta, additiveWeight);
            }

            if (fadeTick < fadeFrames)
            {
                fadeTick++;
                float t = PoseEasingMath.Apply(PoseEasing.EaseOut, fadeTick / (float)(fadeFrames + 1));
                pose = FigurePose.Lerp(fadeFrom, pose, t);
            }

            PoseMotionSettings m = Motion;
            if (m.enabled)
            {
                pose.spine += Lean;
                pose = secondary.Step(pose, m.Weights, m.frequency, m.damping, GameTime.TickDelta);
            }

            if (KeepFeetOnFloor) pose = LiftFeetToFloor(pose);

            Output = pose;
            rig.ApplyPose(pose);
            lastTickFixedTime = Time.fixedTime;
            hasTicked = true;
        }

        private FigurePose LiftFeetToFloor(FigurePose pose)
        {
            rig.ApplyPose(pose);
            float below = rig.FloorLocalY - rig.LowestFootLocalY;
            if (below > 0.005f) pose.rootOffsetY += below;
            return pose;
        }

        /// <summary>Settles the follow-through springs instantly (after a teleport or respawn).</summary>
        public void SnapMotion() => secondary.Reset(Output);

        private void LateUpdate()
        {
            if (!hasTicked || rig == null || !Application.isPlaying) return;

            // Didn't tick on the latest fixed step (hitstop, pause, frozen): hold the exact pose, don't wobble.
            if (!Mathf.Approximately(lastTickFixedTime, Time.fixedTime))
            {
                rig.ApplyPose(Output);
                return;
            }

            float alpha = Mathf.Clamp01((Time.time - Time.fixedTime) / Time.fixedDeltaTime);
            rig.ApplyPose(FigurePose.Lerp(previousOutput, Output, alpha));
        }
    }
}
