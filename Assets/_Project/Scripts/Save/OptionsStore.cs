using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Margin.Save
{
    /// <summary>
    /// Loads, applies and saves the player's options (spec 14, 15): JSON at persistentDataPath/options.json.
    /// Audio and screen shake read <see cref="Current"/> every frame; display settings (window mode, resolution,
    /// vsync) are pushed to Unity by <see cref="ApplyDisplay"/> when they change and when the game starts.
    /// </summary>
    public static class OptionsStore
    {
        private const string FileName = "options.json";
        private static OptionsData current;

        /// <summary>Raised after any option changes (the UI calls NotifyChanged).</summary>
        public static event Action Changed;

        public static string FilePath => Path.Combine(Application.persistentDataPath, FileName);

        /// <summary>The options in use (loaded from disk the first time it's read).</summary>
        public static OptionsData Current
        {
            get
            {
                if (current == null) current = Load();
                return current;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void ApplyAtStartup()
        {
            current = Load();
            ApplyDisplay();
        }

        public static OptionsData Load()
        {
            var data = new OptionsData();
            try
            {
                if (File.Exists(FilePath)) JsonUtility.FromJsonOverwrite(File.ReadAllText(FilePath), data);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"Couldn't read {FilePath} ({e.Message}); using default options.");
                data = new OptionsData();
            }
            data.Clamp();
            return data;
        }

        public static void Save()
        {
            try
            {
                File.WriteAllText(FilePath, JsonUtility.ToJson(Current, true));
            }
            catch (Exception e)
            {
                Debug.LogWarning($"Couldn't save options to {FilePath}: {e.Message}");
            }
        }

        /// <summary>Call after changing a value: clamps it, applies display changes and tells listeners.</summary>
        public static void NotifyChanged(bool displayChanged = false)
        {
            Current.Clamp();
            if (displayChanged) ApplyDisplay();
            Changed?.Invoke();
        }

        /// <summary>Window mode, resolution and vsync. (The editor's Game view ignores mode and resolution.)</summary>
        public static void ApplyDisplay()
        {
            OptionsData o = Current;
            QualitySettings.vSyncCount = o.vsync ? 1 : 0;
            Application.targetFrameRate = -1;   // spec 2: uncapped; vsync (if on) paces the frames
#if !UNITY_EDITOR
            int width = o.resolutionWidth > 0 ? o.resolutionWidth : Screen.currentResolution.width;
            int height = o.resolutionHeight > 0 ? o.resolutionHeight : Screen.currentResolution.height;
            Screen.SetResolution(width, height, ToUnity(o.displayMode));
#endif
        }

        public static FullScreenMode ToUnity(DisplayMode mode)
        {
            switch (mode)
            {
                case DisplayMode.Fullscreen: return FullScreenMode.ExclusiveFullScreen;
                case DisplayMode.Windowed: return FullScreenMode.Windowed;
                default: return FullScreenMode.FullScreenWindow;
            }
        }

        /// <summary>The resolutions to offer: the monitor's list, each size once, smallest first (0 x 0 = native first).</summary>
        public static List<Vector2Int> Resolutions()
        {
            var list = new List<Vector2Int> { Vector2Int.zero };
            foreach (Resolution r in Screen.resolutions)
            {
                var size = new Vector2Int(r.width, r.height);
                if (!list.Contains(size)) list.Add(size);
            }
            OptionsData o = Current;
            var saved = new Vector2Int(o.resolutionWidth, o.resolutionHeight);
            if (!list.Contains(saved)) list.Add(saved);
            return list;
        }
    }
}
