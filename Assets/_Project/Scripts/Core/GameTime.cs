namespace Margin.Core
{
    /// <summary>
    /// The fixed tick rate. This is an architectural constant (Section 0: everything runs at 60 Hz),
    /// not a tuning value, so it lives in code rather than a ScriptableObject.
    /// Project Settings > Time > Fixed Timestep must match TickDelta.
    /// </summary>
    public static class GameTime
    {
        public const int TicksPerSecond = 60;
        public const float TickDelta = 1f / TicksPerSecond;
    }
}
