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
