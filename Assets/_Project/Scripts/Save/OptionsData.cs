using System;

namespace Margin.Save
{
    /// <summary>Window modes offered in Options (mapped to Unity's FullScreenMode by OptionsStore).</summary>
    public enum DisplayMode { Borderless = 0, Fullscreen = 1, Windowed = 2 }

    /// <summary>
    /// The player's options (spec 14: audio, screen shake, display), as plain data so it can be unit tested and
    /// saved as JSON. Volumes and screen shake are 0..1. Resolution 0 x 0 = the monitor's own resolution.
    /// Options belong to the player, not to a save slot, so they live in their own file (options.json).
    /// </summary>
    [Serializable]
    public sealed class OptionsData
    {
        public float masterVolume = 1f;
        public float musicVolume = 0.8f;
        public float sfxVolume = 1f;
        /// <summary>Accessibility (spec 12): 0 = no screen shake, 1 = full.</summary>
        public float screenShake = 1f;
        public DisplayMode displayMode = DisplayMode.Borderless;
        public int resolutionWidth;
        public int resolutionHeight;
        public bool vsync = true;
        /// <summary>Rebound keys and buttons: the Input System's override JSON (empty = MarginControls as shipped).</summary>
        public string bindingOverrides = "";

        /// <summary>Keeps every value in range (a hand-edited or old file can't break the game).</summary>
        public void Clamp()
        {
            masterVolume = Clamp01(masterVolume);
            musicVolume = Clamp01(musicVolume);
            sfxVolume = Clamp01(sfxVolume);
            screenShake = Clamp01(screenShake);
            if (!Enum.IsDefined(typeof(DisplayMode), displayMode)) displayMode = DisplayMode.Borderless;
            if (resolutionWidth < 0 || resolutionHeight < 0) resolutionWidth = resolutionHeight = 0;
            if (bindingOverrides == null) bindingOverrides = "";
        }

        public OptionsData Copy() => (OptionsData)MemberwiseClone();

        /// <summary>
        /// Moves a 0..1 value one notch (Left/Right on a slider): steps of <paramref name="step"/>, landing exactly on
        /// the notches (0.7 + 0.1 is 0.8, not 0.79999), never past 0 or 1.
        /// </summary>
        public static float Step(float value, int direction, float step = 0.1f)
        {
            if (direction == 0 || step <= 0f) return Clamp01(value);
            double notches = Math.Round(value / step) + Math.Sign(direction);
            return Clamp01((float)(notches * step));
        }

        /// <summary>Moves through a list of choices, wrapping at both ends.</summary>
        public static int Cycle(int index, int direction, int count)
        {
            if (count <= 0) return 0;
            int next = (index + Math.Sign(direction)) % count;
            return next < 0 ? next + count : next;
        }

        /// <summary>A 0..1 value as a percent label, e.g. "70%".</summary>
        public static string Percent(float value) => (int)Math.Round(Clamp01(value) * 100f) + "%";

        private static float Clamp01(float v) => float.IsNaN(v) ? 1f : Math.Max(0f, Math.Min(1f, v));
    }
}
