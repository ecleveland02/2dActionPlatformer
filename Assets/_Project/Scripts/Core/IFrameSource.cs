namespace Margin.Core
{
    /// <summary>
    /// Anything that can tell gameplay code which fixed tick (frame) it is.
    /// Gameplay systems depend on this interface instead of Time.time so that
    /// all timing is counted in frames and can be faked in tests.
    /// </summary>
    public interface IFrameSource
    {
        int CurrentFrame { get; }
    }
}
