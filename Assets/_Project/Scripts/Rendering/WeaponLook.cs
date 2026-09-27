using UnityEngine;

namespace Margin.Rendering
{
    public enum WeaponStyle { Plain, Katana, Pencil }

    /// <summary>
    /// How a held weapon is drawn (WeaponLine). Visual only: the weapon's reach is its length (WeaponData
    /// bladeLength / EnemyData weaponLength), and the tip always stays there whatever the look.
    /// Create via Assets > Create > Margin > Weapon Look.
    /// </summary>
    [CreateAssetMenu(fileName = "WeaponLook", menuName = "Margin/Weapon Look")]
    public sealed class WeaponLook : ScriptableObject
    {
        public WeaponStyle style = WeaponStyle.Katana;

        [Header("Katana")]
        [Tooltip("Blade thickness at the guard (units). It stays full until Taper Start, then narrows to the point.")]
        [Min(0.005f)] public float bladeWidth = 0.065f;
        [Tooltip("Fraction of the blade length where the point starts to narrow.")]
        [Range(0.1f, 0.95f)] public float taperStart = 0.75f;
        [Tooltip("How far the blade bows toward its back (units), most at 60% of its length. 0 = straight.")]
        [Min(0f)] public float curve = 0.045f;
        [Tooltip("Guard (tsuba): length across the blade and thickness.")]
        [Min(0f)] public float guardLength = 0.13f;
        [Min(0.005f)] public float guardWidth = 0.035f;
        [Tooltip("Handle behind the fist: length and thickness.")]
        [Min(0f)] public float handleLength = 0.22f;
        [Min(0.005f)] public float handleWidth = 0.045f;

        [Header("Pencil")]
        [Tooltip("How far the pencil sticks out behind the hand (eraser end).")]
        [Min(0f)] public float pencilBackLength = 0.3f;
        [Min(0.01f)] public float pencilWidth = 0.1f;
        [Tooltip("Sharpened cone length at the tip.")]
        [Min(0.01f)] public float coneLength = 0.22f;
        [Min(0.01f)] public float eraserLength = 0.1f;
        [Tooltip("Outline thickness of the pencil lines.")]
        [Min(0.005f)] public float pencilLineWidth = 0.025f;
    }
}
