using UnityEngine;

namespace Margin.Level
{
    /// <summary>
    /// How the camera follows the player (spec 12): a small horizontal and a larger vertical dead zone (so jumps
    /// don't bob the view), slight look-ahead toward the facing direction, easing back onto the player whenever
    /// they stand on the ground, and a confiner that keeps the view inside the current room.
    /// Distances are in units, times in frames (60 per second). Scenes without an asset use these defaults.
    /// </summary>
    [CreateAssetMenu(menuName = "Margin/Camera Settings", fileName = "CameraSettings")]
    public sealed class CameraSettings : ScriptableObject
    {
        [Header("Framing")]
        [Tooltip("Half the view height in units (the camera's Orthographic Size). 7 shows 14 units top to bottom. " +
                 "0 = leave the camera as it is.")]
        [Min(0f)] public float orthographicSize = 7f;
        [Tooltip("The camera aims this far from the player's center (y up shows a little more above them).")]
        public Vector2 offset = new Vector2(0f, 0.6f);

        [Header("Dead zone")]
        [Tooltip("Total width of the zone the player can move in before the camera follows sideways (spec: small).")]
        [Min(0f)] public float deadZoneWidth = 0.6f;
        [Tooltip("How far above the camera's aim the player can rise before it follows (spec: larger vertical, so " +
                 "a normal 3.2 unit jump barely moves the view).")]
        [Min(0f)] public float deadZoneUp = 2.4f;
        [Tooltip("How far below the aim the player can drop before it follows. Smaller than Up so you see where " +
                 "you're falling.")]
        [Min(0f)] public float deadZoneDown = 1.2f;

        [Header("Look-ahead")]
        [Tooltip("The camera leads the player by this much in the direction they face (spec: 0.5).")]
        [Min(0f)] public float lookAhead = 0.5f;
        [Tooltip("Frames for the lead to swing across after turning around.")]
        [Min(0f)] public float lookAheadFrames = 24f;

        [Header("Vertical")]
        [Tooltip("While the player stands on the ground, the camera eases its height back onto them (like " +
                 "platform snapping). Frames for ~63% of the way.")]
        [Min(0f)] public float groundedRecenterFrames = 10f;

        [Header("Rooms")]
        [Tooltip("Never show anything outside the current room's camera bounds.")]
        public bool confineToRoom = true;

        private static CameraSettings defaults;

        public static CameraSettings Defaults
        {
            get
            {
                if (defaults == null)
                {
                    defaults = CreateInstance<CameraSettings>();
                    defaults.hideFlags = HideFlags.DontSave;
                }
                return defaults;
            }
        }
    }
}
