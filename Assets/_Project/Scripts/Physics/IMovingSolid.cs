using UnityEngine;

namespace Margin.Physics
{
    /// <summary>
    /// Solid ground that moves (World 2's grid blocks). A KinematicBody2D standing on it is carried by
    /// <see cref="CarryDelta"/> at the start of its next move. <see cref="MoveStamp"/> changes every time the solid
    /// moves, so a body is never carried twice for the same step.
    /// </summary>
    public interface IMovingSolid
    {
        Vector2 CarryDelta { get; }
        int MoveStamp { get; }
    }
}
