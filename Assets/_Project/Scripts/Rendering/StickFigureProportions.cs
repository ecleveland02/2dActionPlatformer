using UnityEngine;

namespace Margin.Rendering
{
    /// <summary>
    /// Bone lengths and ink style for a stick figure (spec 4.1). Default values fit the 1.8-unit player:
    /// feet 0.9 below the figure's origin, head top about 0.89 above it.
    /// Create via Assets > Create > Margin > Stick Figure Proportions.
    /// </summary>
    [CreateAssetMenu(fileName = "StickFigureProportions", menuName = "Margin/Stick Figure Proportions")]
    public sealed class StickFigureProportions : ScriptableObject
    {
        [Header("Bones (units)")]
        [Tooltip("How far below the figure's origin the feet are in the neutral pose (half the player height).")]
        [Min(0f)] public float feetBelowOrigin = 0.9f;
        [Min(0.01f)] public float thigh = 0.40f;
        [Min(0.01f)] public float shin = 0.40f;
        [Min(0.01f)] public float spine = 0.55f;
        [Min(0.01f)] public float neck = 0.10f;
        [Min(0.01f)] public float headRadius = 0.17f;
        [Min(0.01f)] public float upperArm = 0.30f;
        [Min(0.01f)] public float forearm = 0.28f;

        [Header("Ink")]
        [Min(0.001f)] public float lineWidth = 0.06f;
        [Tooltip("Width along each limb (0 = joint it starts at, 1 = end). A slight dip looks hand-inked.")]
        public AnimationCurve widthVariation = new AnimationCurve(
            new Keyframe(0f, 1f), new Keyframe(0.5f, 0.82f), new Keyframe(1f, 0.95f));
        public Color inkColor = new Color32(0x1A, 0x1A, 0x1A, 0xFF);
        [Tooltip("The far arm and leg are drawn lighter so the body reads clearly.")]
        public Color backLimbColor = new Color32(0x7A, 0x7A, 0x7A, 0xFF);
        [Min(3)] public int headSegments = 20;
        [Tooltip("Sorting order of the body. Back limbs draw at this - 1, front limbs at this + 1.")]
        public int sortingOrder = 10;
    }
}
