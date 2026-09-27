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
    /// click activates). A Controls entry opens a page listing every action with the key/mouse pictures of its
    /// current bindings (text for gamepad), read from the input asset so it follows rebinding.
    /// </summary>
    public sealed class MenuView
    {
        /// <summary>One button: label, icon and what it does (or which page it opens).</summary>
        public struct Entry
        {
            public string Label;
            public Texture2D Icon;
            public Action Action;
            internal Page Opens;
        }

        public static Entry Item(string label, Texture2D icon, Action action) =>
            new Entry { Label = label, Icon = icon, Action = action, Opens = Page.Main };

        /// <summary>The button that opens the Controls page.</summary>
        public static Entry Controls(UISettings s) => new Entry { Label = "Controls", Icon = s.iconControls, Opens = Page.Controls };

        /// <summary>The button that opens the Options page (audio, screen shake, display).</summary>
        public static Entry Options(UISettings s) => new Entry { Label = "Options", Icon = s.iconOptions, Opens = Page.Options };

        internal enum Page { Main, Controls, Options }

        private readonly UISettings s;
        private readonly VisualElement layer;
        private readonly InkPanel mainCard, controlsCard;
        private readonly List<UIButton> buttons = new List<UIButton>();
        private readonly List<Action> actions = new List<Action>();
        private readonly UIButton backButton;
        private readonly VisualElement bindingList;
        private readonly RuledPaper page;
        private readonly OptionsPanel options;
        private MenuCursor cursor;
        private Page shown;
        private InputActionAsset shownBindings;
        private UIButton pressedButton;

        private const float ControlsWidth = 980f;

        public bool IsOpen { get; private set; }

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
            mainCard = Card(mainWidth, mainHeight);
            layer.Add(mainCard);
            VisualElement column = Column(mainCard);
            Label titleLabel = Title(title, size);
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
                Page opens = e.Opens;
                AddButton(column, e.Label, e.Icon, opens == Page.Main ? e.Action : () => ShowPage(opens));
            }

            // ---- Options page ----
            options = new OptionsPanel(s, Card);
            layer.Add(options.Card);

            // ---- Controls page: bindings table + Back ----
            float rowHeight = s.keyIconSize + 10f;
            float controlsHeight = 60f + s.menuTitleSize + 30f + 12f * rowHeight + s.buttonHeight + 70f;
            controlsCard = Card(ControlsWidth, controlsHeight);
            layer.Add(controlsCard);
            VisualElement controlsColumn = Column(controlsCard);
            controlsColumn.Add(Title("CONTROLS", s.menuTitleSize));
            bindingList = new VisualElement { pickingMode = PickingMode.Ignore };
            bindingList.style.width = ControlsWidth - 120f;
            bindingList.style.marginBottom = 24f;
            controlsColumn.Add(bindingList);
            backButton = new UIButton(s, "Back", s.iconBack);
            backButton.Clicked += () =>
            {
                Sfx.Play("ui_back");
                ShowPage(Page.Main);
            };
            controlsColumn.Add(backButton);

            cursor = new MenuCursor(buttons.Count);
            Close();
        }

        public void Open(InputActionAsset inputActions)
        {
            IsOpen = true;
            HudView.SetVisible(layer, true);
            if (inputActions != null && inputActions != shownBindings)
            {
                shownBindings = inputActions;
                FillBindings(inputActions);
            }
            ShowPage(Page.Main);
            Select(0);
        }

        public void Close()
        {
            if (options != null && options.IsOpen) options.Close();
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

            if (shown == Page.Options)
            {
                options.Update(navigate, submit, cancel, realFrames);
                if (page != null) page.Refresh();
                return true;
            }

            if (shown == Page.Main)
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
            }
            else if (submit || cancel)
            {
                Sfx.Play("ui_back");
                ShowPage(Page.Main);
            }

            mainCard.Refresh();
            controlsCard.Refresh();
            if (page != null) page.Refresh();
            return true;
        }

        /// <summary>Pause pressed while open: back out of Controls first, otherwise close. Returns true if it closed.</summary>
        public bool Back()
        {
            if (shown == Page.Options)
            {
                options.Close();   // saves and returns to the main page
                return false;
            }
            if (shown != Page.Controls) return true;
            ShowPage(Page.Main);
            return false;
        }

        // ---------------- pages and selection ----------------

        private void ShowPage(Page next)
        {
            if (shown == Page.Options && next != Page.Options && options.IsOpen) options.Close();
            shown = next;
            HudView.SetVisible(mainCard, shown == Page.Main);
            HudView.SetVisible(controlsCard, shown == Page.Controls);
            backButton.SetSelected(shown == Page.Controls);   // the only button there
            if (shown == Page.Options && !options.IsOpen) options.Open(() => ShowPage(Page.Main));
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

        // ---------------- controls table ----------------

        private void FillBindings(InputActionAsset inputActions)
        {
            bindingList.Clear();
            InputActionMap gameplay = inputActions.FindActionMap("Gameplay");
            InputActionMap menu = inputActions.FindActionMap("Menu");
            int size = s.labelSize + 2;

            Header("", "KEYBOARD / MOUSE", "GAMEPAD", size);
            Row("Move", gameplay?.FindAction("Move"), size);
            Row("Jump", gameplay?.FindAction("Jump"), size);
            Row("Light attack", gameplay?.FindAction("LightAttack"), size);
            Row("Heavy attack", gameplay?.FindAction("HeavyAttack"), size);
            Row("Special", gameplay?.FindAction("Special"), size);
            Row("Dash", gameplay?.FindAction("Dash"), size);
            Row("Parry", gameplay?.FindAction("Parry"), size);
            if (gameplay?.FindAction("Grapple") != null) Row("Grapple Line", gameplay.FindAction("Grapple"), size);
            Row("Pause", menu?.FindAction("Pause"), size);
            if (gameplay?.FindAction("Heal") != null) Row("Heal (100 ink)", gameplay.FindAction("Heal"), size);
            else Header("Redraw (100 ink)", "Down + Special", "Down + Special", size);
            Header("Combo breaker (50 ink)", "Parry while hit", "Parry while hit", size);
        }

        private VisualElement TableRow()
        {
            var row = new VisualElement { pickingMode = PickingMode.Ignore };
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.height = s.keyIconSize + 10f;
            bindingList.Add(row);
            return row;
        }

        private VisualElement Cell(VisualElement row, float width)
        {
            var cell = new VisualElement { pickingMode = PickingMode.Ignore };
            cell.style.width = width;
            cell.style.flexDirection = FlexDirection.Row;
            cell.style.alignItems = Align.Center;
            row.Add(cell);
            return cell;
        }

        private void Header(string what, string keyboard, string gamepad, int size)
        {
            VisualElement row = TableRow();
            Color color = string.IsNullOrEmpty(what) ? s.faint : s.ink;
            Cell(row, 300f).Add(UIButton.FlowLabel(s, what, size, s.ink));
            Cell(row, 300f).Add(UIButton.FlowLabel(s, keyboard, size, color));
            Cell(row, 260f).Add(UIButton.FlowLabel(s, gamepad, size, color));
        }

        private void Row(string what, InputAction action, int size)
        {
            VisualElement row = TableRow();
            Cell(row, 300f).Add(UIButton.FlowLabel(s, what, size, s.ink));
            Cell(row, 300f).Add(new KeyHint(s, action, "Keyboard", size, s.keyIconSize, s.ink));
            Cell(row, 260f).Add(new KeyHint(s, action, "Gamepad", size, s.keyIconSize, s.ink));
        }

        // ---------------- building helpers ----------------

        private InkPanel Card(float width, float height)
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

        private Label Title(string text, int size)
        {
            Label title = UIButton.FlowLabel(s, text, size, s.ink);
            title.style.marginBottom = 30f;
            return title;
        }
    }
}
