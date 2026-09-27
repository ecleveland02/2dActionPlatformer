using System;
using System.Collections.Generic;
using Margin.Combat;
using UnityEngine;

namespace Margin.Bosses
{
    /// <summary>One entry in a phase's attack pool.</summary>
    [Serializable]
    public struct BossMoveEntry
    {
        [Tooltip("Which move this is (the boss script knows its moves by name, e.g. \"Swipe\").")]
        public string move;
        [Tooltip("Frame data, damage, knockback and parryability. Startup is the telegraph (spec 9: at least 12 frames).")]
        public AttackData attack;
        [Tooltip("How often it's picked compared with the others.")]
        [Min(0)] public int weight;
        [Tooltip("Only picked when the player is at least this far away (units).")]
        [Min(0f)] public float minRange;
        [Tooltip("Only picked when the player is at most this far away. 0 = any distance.")]
        [Min(0f)] public float maxRange;
        [Tooltip("Sound played as the telegraph starts (a SoundBank id, e.g. boss_swipe_tell). Empty = the boss's default.")]
        public string tellSound;
    }

    /// <summary>
    /// A boss phase (spec 10): its attack pool, when it starts, how long the boss rests between attacks (the
    /// openings), and the phase-change sequence (full length the first time it's seen, short after that).
    /// </summary>
    [CreateAssetMenu(menuName = "Margin/Boss Phase", fileName = "BossPhase")]
    public sealed class BossPhase : ScriptableObject
    {
        [Tooltip("The phase starts when health falls to this fraction (1 = from the start, 0.5 = at half).")]
        [Range(0f, 1f)] public float healthThreshold = 1f;
        public List<BossMoveEntry> moves = new List<BossMoveEntry>();

        [Header("Pacing")]
        [Tooltip("Frames the boss rests between attacks (random in this range). This is when to hit it.")]
        [Min(0)] public int restFramesMin = 30;
        [Min(0)] public int restFramesMax = 50;

        [Header("Phase change (entering this phase)")]
        [Tooltip("Length of the phase-change sequence the first time. The boss can't be hurt meanwhile.")]
        [Min(0)] public int transitionFrames = 150;
        [Tooltip("Length after it has been seen once (spec 10: skippable after the first view).")]
        [Min(0)] public int transitionFramesRepeat = 45;
    }
}
