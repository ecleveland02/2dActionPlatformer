using UnityEngine;

namespace Margin.Bosses
{
    /// <summary>
    /// The Highlighter's own numbers (spec 10, Boss 1: tests parrying and reaction). Frame data, damage and
    /// parryability of each move live in its AttackData (Data/Bosses/Highlighter); this holds speeds and sizes.
    /// Distances in units, speeds in units per second, times in frames.
    /// </summary>
    [CreateAssetMenu(menuName = "Margin/Bosses/Highlighter Data", fileName = "HighlighterData")]
    public sealed class HighlighterData : BossData
    {
        [Header("Phase 1: Swipe (parryable)")]
        [Tooltip("Forward speed while the swipe is out.")]
        [Min(0f)] public float swipeLunge = 7f;

        [Header("Phase 1: Dash Stroke (can't be parried: jump it or dash through)")]
        [Min(0f)] public float dashSpeed = 22f;
        [Tooltip("Frames the highlighted streak on the floor takes to fade after the dash.")]
        [Min(1)] public int dashTrailFrames = 45;

        [Header("Cap Toss (parry it back at the boss)")]
        [Tooltip("Frames for the thrown cap to reach where the player was.")]
        [Min(10)] public int capFlightFrames = 40;
        [Min(0f)] public float capGravity = 30f;
        [Tooltip("Frames a missed cap lies on the floor before flying home.")]
        [Min(0)] public int capReturnDelay = 30;
        [Tooltip("Damage when a parried cap hits the boss (it also staggers for Parry Stagger Frames).")]
        [Min(0)] public int capReflectDamage = 45;
        [Min(1f)] public float capReflectSpeed = 26f;

        [Header("Phase 2: flight and flood")]
        [Tooltip("How high the boss's feet hover above the arena floor between attacks.")]
        [Min(0f)] public float hoverHeight = 6.8f;
        [Tooltip("How far to the side of the player it likes to hover.")]
        [Min(0f)] public float hoverOffset = 4f;
        [Min(0f)] public float flySpeed = 7f;
        [Tooltip("Frames for the glowing ink to flood the floor during the phase change.")]
        [Min(1)] public int floodRiseFrames = 90;

        [Header("Phase 2: Line Sweep (parryable)")]
        [Min(0f)] public float sweepSpeed = 24f;

        [Header("Phase 2: Drip Rain (can't be parried: step out of the lanes)")]
        [Min(1)] public int dripCount = 4;
        [Min(1)] public int dripInterval = 10;
        [Min(1f)] public float dripSpeed = 14f;
    }
}
