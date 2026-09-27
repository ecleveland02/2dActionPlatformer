using System.Collections.Generic;
using Margin.Core;
using UnityEngine;

namespace Margin.Combat
{
    /// <summary>
    /// Shared hit detection (spec 6.1): tests one world hitbox against every hurtbox of the opposing faction,
    /// delivers HitInfo to each new target, and raises CombatEvents.Hit. Used by the player's sword and by
    /// projectiles, so every source of damage follows the same rules.
    /// </summary>
    public static class HitResolver
    {
        private static readonly List<Hurtbox> targets = new List<Hurtbox>();

        /// <param name="alreadyHit">Targets this attack has hit before; new targets are added to it.</param>
        /// <param name="facing">+1 right, -1 left: flips knockback.</param>
        /// <returns>Number of targets hit.</returns>
        public static int Resolve(Component attacker, Faction attackerFaction, AttackData attack, AabbBox box, int facing,
                                  HashSet<IHitReceiver> alreadyHit, CombatSettings settings)
        {
            // Copy: a hit can disable or destroy a hurtbox, which changes the live list.
            targets.Clear();
            targets.AddRange(Hurtbox.Active);

            int hits = 0;
            foreach (Hurtbox hurtbox in targets)
            {
                if (hurtbox == null || hurtbox.Faction == attackerFaction) continue;
                IHitReceiver target = hurtbox.Receiver;
                if (target == null || !target.CanBeHit || alreadyHit.Contains(target)) continue;

                AabbBox hurt = hurtbox.WorldBox;
                if (!HitboxMath.Overlaps(box, hurt)) continue;

                HitboxMath.OverlapCenter(box, hurt, out float px, out float py);
                bool global = attack.hitstopFrames >= settings.globalHitstopThreshold;
                var knockback = new Vector2(attack.knockback.x * (facing < 0 ? -1f : 1f), attack.knockback.y);
                var hit = new HitInfo(attacker, attack, knockback, new Vector2(px, py), global);

                alreadyHit.Add(target);
                target.ReceiveHit(hit);
                CombatEvents.RaiseHit(hit, target);
                hits++;
            }

            if (hits > 0 && attack.hitstopFrames >= settings.globalHitstopThreshold) GameLoop.Freeze(attack.hitstopFrames);
            return hits;
        }
    }
}
