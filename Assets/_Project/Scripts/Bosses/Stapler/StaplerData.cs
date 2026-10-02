using UnityEngine;

namespace Margin.Bosses
{
    /// <summary>
    /// The Stapler Titan's own numbers (spec 10, Boss 2: tests positioning and punishing openings). Frame data,
    /// damage and parryability of each move live in its AttackData (Data/Bosses/Stapler); this holds speeds and
    /// sizes. Distances in units, speeds in units per second, times in frames.
    /// </summary>
    [CreateAssetMenu(menuName = "Margin/Bosses/Stapler Titan Data", fileName = "StaplerTitanData")]
    public sealed class StaplerData : BossData
    {
        [Header("Chomp (parryable)")]
        [Tooltip("Forward speed while the jaw snaps shut.")]
        [Min(0f)] public float chompLunge = 9f;

        [Header("Hop Slam (can't be parried: get out from under it, then jump the shockwaves)")]
        [Tooltip("Frames in the air. The landing spot is where the player stood when it jumped.")]
        [Min(10)] public int hopAirFrames = 40;
        [Tooltip("How high the hop peaks (units).")]
        [Min(0.5f)] public float hopHeight = 4.5f;
        [Tooltip("Longest hop (units).")]
        [Min(0f)] public float hopMaxDistance = 12f;
        [Tooltip("The two shockwaves the landing sends along the floor.")]
        [Min(0f)] public float waveSpeed = 11f;
        [Min(0.1f)] public float waveHeight = 0.8f;
        [Tooltip("Taller waves in phase 2.")]
        [Min(0.1f)] public float waveHeightPhase2 = 1.15f;
        [Min(1)] public int waveLifetimeFrames = 120;

        [Header("Staple Shot (can't be parried: move or dash through)")]
        [Min(1)] public int shotCount = 3;
        [Tooltip("Frames between staples. Each one aims at the player when it's fired.")]
        [Min(1)] public int shotInterval = 12;
        [Min(1f)] public float shotSpeed = 15f;

        [Header("Phase 2: Staple Rain (can't be parried: step out of the lanes or shelter under a stapled platform)")]
        [Min(1)] public int rainCount = 5;
        [Tooltip("Minimum gap between lanes.")]
        [Min(0.5f)] public float rainSpacing = 3f;
        [Tooltip("Frames between drops (one lane after another).")]
        [Min(1)] public int rainInterval = 8;
        [Min(1f)] public float rainSpeed = 16f;

        [Header("Phase 2 change (the platforms)")]
        [Tooltip("Fraction of the phase change when the kept platforms get stapled in place.")]
        [Range(0f, 1f)] public float pinAt = 0.3f;
        [Tooltip("Fraction of the phase change when the others are torn out (they flicker from Pin At until then).")]
        [Range(0f, 1f)] public float removeAt = 0.65f;
    }
}
