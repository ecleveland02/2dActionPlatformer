using UnityEngine;

namespace Margin.Level
{
    /// <summary>
    /// Room transitions, pits, checkpoints and respawn timing (spec 10, 11). Times are in frames (60 per second).
    /// Created at Data/Level/LevelSettings by Margin > Build World 1. Scenes without an asset use these defaults.
    /// </summary>
    [CreateAssetMenu(menuName = "Margin/Level Settings", fileName = "LevelSettings")]
    public sealed class LevelSettings : ScriptableObject
    {
        [Header("Room transitions")]
        [Tooltip("Frames to fade to paper after walking through a door.")]
        [Min(1)] public int fadeOutFrames = 10;
        [Tooltip("Frames to fade back in, in the next room.")]
        [Min(1)] public int fadeInFrames = 14;
        [Tooltip("After arriving through a side door the player keeps walking in for this many frames " +
                 "(no control, can't be hurt), so they never stand in the doorway.")]
        [Min(0)] public int entryWalkFrames = 12;
        [Tooltip("Arriving from below (through a hole in the floor): upward speed as a multiple of the jump speed.")]
        [Min(0f)] public float upwardEntryBoost = 1.15f;

        [Header("Pits")]
        [Tooltip("Damage for falling out of the bottom of a room. Then the player is put back at the door they " +
                 "came in by (or the checkpoint).")]
        [Min(0)] public int pitDamage = 15;
        [Tooltip("How far below the room's camera bounds the player's center must fall to count as a pit.")]
        [Min(0f)] public float killPlaneMargin = 1.5f;
        [Min(1)] public int pitFadeOutFrames = 12;

        [Header("Death and respawn")]
        [Tooltip("The screen fades out over the last frames of the defeat animation, then back in on respawn.")]
        [Min(1)] public int deathFadeFrames = 24;
        [Tooltip("Frames to fade in after a respawn.")]
        [Min(1)] public int respawnFadeInFrames = 18;

        private static LevelSettings defaults;

        public static LevelSettings Defaults
        {
            get
            {
                if (defaults == null)
                {
                    defaults = CreateInstance<LevelSettings>();
                    defaults.hideFlags = HideFlags.DontSave;
                }
                return defaults;
            }
        }
    }
}
