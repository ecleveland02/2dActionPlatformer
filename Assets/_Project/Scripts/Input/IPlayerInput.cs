using UnityEngine;

namespace Margin.Input
{
    /// <summary>
    /// What the player controller needs from input. InputReader implements it for real play;
    /// tests implement it with scripted values so movement can be tested frame by frame.
    /// </summary>
    public interface IPlayerInput
    {
        Vector2 Move { get; }
        bool JumpHeld { get; }
        bool DownHeld { get; }
        InputBuffer Buffer { get; }
    }
}
