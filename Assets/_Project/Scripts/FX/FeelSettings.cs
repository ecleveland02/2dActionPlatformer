using UnityEngine;

namespace Margin.FX
{
    /// <summary>
    /// Tuning for game-feel effects (spec 4.3, 6.4, 12). Create via Assets > Create > Margin > Feel Settings,
    /// or let Margin > Build Movement Gym / Wire Player References create Data/FeelSettings.
    /// Effects without an assigned asset use these defaults.
    /// </summary>
    [CreateAssetMenu(fileName = "FeelSettings", menuName = "Margin/Feel Settings")]
    public sealed class FeelSettings : ScriptableObject
    {
        [Header("Screen shake")]
        [Tooltip("Accessibility: 0 = no screen shake, 1 = full (spec 12). Will move to the Options menu in Milestone 6.")]
        [Range(0f, 1f)] public float screenShakeScale = 1f;
        [Tooltip("How long a hit's shake lasts, in frames. Amplitude comes from each attack's Screen Shake value.")]
        [Min(1)] public int shakeFrames = 12;

        [Header("Smear (spec 4.3)")]
        [Tooltip("The blade must turn at least this many degrees in one tick to draw a smear (the fastest frames).")]
        [Range(1f, 180f)] public float smearMinAngle = 25f;
        [Tooltip("Frames a smear stays visible (spec: 1 to 3).")]
        [Range(1, 6)] public int smearFrames = 3;
        [Tooltip("How thick the crescent gets at its leading edge: inner radius as a fraction of the outer radius.")]
        [Range(0.05f, 0.9f)] public float smearThickness = 0.35f;
        [Range(0f, 1f)] public float smearAlpha = 0.85f;

        [Header("Blade trail (spec 4.3)")]
        [Tooltip("Seconds the trail lingers (spec: 0.08 to 0.15).")]
        [Range(0.02f, 0.4f)] public float trailTime = 0.12f;
        [Min(0.001f)] public float trailWidth = 0.06f;

        [Header("Ink splatter (spec 4.3)")]
        [Min(0)] public int splatterBase = 8;
        [Tooltip("Extra droplets per point of damage.")]
        [Min(0f)] public float splatterPerDamage = 0.6f;
        [Tooltip("Spread of the burst around the knockback direction, in degrees.")]
        [Range(0f, 180f)] public float splatterSpread = 40f;
        public Vector2 splatterSpeed = new Vector2(3f, 10f);
        public Vector2 splatterSize = new Vector2(0.03f, 0.09f);
        public Vector2 splatterLifetime = new Vector2(0.3f, 0.6f);
        [Min(0f)] public float splatterGravity = 1.5f;

        [Header("Dash afterimages (spec 4.3)")]
        [Range(1, 8)] public int afterimageCount = 4;
        [Tooltip("Frames between afterimages during a dash.")]
        [Min(1)] public int afterimageInterval = 3;
        [Tooltip("Frames an afterimage takes to fade out.")]
        [Min(1)] public int afterimageFadeFrames = 12;
        [Range(0f, 1f)] public float afterimageAlpha = 0.45f;

        private static FeelSettings defaults;

        public static FeelSettings Defaults
        {
            get
            {
                if (defaults == null)
                {
                    defaults = CreateInstance<FeelSettings>();
                    defaults.hideFlags = HideFlags.DontSave;
                }
                return defaults;
            }
        }
    }
}
