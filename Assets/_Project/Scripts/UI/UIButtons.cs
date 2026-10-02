using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace Margin.UI
{
    /// <summary>
    /// A clean flat menu button: rounded box with an ink border, a white pack icon tinted to match, and a label.
    /// Selected (keyboard/gamepad focus or mouse hover) inverts it to paper on ink; pressing squashes it slightly.
    /// The menu owns selection (MenuCursor); the button reports hover and clicks.
    /// </summary>
    public sealed class UIButton : VisualElement
    {
        private readonly UISettings s;
        private readonly VisualElement icon;
        private readonly Label label;
        private bool selected;

        /// <summary>The mouse moved onto the button (the menu selects it).</summary>
        public event Action Hovered;
        /// <summary>Clicked with the mouse. Keyboard/gamepad presses go through the menu, which calls Press().</summary>
        public event Action Clicked;

        public UIButton(UISettings settings, string text, Texture2D iconTexture)
        {
            s = settings;
            style.width = s.buttonWidth;
            style.height = s.buttonHeight;
            style.flexDirection = FlexDirection.Row;
            style.alignItems = Align.Center;
            style.paddingLeft = 24f;
            style.marginBottom = s.buttonSpacing;
            SetRadius(this, s.buttonRadius);
            SetBorder(this, s.buttonBorder, s.ink);

            icon = new VisualElement { pickingMode = PickingMode.Ignore };
            icon.style.width = s.buttonIconSize;
            icon.style.height = s.buttonIconSize;
            icon.style.marginRight = 18f;
            icon.style.unityBackgroundScaleMode = ScaleMode.ScaleToFit;
            if (iconTexture != null) icon.style.backgroundImage = new StyleBackground(iconTexture);
            else icon.style.display = DisplayStyle.None;
            Add(icon);

            label = FlowLabel(s, text, s.menuItemSize, s.ink);
            Add(label);

            RegisterCallback<PointerEnterEvent>(_ => Hovered?.Invoke());
            RegisterCallback<PointerDownEvent>(_ => SetPressed(true));
            RegisterCallback<PointerUpEvent>(_ => SetPressed(false));
            RegisterCallback<PointerLeaveEvent>(_ => SetPressed(false));
            RegisterCallback<ClickEvent>(_ => Clicked?.Invoke());
            SetSelected(false);
        }

        public void SetSelected(bool on)
        {
            selected = on;
            Color back = on ? s.ink : s.paper;
            Color fore = on ? s.paper : s.ink;
            style.backgroundColor = back;
            label.style.color = fore;
            icon.style.unityBackgroundImageTintColor = fore;
        }

        public bool Selected => selected;

        /// <summary>Changes the button's text (save slots show their summary).</summary>
        public void SetText(string text) => label.text = text;

        /// <summary>Keyboard/gamepad confirm: a quick squash (released next frame by the menu).</summary>
        public void SetPressed(bool on) =>
            style.scale = new Scale(on ? new Vector3(0.96f, 0.96f, 1f) : Vector3.one);

        // ---------------- shared style helpers ----------------

        internal static void SetRadius(VisualElement e, float r)
        {
            e.style.borderTopLeftRadius = r;
            e.style.borderTopRightRadius = r;
            e.style.borderBottomLeftRadius = r;
            e.style.borderBottomRightRadius = r;
        }

        internal static void SetBorder(VisualElement e, float width, Color color)
        {
            e.style.borderLeftWidth = width;
            e.style.borderRightWidth = width;
            e.style.borderTopWidth = width;
            e.style.borderBottomWidth = width;
            e.style.borderLeftColor = color;
            e.style.borderRightColor = color;
            e.style.borderTopColor = color;
            e.style.borderBottomColor = color;
        }

        /// <summary>A label that takes part in flex layout (MarginUI.MakeLabel places absolutely).</summary>
        internal static Label FlowLabel(UISettings s, string text, int size, Color color)
        {
            Label label = MarginUI.MakeLabel(s, text, size, color, 0f, 0f);
            label.style.position = Position.Relative;
            label.style.left = StyleKeyword.Auto;
            label.style.top = StyleKeyword.Auto;
            return label;
        }
    }

    /// <summary>
    /// Shows how to press an action: the pack's key/mouse pictures for each keyboard binding (e.g. [F]), with text
    /// where there's no picture (gamepad, unusual keys). Built from the input asset, so it follows rebinding.
    /// </summary>
    public sealed class KeyHint : VisualElement
    {
        public KeyHint(UISettings s, InputAction action, string group, int textSize, float iconSize, Color color, int maxEntries = 3)
        {
            pickingMode = PickingMode.Ignore;
            style.flexDirection = FlexDirection.Row;
            style.alignItems = Align.Center;

            List<Entry> entries = Collect(action, group, s);
            int shown = 0;
            foreach (Entry e in entries)
            {
                if (shown == maxEntries) break;
                if (shown > 0) Add(Text(s, "/", textSize, color));
                if (e.Icon != null)
                {
                    var pic = new VisualElement { pickingMode = PickingMode.Ignore };
                    pic.style.width = iconSize;
                    pic.style.height = iconSize;
                    pic.style.backgroundImage = new StyleBackground(e.Icon);
                    pic.style.unityBackgroundImageTintColor = color;
                    pic.style.unityBackgroundScaleMode = ScaleMode.ScaleToFit;
                    Add(pic);
                }
                else Add(Text(s, e.Text, textSize, color));
                shown++;
            }
            if (shown == 0) Add(Text(s, "-", textSize, color));
        }

        private static Label Text(UISettings s, string text, int size, Color color)
        {
            Label l = UIButton.FlowLabel(s, text, size, color);
            l.style.marginLeft = 4f;
            l.style.marginRight = 4f;
            return l;
        }

        private struct Entry
        {
            public Texture2D Icon;
            public string Text;
        }

        /// <summary>One entry per binding in the group; composites (WASD) become one entry.</summary>
        private static List<Entry> Collect(InputAction action, string group, UISettings s)
        {
            var result = new List<Entry>();
            if (action == null) return result;
            var bindings = action.bindings;
            for (int i = 0; i < bindings.Count; i++)
            {
                InputBinding b = bindings[i];
                if (b.isComposite)
                {
                    // Gather this composite's parts (they follow it in the list).
                    var parts = new List<string>();
                    bool inGroup = false;
                    int j = i + 1;
                    for (; j < bindings.Count && bindings[j].isPartOfComposite; j++)
                    {
                        parts.Add(bindings[j].effectivePath);
                        inGroup |= InGroup(bindings[j], group);
                    }
                    if (inGroup)
                    {
                        Texture2D pic = s.FindKeyIcon(KeyIcons.ForComposite(parts));
                        result.Add(new Entry { Icon = pic, Text = pic == null ? action.GetBindingDisplayString(i) : null });
                    }
                    i = j - 1;
                    continue;
                }
                if (b.isPartOfComposite || !InGroup(b, group)) continue;
                Texture2D icon = s.FindKeyIcon(KeyIcons.ForPath(b.effectivePath));
                result.Add(new Entry { Icon = icon, Text = icon == null ? action.GetBindingDisplayString(i) : null });
            }
            return result;
        }

        private static bool InGroup(InputBinding b, string group) =>
            !string.IsNullOrEmpty(b.groups) && Array.IndexOf(b.groups.Split(';'), group) >= 0;
    }
}
