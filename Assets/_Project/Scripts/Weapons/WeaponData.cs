using System.Collections.Generic;
using Margin.Combat;
using UnityEngine;

namespace Margin.Weapons
{
    /// <summary>
    /// A weapon's full move list (spec 7). Only moves listed as starters can begin from neutral;
    /// follow-ups (Light 2-4, and the heavy branches like L1 > Iaido) are reached through each attack's
    /// "Cancels into" list, so combos come from data. Light2-4 fields are for reference and tools.
    /// </summary>
    [CreateAssetMenu(fileName = "Weapon", menuName = "Margin/Weapon Data")]
    public sealed class WeaponData : ScriptableObject
    {
        public string displayName = "Brush Katana";

        [Header("Move list")]
        public AttackData light1;
        public AttackData light2;
        public AttackData light3;
        public AttackData light4;
        public AttackData heavy;
        public AttackData upAttack;
        public AttackData airLight;
        public AttackData airHeavy;
        public AttackData downAir;
        public AttackData special;

        [Header("Visual")]
        [Tooltip("Length of the blade line drawn from the hand, in units.")]
        [Min(0f)] public float bladeLength = 0.9f;
        [Tooltip("How the weapon is drawn (katana with guard, handle and scabbard). Visual only.")]
        public Margin.Rendering.WeaponLook look;

        /// <summary>Moves that can start from standing, running or jumping (not mid-combo follow-ups).</summary>
        public IEnumerable<AttackData> Starters()
        {
            if (light1 != null) yield return light1;
            if (heavy != null) yield return heavy;
            if (upAttack != null) yield return upAttack;
            if (airLight != null) yield return airLight;
            if (airHeavy != null) yield return airHeavy;
            if (downAir != null) yield return downAir;
            if (special != null) yield return special;
        }
    }
}
