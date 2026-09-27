using UnityEngine;

namespace Margin.Rendering
{
    /// <summary>
    /// Plays PoseClips on a StickFigureRig, one step per gameplay tick (spec 4.2).
    /// - Play(clip) switches clips with a short crossfade (the clip's fadeInFrames).
    /// - An optional additive clip (e.g. breathing) adds its motion on top of the base clip.
    /// It does not tick itself: its owner (e.g. PlayerAnimator) calls Tick() once per gameplay tick,
    /// so animation pauses and frame-steps together with the game.
    /// </summary>
    public sealed class PoseAnimator : MonoBehaviour
    {
        [SerializeField] private StickFigureRig rig;

        private PoseClip clip;
        private PoseTimeline timeline;
        private int clipTick;

        private FigurePose fadeFrom;
        private int fadeFrames;
        private int fadeTick;

        private PoseClip additive;
        private PoseTimeline additiveTimeline;
        private int additiveTick;
        private float additiveWeight;

        public StickFigureRig Rig
        {
            get => rig;
            set => rig = value;
        }

        public PoseClip CurrentClip => clip;
        public int ClipTick => clipTick;
        public FigurePose Output { get; private set; }

        /// <summary>
        /// Switches to a clip (no-op if it is already playing). keepPhase: continue at the same point of the
        /// cycle (e.g. run to sprint) so the legs don't pop back to the first frame.
        /// </summary>
        public void Play(PoseClip next, bool keepPhase = false)
        {
            if (next == null || next == clip) return;
            PoseTimeline nextTimeline = next.Timeline;
            if (nextTimeline == null) return;

            int nextTick = 0;
            if (keepPhase && timeline != null)
            {
                float phase = (clipTick % timeline.TotalFrames) / (float)timeline.TotalFrames;
                nextTick = Mathf.RoundToInt(phase * nextTimeline.TotalFrames);
            }

            fadeFrom = Output;
            fadeFrames = clip == null ? 0 : next.fadeInFrames;   // the very first clip appears instantly
            fadeTick = 0;
            clip = next;
            timeline = nextTimeline;
            clipTick = nextTick;
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

            FigurePose pose = timeline.Sample(clipTick++);

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

            Output = pose;
            rig.ApplyPose(pose);
        }
    }
}
