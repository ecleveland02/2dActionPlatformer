namespace Margin.Level
{
    public enum TransitionPhase { None, Out, In }

    /// <summary>
    /// The screen fade used for room transitions, pit falls and respawns (spec 11.1), counted in ticks.
    /// Out: the screen fades to paper. The tick it is fully covered, Tick() returns true once (move the player and
    /// switch rooms then, unseen). In: it fades back. Fade is 0 (clear) to 1 (covered).
    /// </summary>
    public sealed class TransitionTimer
    {
        private int outFrames = 1, inFrames = 1;

        public TransitionPhase Phase { get; private set; }
        /// <summary>Ticks spent in the current phase.</summary>
        public int Frame { get; private set; }
        public bool Active => Phase != TransitionPhase.None;

        public float Fade
        {
            get
            {
                switch (Phase)
                {
                    case TransitionPhase.Out: return Frame / (float)outFrames;
                    case TransitionPhase.In: return 1f - Frame / (float)inFrames;
                    default: return 0f;
                }
            }
        }

        /// <summary>Fade out over <paramref name="fadeOut"/> ticks, then back in over <paramref name="fadeIn"/>.</summary>
        public void Start(int fadeOut, int fadeIn)
        {
            outFrames = fadeOut < 1 ? 1 : fadeOut;
            inFrames = fadeIn < 1 ? 1 : fadeIn;
            Phase = TransitionPhase.Out;
            Frame = 0;
        }

        /// <summary>The screen is already covered (e.g. after a death): only fade back in.</summary>
        public void StartIn(int fadeIn)
        {
            inFrames = fadeIn < 1 ? 1 : fadeIn;
            Phase = TransitionPhase.In;
            Frame = 0;
        }

        public void Cancel()
        {
            Phase = TransitionPhase.None;
            Frame = 0;
        }

        /// <summary>Advances one tick. Returns true on the one tick the screen becomes fully covered.</summary>
        public bool Tick()
        {
            switch (Phase)
            {
                case TransitionPhase.Out:
                    Frame++;
                    if (Frame < outFrames) return false;
                    Phase = TransitionPhase.In;
                    Frame = 0;
                    return true;
                case TransitionPhase.In:
                    Frame++;
                    if (Frame >= inFrames) Cancel();
                    return false;
                default:
                    return false;
            }
        }
    }
}
