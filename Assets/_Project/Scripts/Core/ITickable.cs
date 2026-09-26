namespace Margin.Core
{
    /// <summary>
    /// Anything that runs gameplay logic once per fixed 60 Hz tick. The GameLoop calls Tick()
    /// on every registered ITickable in ascending TickOrder (input first, then the player, ...).
    /// </summary>
    public interface ITickable
    {
        int TickOrder { get; }
        void Tick();
    }
}
