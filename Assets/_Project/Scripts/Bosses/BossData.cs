using System.Collections.Generic;
using UnityEngine;

namespace Margin.Bosses
{
    /// <summary>
    /// Everything tunable about a boss (spec 10): health, phases (each with its attack pool), and timings.
    /// Boss-specific numbers live in a subclass (e.g. HighlighterData). Times are in frames (60 per second).
    /// </summary>
    [CreateAssetMenu(menuName = "Margin/Boss Data", fileName = "BossData")]
    public class BossData : ScriptableObject
    {
        [Tooltip("Shown on the boss health bar.")]
        public string bossName = "BOSS";
        [Min(1)] public int maxHealth = 600;
        [Tooltip("In order. The first phase's threshold should be 1.")]
        public List<BossPhase> phases = new List<BossPhase>();

        [Header("Timings")]
        [Tooltip("The entrance the first time the fight starts (title card, can't be hurt).")]
        [Min(0)] public int introFrames = 120;
        [Tooltip("The entrance on a retry (spec 10: instant retry, under 3 seconds from death to fight).")]
        [Min(0)] public int introFramesRepeat = 30;
        [Tooltip("Stunned after its attack is parried: the big punish window.")]
        [Min(0)] public int parryStaggerFrames = 70;
        [Tooltip("The defeat animation before it vanishes and the arena opens.")]
        [Min(1)] public int defeatFrames = 150;

        [Header("Body")]
        [Tooltip("Hurtbox size standing up (units).")]
        public Vector2 bodySize = new Vector2(1.3f, 3f);
        [Tooltip("Walking speed between attacks (units per second).")]
        [Min(0f)] public float walkSpeed = 3.5f;
        [Tooltip("Frames a hit freezes the boss (its own hitstop, when the whole game isn't frozen).")]
        [Min(0)] public int hitstopOnHurt = 4;
    }
}
