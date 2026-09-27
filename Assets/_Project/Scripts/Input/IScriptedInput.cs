using UnityEngine;

namespace Margin.Input
{
    /// <summary>
    /// Input that the game can take over for a moment, e.g. to walk the player through a door during a room
    /// transition. While scripted, Move and JumpHeld come from the script and button presses are ignored.
    /// </summary>
    public interface IScriptedInput
    {
        bool IsScripted { get; }
        void Script(Vector2 move, bool jumpHeld);
        void ClearScript();
    }
}
