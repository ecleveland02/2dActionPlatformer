using UnityEngine;

namespace Margin.Combat
{
    /// <summary>Global combat feel rules (spec 6.4). Create via Assets > Create > Margin > Combat Settings.</summary>
    [CreateAssetMenu(fileName = "CombatSettings", menuName = "Margin/Combat Settings")]
    public sealed class CombatSettings : ScriptableObject
    {
        [Tooltip("Hits with at least this much hitstop freeze the whole game instead of just attacker and target.")]
        [Min(1)] public int globalHitstopThreshold = 8;
        [Tooltip("How far (units) a target shakes side to side during hitstop.")]
        [Min(0f)] public float hitShakeDistance = 0.05f;
        [Tooltip("Airborne targets in hitstun fall with gravity multiplied by this, so air combos can keep them up.")]
        [Range(0.1f, 1f)] public float juggleGravityScale = 0.55f;
        [Tooltip("A target landing faster than this (units/s, downward) after being spiked counts as a slam impact.")]
        [Min(0f)] public float slamImpactSpeed = 12f;

        [Header("Ink meter (spec 6.6)")]
        [Min(1)] public int inkMax = 100;
        [Tooltip("Frames without landing a hit before ink starts draining (spec: 5 s).")]
        [Min(0)] public int inkDecayDelayFrames = 300;
        [Tooltip("Frames per point of ink drained (spec: 1 per second).")]
        [Min(1)] public int inkDecayIntervalFrames = 60;

        private static CombatSettings defaults;

        /// <summary>Default values, used when no asset is assigned.</summary>
        public static CombatSettings Defaults
        {
            get
            {
                if (defaults == null)
                {
                    defaults = CreateInstance<CombatSettings>();
                    defaults.hideFlags = HideFlags.DontSave;
                }
                return defaults;
            }
        }
    }
}
