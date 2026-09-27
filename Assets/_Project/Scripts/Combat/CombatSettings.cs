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

        [Header("Player (spec 6.7)")]
        [Min(1)] public int playerMaxHealth = 100;
        [Tooltip("Invulnerable frames after being hit (the player flickers).")]
        [Min(0)] public int hurtInvulnerableFrames = 45;

        [Header("Parry (spec 6.5)")]
        [Tooltip("Parry is active from its first frame (0 startup) for this many frames.")]
        [Min(1)] public int parryActiveFrames = 6;
        [Tooltip("Frames stuck in parry after the active frames if nothing was parried.")]
        [Min(0)] public int parryWhiffRecoveryFrames = 20;
        [Tooltip("Frames the parried attacker is staggered (open to punishment).")]
        [Min(0)] public int parryStaggerFrames = 40;
        [Tooltip("Whole-game freeze on a successful parry.")]
        [Min(0)] public int parryHitstopFrames = 12;
        [Min(0)] public int parryInkGain = 20;

        [Header("Redraw heal (spec 6.6)")]
        [Min(1)] public int redrawInkCost = 100;
        [Tooltip("Frames of the vulnerable heal animation. The heal lands at the end; getting hit first cancels it.")]
        [Min(1)] public int redrawFrames = 45;
        [Range(0f, 1f)] public float redrawHealFraction = 0.3f;

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
