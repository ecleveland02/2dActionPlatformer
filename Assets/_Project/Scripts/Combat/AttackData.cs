using System;
using System.Collections.Generic;
using Margin.Rendering;
using UnityEngine;

namespace Margin.Combat
{
    public enum AttackButton { Light, Heavy, Special }
    public enum AttackDirection { Neutral, Up, Down }
    public enum Faction { Player, Enemy }

    /// <summary>A hitbox rectangle, authored for a right-facing attacker. Offset is from the attacker's center.</summary>
    [Serializable]
    public struct HitboxShape
    {
        public Vector2 offset;
        public Vector2 size;
    }

    /// <summary>Hitboxes that are out during a range of attack frames. 0/0 means "all active frames".</summary>
    [Serializable]
    public sealed class HitboxWindow
    {
        [Tooltip("First attack frame (1 = the tick the attack starts). 0 = first active frame.")]
        [Min(0)] public int firstFrame;
        [Tooltip("Last attack frame. 0 = last active frame.")]
        [Min(0)] public int lastFrame;
        public List<HitboxShape> boxes = new List<HitboxShape>();
    }

    /// <summary>
    /// Everything about one move (spec 6.2). Code reads data; designers tweak data.
    /// Frame numbers: frame 1 is the tick the attack starts.
    /// </summary>
    [CreateAssetMenu(fileName = "Attack", menuName = "Margin/Attack Data")]
    public sealed class AttackData : ScriptableObject
    {
        [Header("Input")]
        [Tooltip("Which button performs this attack.")]
        public AttackButton button = AttackButton.Light;
        [Tooltip("Held direction required. Neutral attacks are the fallback when no directional attack matches.")]
        public AttackDirection direction = AttackDirection.Neutral;
        [Tooltip("Performed in the air instead of on the ground.")]
        public bool airborne;

        [Header("Frame data")]
        [Min(1)] public int startupFrames = 4;
        [Min(1)] public int activeFrames = 3;
        [Min(0)] public int recoveryFrames = 10;

        [Header("On hit")]
        [Min(0)] public int damage = 8;
        [Tooltip("Frames attacker and target freeze on hit. At or above the global threshold (CombatSettings) the whole game freezes.")]
        [Min(0)] public int hitstopFrames = 4;
        [Tooltip("Frames the target is stunned.")]
        [Min(0)] public int hitstunFrames = 18;
        [Tooltip("Velocity given to the target (units/s), for a right-facing attacker. X flips with facing.")]
        public Vector2 knockback = new Vector2(4f, 1f);
        [Tooltip("Sends the target airborne (juggle).")]
        public bool launches;
        [Tooltip("Ink meter gained on hit (Milestone 3, chunk 4).")]
        [Min(0)] public int inkGain = 5;
        [Tooltip("Can hit the same target again in each separate hitbox window.")]
        public bool multiHit;

        [Header("Cancels (spec 6.3)")]
        [Min(1)] public int cancelWindowStart = 9;
        [Min(1)] public int cancelWindowEnd = 17;
        [Tooltip("Attacks this can cancel into on hit, during the window.")]
        public List<AttackData> cancelsInto = new List<AttackData>();
        [Tooltip("A missed attack can still chain into its follow-ups, just later (see Whiff Chain Delay).")]
        public bool chainsOnWhiff = true;
        [Tooltip("Extra frames after the cancel window start before a MISSED attack can chain. 0 = same as on hit.")]
        [Min(0)] public int whiffChainDelay = 4;
        public bool cancelsIntoJump = true;
        public bool cancelsIntoDash = true;

        [Header("Movement")]
        [Tooltip("Forward speed (units/s) from Lunge First Frame through the last active frame. Ground attacks only. 0 = stand still.")]
        [Min(0f)] public float lungeSpeed;
        [Tooltip("Attack frame the lunge starts on (1 = immediately). A late lunge makes a dashing thrust.")]
        [Min(1)] public int lungeFirstFrame = 1;
        [Tooltip("Upward speed (units/s) given on Hop Frame, for leaping attacks. 0 = no hop.")]
        [Min(0f)] public float hopVelocity;
        [Tooltip("Attack frame the hop happens on (1 = immediately).")]
        [Min(1)] public int hopFrame = 1;

        [Header("Hitboxes")]
        public List<HitboxWindow> hitboxes = new List<HitboxWindow>();

        [Header("Presentation")]
        public PoseClip poseClip;
        [Tooltip("Draw a smear arc on the fastest frame (chunk 3).")]
        public bool smear = true;
        [Tooltip("Screen shake amplitude on hit, in units (chunk 3). Light 0.05, heavy 0.15.")]
        [Min(0f)] public float screenShake = 0.05f;
        public string swingSound = "swing_light";
        public string hitSound = "hit_light";

        public AttackTiming Timing =>
            new AttackTiming(startupFrames, activeFrames, recoveryFrames, cancelWindowStart, cancelWindowEnd,
                             chainsOnWhiff ? whiffChainDelay : -1);

        public int TotalFrames => startupFrames + activeFrames + recoveryFrames;

        /// <summary>The hitbox windows covering this attack frame (empty outside active hitbox frames).</summary>
        public IEnumerable<HitboxWindow> WindowsAt(int frame)
        {
            AttackTiming t = Timing;
            foreach (HitboxWindow w in hitboxes)
            {
                int first = w.firstFrame > 0 ? w.firstFrame : t.FirstActiveFrame;
                int last = w.lastFrame > 0 ? w.lastFrame : t.LastActiveFrame;
                if (frame >= first && frame <= last) yield return w;
            }
        }

        private void OnValidate()
        {
            if (cancelWindowEnd < cancelWindowStart) cancelWindowEnd = cancelWindowStart;
        }
    }
}
