using Margin.Combat;
using UnityEngine;

namespace Margin.FX
{
    /// <summary>
    /// Screen shake on hits (spec 6.4, 12). Amplitude comes from each attack's Screen Shake value
    /// (light 0.05, heavy 0.15), scaled by FeelSettings.screenShakeScale (accessibility, 0 to 100%).
    /// Runs in real time so it keeps shaking during hitstop freezes. Replaced by Cinemachine Impulse in M5.
    /// Runs after other camera scripts and only adds an offset on top of wherever they put the camera.
    /// </summary>
    [DefaultExecutionOrder(1000)]
    public sealed class CameraShake : MonoBehaviour
    {
        [SerializeField] private FeelSettings settings;

        private static CameraShake instance;
        private float amplitude;
        private float remaining;
        private Vector3 applied;
        private Vector3 lastWritten;

        private FeelSettings Settings => settings != null ? settings : FeelSettings.Defaults;
        public Vector3 CurrentOffset => applied;

        public FeelSettings FeelSettings
        {
            get => settings;
            set => settings = value;
        }

        /// <summary>Shakes the active camera. Stronger shakes override weaker ones in progress.</summary>
        public static void Shake(float strength)
        {
            if (instance != null) instance.Add(strength);
        }

        private void OnEnable()
        {
            instance = this;
            CombatEvents.Hit += OnHit;
        }

        private void OnDisable()
        {
            CombatEvents.Hit -= OnHit;
            if (instance == this) instance = null;
            transform.position -= applied;
            applied = Vector3.zero;
        }

        private void OnHit(HitInfo hit, IHitReceiver target) => Add(hit.Attack.screenShake);

        private void Add(float strength)
        {
            strength *= Settings.screenShakeScale * Margin.Save.OptionsStore.Current.screenShake;   // Options slider
            if (strength <= 0f) return;
            // Keep whichever shake is currently stronger, and restart the timer.
            float current = amplitude * FeelMath.ShakeFalloff(remaining, Settings.shakeFrames);
            amplitude = Mathf.Max(strength, current);
            remaining = Settings.shakeFrames;
        }

        private void LateUpdate()
        {
            // If another script (camera follow) moved the camera this frame, that's the new base position.
            // Otherwise remove last frame's offset to get back to the base.
            Vector3 basePosition = transform.position == lastWritten ? transform.position - applied : transform.position;

            if (remaining > 0f)
            {
                // Draw the current strength first, then count down, so even a long frame shows the hit.
                float strength = amplitude * FeelMath.ShakeFalloff(remaining, Settings.shakeFrames);
                Vector2 dir = Random.insideUnitCircle;
                applied = new Vector3(dir.x, dir.y, 0f) * strength;
                remaining -= Time.unscaledDeltaTime * 60f;   // frames of real time
            }
            else
            {
                applied = Vector3.zero;
            }

            transform.position = basePosition + applied;
            lastWritten = transform.position;
        }
    }
}
