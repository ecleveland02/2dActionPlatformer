using UnityEngine;

namespace Margin.Physics
{
    /// <summary>
    /// Collision tuning shared by every KinematicBody2D (player now, enemies later).
    /// Create via Assets > Create > Margin > Kinematic Body Data.
    /// </summary>
    [CreateAssetMenu(fileName = "KinematicBodyData", menuName = "Margin/Kinematic Body Data")]
    public sealed class KinematicBodyData : ScriptableObject
    {
        [Tooltip("Casts start this far inside the box so the body never starts a cast already touching a surface.")]
        [Min(0.001f)] public float skinWidth = 0.015f;

        [Tooltip("Steepest ground, in degrees, that counts as floor. Steeper surfaces act as walls.")]
        [Range(0f, 89f)] public float maxSlopeAngle = 45f;

        [Tooltip("When walking over a slope crest or small step down, pull the body down this far to stay grounded.")]
        [Min(0f)] public float groundSnapDistance = 0.3f;

        [Tooltip("Layers that block from every side.")]
        public LayerMask solidMask = 1 << 6;      // "Ground"

        [Tooltip("Layers you can jump up through and stand on (and drop through with Down + Jump).")]
        public LayerMask oneWayMask = 1 << 7;     // "OneWayPlatform"
    }
}
