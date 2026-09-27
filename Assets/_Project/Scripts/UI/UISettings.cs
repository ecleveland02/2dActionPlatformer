using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Margin.UI
{
    /// <summary>
    /// Look and timing of the HUD and menus (spec 14: hand-drawn boxes, uneven lines, paper background).
    /// Sizes are in reference pixels at 1920x1080; the UI scales with the screen. Times are in frames (60 per second).
    /// Created by Margin > Build Movement Gym / Build Enemy Arena at Data/UI/UISettings.asset.
    /// </summary>
    [CreateAssetMenu(menuName = "Margin/UI Settings", fileName = "UISettings")]
    public sealed class UISettings : ScriptableObject
    {
        [Header("Colors")]
        public Color paper = new Color32(0xF2, 0xEE, 0xE3, 0xF0);
        public Color ink = new Color32(0x1A, 0x1A, 0x1A, 0xFF);
        [Tooltip("Empty part of a bar and small labels.")]
        public Color faint = new Color32(0xB9, 0xB4, 0xA8, 0xFF);
        [Tooltip("Damage chips, low health and warnings: a red ink.")]
        public Color accent = new Color32(0xB3, 0x26, 0x1E, 0xFF);
        [Tooltip("Dims the game behind the pause menu.")]
        public Color pauseDim = new Color32(0xF2, 0xEE, 0xE3, 0xB0);

        [Header("Hand-drawn lines")]
        [Tooltip("How far lines wander from straight, in pixels.")]
        [Min(0f)] public float wobble = 1.4f;
        [Tooltip("How far box lines run past their corners.")]
        [Min(0f)] public float overshoot = 6f;
        [Tooltip("Distance between wobble points along a line.")]
        [Min(2f)] public float wobbleStep = 14f;
        [Min(0.5f)] public float lineWidth = 2.6f;
        [Tooltip("Pen pressure: how much the line width varies (0.25 = +-25%).")]
        [Range(0f, 0.6f)] public float lineWidthVariation = 0.3f;
        [Tooltip("Redraw the wobble every few frames, like hand-drawn animation. Off = lines never move.")]
        public bool boil = true;
        [Tooltip("Frames between boils (8 = 7.5 times a second).")]
        [Min(1)] public int boilFrames = 8;

        [Header("HUD")]
        [Tooltip("Gap from the screen's top-left corner.")]
        public Vector2 hudMargin = new Vector2(32f, 28f);
        [Min(80f)] public float barWidth = 340f;
        [Min(4f)] public float healthBarHeight = 20f;
        [Min(4f)] public float inkBarHeight = 13f;
        [Min(8)] public int labelSize = 18;
        [Tooltip("Frames the damage chip holds before draining.")]
        [Min(0)] public int chipHoldFrames = 30;
        [Tooltip("How much of the bar the chip drains per frame (0.012 = the whole bar in ~1.4 s).")]
        [Min(0.001f)] public float chipDrainPerFrame = 0.012f;
        [Tooltip("At or below this fraction of health the health bar pulses red.")]
        [Range(0f, 1f)] public float lowHealth = 0.25f;
        [Min(2)] public int lowHealthPulseFrames = 40;
        [Tooltip("When the player is hurt the health card shakes this far (pixels) for hurtShakeFrames.")]
        [Min(0f)] public float hurtShake = 5f;
        [Min(0)] public int hurtShakeFrames = 12;
        [Min(8)] public int comboSize = 34;

        [Header("Boss bar")]
        [Min(200f)] public float bossBarWidth = 900f;
        [Min(4f)] public float bossBarHeight = 22f;
        [Tooltip("Gap from the bottom of the screen.")]
        [Min(0f)] public float bossBarBottom = 48f;

        [Header("Menus")]
        [Min(8)] public int menuTitleSize = 44;
        [Min(8)] public int menuItemSize = 28;
        [Tooltip("Holding a direction: frames before it starts repeating, then frames between repeats.")]
        [Min(1)] public int menuRepeatDelay = 24;
        [Min(1)] public int menuRepeatInterval = 7;

        [Header("Buttons")]
        [Min(80f)] public float buttonWidth = 400f;
        [Min(24f)] public float buttonHeight = 64f;
        [Min(0f)] public float buttonRadius = 14f;
        [Min(0f)] public float buttonBorder = 3f;
        [Min(8f)] public float buttonIconSize = 30f;
        [Tooltip("Gap between buttons.")]
        [Min(0f)] public float buttonSpacing = 14f;
        [Tooltip("Key and mouse pictures (Controls page, prompts).")]
        [Min(8f)] public float keyIconSize = 44f;

        [Header("Icons (white pictures, tinted ink or paper)")]
        public Texture2D iconResume;
        public Texture2D iconRestart;
        public Texture2D iconControls;
        public Texture2D iconQuit;
        public Texture2D iconBack;
        [Tooltip("Key and mouse pictures by name (\"f\", \"space\", \"mouse-left\", \"keyboard-wasd\"...). Filled from " +
                 "Art/UI/InputIcons by the builders; see KeyIcons for how bindings map to names.")]
        public List<KeyIcon> keyIcons = new List<KeyIcon>();

        [Serializable]
        public struct KeyIcon
        {
            public string name;
            public Texture2D icon;
        }

        private Dictionary<string, Texture2D> keyIconLookup;

        /// <summary>The picture for a key name, or null.</summary>
        public Texture2D FindKeyIcon(string name)
        {
            if (string.IsNullOrEmpty(name) || keyIcons == null) return null;
            if (keyIconLookup == null)
            {
                keyIconLookup = new Dictionary<string, Texture2D>();
                foreach (KeyIcon k in keyIcons)
                    if (!string.IsNullOrEmpty(k.name) && k.icon != null) keyIconLookup[k.name] = k.icon;
            }
            return keyIconLookup.TryGetValue(name, out Texture2D icon) ? icon : null;
        }

        // Edited in the Inspector (or by the builder): rebuild the lookup next time.
        private void OnValidate() => keyIconLookup = null;

        [Header("Assets")]
        [Tooltip("UI Toolkit theme (Data/UI/MarginTheme.tss). Only needed so Unity doesn't warn; the ink look is drawn in code.")]
        public ThemeStyleSheet theme;
        [Tooltip("Text font. Empty = Unity's built-in font.")]
        public Font font;

        private static UISettings defaults;

        /// <summary>Default values, for scenes set up before the settings asset existed.</summary>
        public static UISettings Defaults
        {
            get
            {
                if (defaults == null)
                {
                    defaults = CreateInstance<UISettings>();
                    defaults.hideFlags = HideFlags.DontSave;
                }
                return defaults;
            }
        }
    }
}
