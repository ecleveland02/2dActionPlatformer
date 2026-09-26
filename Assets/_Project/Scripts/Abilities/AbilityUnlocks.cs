using System;

namespace Margin.Abilities
{
    /// <summary>
    /// Which abilities the player has (spec Section 8). Code checks these flags before allowing a state.
    /// Plain serializable class so it shows in the Inspector now and can be saved to JSON later (Section 15).
    /// </summary>
    [Serializable]
    public sealed class AbilityUnlocks
    {
        public bool dash = true;           // starting ability in the vertical slice
        public bool grappleLine;           // Boss 1: Highlighter
        public bool doubleJump;            // Boss 2: Stapler Titan
        public bool groundPound;           // Boss 3: Spiral
        public bool wallCling;             // Boss 4: Crossout (wall slide + wall jump)
        public bool carbonCopy;            // post-Crossout reward
    }
}
