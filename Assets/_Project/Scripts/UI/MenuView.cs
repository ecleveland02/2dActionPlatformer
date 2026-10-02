using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;
using Margin.Audio;

namespace Margin.UI
{
    /// <summary>
    /// A menu (spec 14): a hand-drawn paper card with a title and a column of clean flat buttons, each with an icon
    /// from Art/UI. Used by the pause menu (over the dimmed game) and the title screen (over a full notebook page).
    /// Works with keyboard, gamepad (MarginControls "Menu" map, fed in by the owner) and mouse (hover selects,
    /// click activates). Buttons either do something or open a page of their own (IMenuPanel): Controls (bindings
    /// table + rebinding), Options, the title screen's save slots.
    /// </summary>
    public sealed class MenuView
    {
        /// <summary>One button: label, icon and what it does (or which page it opens).</summary>
        public struct Entry
        {
            public string Label;
            public Texture2D Icon;
            public Action Action;
            internal Func<MenuView, IMenuPanel> MakePanel;
        }

        public static Entry Item(string label, Texture2D icon, Action action) =>
            new Entry { Label = label, Icon = icon, Action = action };

        /// <summary>The Controls page: every action's keys and buttons, and rebinding them.</summary>
        public static Entry Controls(UISettings s) =>
            Panel("Controls", s.iconControls, menu => new ControlsPanel(s, menu.MakeCard, () => menu.Bindings));

        /// <summary>The Options page (audio, screen shake, display).</summary>
        public static Entry Options(UISettings s) => Panel("Options", s.iconOptions, menu => new OptionsPanel(s, menu.MakeCard));

        /// <summary>A button that opens a page of its own.</summary>
        public static Entry Panel(string label, Texture2D icon, Func<MenuView, IMenuPanel> make) =>
            new Entry { Label = label, Icon = icon, MakePanel = make };

        private readonly UISettings s;
        private readonly VisualElement layer;
        private readonly InkPanel mainCard;
        private readonly List<UIButton> buttons = new List<UIButton>();
        private readonly List<Action> actions = new List<Action>();
        private readonly RuledPaper page;
        private readonly MenuCursor cursor;
        private IMenuPanel openPanel;
        private UIButton pressedButton;

        public bool IsOpen { get; private set; }

        /// <summary>The controls asset given to Open (the Controls page reads and rebinds it).</summary>
        public InputActionAsset Bindings { get; private set; }

        /// <param name="fullPage">True: a whole notebook page behind the card (title screen). False: dim the game.</param>
        /// <param name="titleSize">Title text size; 0 = UISettings.menuTitleSize.</param>
        public MenuView(VisualElement parent, UISettings settings, string title, IList<Entry> entries, bool fullPage,
                        string subtitle = null, int titleSize = 0)
        {
            s = settings;
            layer = HudView.Layer(parent);
            layer.pickingMode = PickingMode.Position;   // blocks clicks to anything behind the menu
            if (fullPage)
            {
                page = new RuledPaper(s);
                page.style.position = Position.Absolute;
                page.style.left = page.style.top = page.style.right = page.style.bottom = 0f;
                layer.Add(page);
            }
            else layer.Add(new InkDim(s));

            // ---- Main page: title (+ subtitle) + a column of buttons ----
            int size = titleSize > 0 ? titleSize : s.menuTitleSize;
            float mainWidth = s.buttonWidth + 120f;
            float mainHeight = 60f + size + 36f + (string.IsNullOrEmpty(subtitle) ? 0f : s.labelSize + 24f) +
                               entries.Count * (s.buttonHeight + s.buttonSpacing) + 40f;
            mainCard = MakeCard(mainWidth, mainHeight);
            layer.Add(mainCard);
            VisualElement column = Column(mainCard);
            Label titleLabel = UIButton.FlowLabel(s, title, size, s.ink);
            titleLabel.style.marginBottom = 30f;
            column.Add(titleLabel);
            if (!string.IsNullOrEmpty(subtitle))
            {
                titleLabel.style.marginBottom = 6f;
                Label sub = UIButton.FlowLabel(s, subtitle, s.labelSize + 2, s.faint);
                sub.style.marginBottom = 30f;
                column.Add(sub);
            }

            foreach (Entry e in entries)
            {
                if (e.MakePanel != null)
                {
                    // A page of its own: built now, shown when its button is picked.
                    IMenuPanel panel = e.MakePanel(this);
                    layer.Add(panel.Card);
                    HudView.SetVisible(panel.Card, false);
                    AddButton(column, e.Label, e.Icon, () => OpenPanel(panel));
                }
                else AddButton(column, e.Label, e.Icon, e.Action);
            }

            cursor = new MenuCursor(buttons.Count);
            Close();
        }

        public void Open(InputActionAsset inputActions)
        {
            if (inputActions != null) Bindings = inputActions;
            IsOpen = true;
            HudView.SetVisible(layer, true);
            ShowMain();
            Select(0);
        }

        public void Close()
        {
            if (openPanel != null && openPanel.IsOpen) openPanel.Close();
            IsOpen = false;
            HudView.SetVisible(layer, false);
        }

        /// <summary>
        /// Called every rendered frame while open. navigate: stick/keys (y up = previous button); submit/cancel:
        /// pressed this frame. Returns false when Cancel was pressed on the main page (the owner decides what that
        /// means: the pause menu resumes, the title screen ignores it).
        /// </summary>
        public bool Update(Vector2 navigate, bool submit, bool cancel, float realFrames)
        {
            if (!IsOpen) return true;

            // Release last frame's keyboard/gamepad press squash.
            if (pressedButton != null)
            {
                pressedButton.SetPressed(false);
                pressedButton = null;
            }

            if (openPanel != null && openPanel.IsOpen)
            {
                openPanel.Update(navigate, submit, cancel, realFrames);
            }
            else
            {
                int direction = navigate.y > 0.5f ? -1 : navigate.y < -0.5f ? 1 : 0;
                if (cursor.Hold(direction, realFrames, s.menuRepeatDelay, s.menuRepeatInterval))
                {
                    Select(cursor.Index);
                    Sfx.Play("ui_move");
                }
                if (submit)
                {
                    pressedButton = buttons[cursor.Index];
                    pressedButton.SetPressed(true);
                    Sfx.Play("ui_select");
                    actions[cursor.Index]?.Invoke();
                }
                else if (cancel) return false;
                mainCard.Refresh();
            }

            if (page != null) page.Refresh();
            return true;
        }

        /// <summary>
        /// Pause pressed while open: back out of an open page first, otherwise close. Returns true if it closed.
        /// A page that's busy (waiting for a key to rebind) keeps the press.
        /// </summary>
        public bool Back()
        {
            if (openPanel == null || !openPanel.IsOpen) return true;
            if (!openPanel.Busy) openPanel.Close();
            return false;
        }

        /// <summary>Builds a page card in the menu's hand-drawn style, centered on the screen.</summary>
        public InkPanel MakeCard(float width, float height)
        {
            var card = new InkPanel(s) { pickingMode = PickingMode.Position };
            card.style.position = Position.Absolute;
            card.style.left = Length.Percent(50f);
            card.style.top = Length.Percent(50f);
            card.style.marginLeft = -width * 0.5f;
            card.style.marginTop = -height * 0.5f;
            card.style.width = width;
            card.style.height = height;
            return card;
        }

        // ---------------- pages and selection ----------------

        private void ShowMain()
        {
            if (openPanel != null && openPanel.IsOpen) openPanel.Close();
            openPanel = null;
            HudView.SetVisible(mainCard, true);
        }

        private void OpenPanel(IMenuPanel panel)
        {
            openPanel = panel;
            HudView.SetVisible(mainCard, false);
            panel.Open(() =>
            {
                openPanel = null;
                HudView.SetVisible(mainCard, true);
            });
        }

        private void Select(int index)
        {
            cursor.Select(index);
            for (int i = 0; i < buttons.Count; i++) buttons[i].SetSelected(i == cursor.Index);
        }

        private void AddButton(VisualElement column, string text, Texture2D icon, Action action)
        {
            int index = buttons.Count;
            var button = new UIButton(s, text, icon);
            button.Hovered += () =>
            {
                if (cursor.Index != index) Sfx.Play("ui_move");
                Select(index);
            };
            button.Clicked += () =>
            {
                Sfx.Play("ui_select");
                action?.Invoke();
            };
            column.Add(button);
            buttons.Add(button);
            actions.Add(action);
        }

        private static VisualElement Column(VisualElement card)
        {
            var column = new VisualElement { pickingMode = PickingMode.Ignore };
            column.style.flexDirection = FlexDirection.Column;
            column.style.alignItems = Align.Center;
            column.style.paddingTop = 44f;
            column.style.flexGrow = 1f;
            card.Add(column);
            return column;
        }
    }
}
