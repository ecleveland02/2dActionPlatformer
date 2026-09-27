using UnityEngine;

namespace Margin.Combat
{
    /// <summary>Everything a target needs to know about a hit it received (spec: DamageInfo).</summary>
    public readonly struct HitInfo
    {
        public readonly Component Attacker;
        public readonly AttackData Attack;
        public readonly int Damage;
        /// <summary>Knockback velocity (units/s) already flipped for the attacker's facing.</summary>
        public readonly Vector2 Knockback;
        public readonly int HitstunFrames;
        public readonly int HitstopFrames;
        /// <summary>Center of the hitbox/hurtbox overlap; where hit effects spawn.</summary>
        public readonly Vector2 Point;
        /// <summary>True when this hit froze the whole game (heavy hit), so the target should not add its own freeze.</summary>
        public readonly bool GlobalHitstop;

        public HitInfo(Component attacker, AttackData attack, Vector2 knockback, Vector2 point, bool globalHitstop)
        {
            Attacker = attacker;
            Attack = attack;
            Damage = attack.damage;
            Knockback = knockback;
            HitstunFrames = attack.hitstunFrames;
            HitstopFrames = attack.hitstopFrames;
            Point = point;
            GlobalHitstop = globalHitstop;
        }
    }

    /// <summary>Something that can be hit: a training dummy, an enemy, or the player.</summary>
    public interface IHitReceiver
    {
        /// <summary>False while invulnerable (dash i-frames, respawn, already dead).</summary>
        bool CanBeHit { get; }

        /// <summary>Handles a hit. Returns false if it didn't land (e.g. it was parried), so no hit effects play.</summary>
        bool ReceiveHit(in HitInfo hit);
    }

    /// <summary>An attacker that reacts to being parried (spec 6.5: it staggers).</summary>
    public interface IParryable
    {
        void OnParried(in HitInfo hit, int staggerFrames);
    }

    /// <summary>Static combat events (spec 3.2 event bus). Listeners must unsubscribe when destroyed.</summary>
    public static class CombatEvents
    {
        /// <summary>Raised for every landed hit, after the target received it.</summary>
        public static event System.Action<HitInfo, IHitReceiver> Hit;

        public static void RaiseHit(in HitInfo hit, IHitReceiver target) => Hit?.Invoke(hit, target);

        /// <summary>Raised when the player parries an attack.</summary>
        public static event System.Action<HitInfo> Parry;

        public static void RaiseParry(in HitInfo hit) => Parry?.Invoke(hit);
    }
}
