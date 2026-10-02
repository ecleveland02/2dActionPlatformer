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
        [Tooltip("Invulnerable frames after a combo on the player ends (the player flickers). Hits during hitstun " +
                 "still land, so enemies can combo; this stops a fresh combo starting right away.")]
        [Min(0)] public int hurtInvulnerableFrames = 45;

        [Tooltip("Frames of the defeat animation before respawning (spec 10: retry in under 3 seconds).")]
        [Min(1)] public int playerDeathFrames = 90;
        [Tooltip("Invulnerable frames after respawning, so nothing hits you on arrival.")]
        [Min(0)] public int respawnInvulnerableFrames = 60;
        [Tooltip("Each later hit of a combo on the player does this much less damage (0.15 = 100%, 85%, 70%...).")]
        [Range(0f, 1f)] public float comboDamageStep = 0.15f;
        [Tooltip("Combo damage never scales below this fraction.")]
        [Range(0.1f, 1f)] public float comboDamageFloor = 0.5f;

        [Header("Enemy knockdown")]
        [Tooltip("An enemy hit with at least this much vertical knockback (up or down, units/s) is 'hit hard' and " +
                 "is knocked down when it lands. Heavy hits (global hitstop) on an airborne enemy count too.")]
        [Min(0f)] public float knockdownLaunchSpeed = 8f;
        [Tooltip("Frames lying on the ground.")]
        [Min(1)] public int knockdownFrames = 45;
        [Tooltip("Frames of the get-up animation afterwards.")]
        [Min(1)] public int getUpFrames = 24;
        [Tooltip("Knocked-down enemies can't be hit (no juggling them along the floor).")]
        public bool knockdownInvulnerable = true;

        [Header("Enemies (spec 9)")]
        [Tooltip("Max enemies attacking the player at once (attack slots). 0 = no limit: every enemy that has " +
                 "noticed the player may attack. Set 2 or 3 if groups turn into unavoidable lock-downs.")]
        [Min(0)] public int maxEnemyAttackers = 0;

        [Header("Combo breaker")]
        [Tooltip("Parry pressed during hitstun with this much ink: break out of the combo.")]
        [Min(0)] public int comboBreakerInkCost = 50;
        [Tooltip("Frames of the breaker burst. The player can't act or be hit during it.")]
        [Min(1)] public int comboBreakerFrames = 14;
        [Tooltip("Size of the push area (units), centered on the player.")]
        public Vector2 comboBreakerSize = new Vector2(3.2f, 2.4f);
        [Tooltip("Knockback given to enemies caught in the burst (x is away from the player).")]
        public Vector2 comboBreakerKnockback = new Vector2(8f, 4f);
        [Tooltip("Frames enemies are stunned by the burst (cancels their attack).")]
        [Min(0)] public int comboBreakerStunFrames = 30;
        [Tooltip("Whole-game freeze when the burst goes off.")]
        [Min(0)] public int comboBreakerHitstopFrames = 6;

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

        [Header("Double jump spring (spec 8)")]
        [Tooltip("The Spring Doodle's hit on whatever is under your feet when you double jump.")]
        [Min(0)] public int springDamage = 6;
        [Min(0)] public int springHitstunFrames = 18;
        [Tooltip("Knockback given to what's below (x flips with facing): down and away.")]
        public Vector2 springKnockback = new Vector2(3f, -8f);

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
