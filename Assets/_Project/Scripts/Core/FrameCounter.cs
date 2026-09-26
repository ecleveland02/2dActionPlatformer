namespace Margin.Core
{
    /// <summary>
    /// The single gameplay clock. Advance() is called exactly once per fixed 60 Hz tick
    /// (by the game loop, or by frame-step debug tools). Pausing simply means not calling it.
    /// </summary>
    public sealed class FrameCounter : IFrameSource
    {
        public int CurrentFrame { get; private set; }

        public void Advance()
        {
            CurrentFrame++;
        }

        public void Reset()
        {
            CurrentFrame = 0;
        }
    }
}
