using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace Margin.UI
{
    /// <summary>
    /// The pause menu (spec 14): a hand-drawn paper card over the dimmed game with clean flat buttons (Resume,
    /// Restart, Controls, Quit), each with an icon from Art/UI. Works with keyboard, gamepad (MarginControls "Menu"
    /// map, fed in by MarginUI) and mouse (hover selects, click activates).
    /// "Controls" lists every action with the key/mouse pictures of its current bindings (text for gamepad),
    /// read from the input asset so it follows rebinding.
    /// </summary>
    public sealed class PauseMenuView
    {
        private enum Page { Main, Controls }

        private readonly UISettings s;
        private readonly VisualElement layer;
        private readonly InkPanel mainCard, controlsCard;
        private readonly List<UIButton> buttons = new List<UIButton>();
        private readonly List<Action> actions = new List<Action>();
        private readonly UIButton backButton;
        private readonly VisualElement bindingList;
        private readonly MenuCursor cursor;
        private Page page;
        private InputActionAsset shownBindings;
        private UIButton pressedButton;

        private const float ControlsWidth = 980f;

        public bool IsOpen { get; private set; }

        public PauseMenuView(VisualElement parent, UISettings settings, Action resume, Action restart, Action quit)
        {
            s = settings;
            layer = HudView.Layer(parent);
            layer.pickingMode = PickingMode.Position;   // blocks clicks to anything behind the menu
            layer.Add(new InkDim(s));

            // ---- Main page: title + a column of buttons ----
            float mainWidth = s.buttonWidth + 120f;
            float mainHeight = 60f + s.menuTitleSize + 36f + 4f * (s.buttonHeight + s.buttonSpacing) + 40f;
            mainCard = Card(mainWidth, mainHeight);
            layer.Add(mainCard);
            VisualElement column = Column(mainCard);
            column.Add(Title("PAUSED"));

            AddButton(column, "Resume", s.iconResume, resume);
            AddButton(column, "Restart", s.iconRestart, restart);
            AddButton(column, "Controls", s.iconControls, () => ShowPage(Page.Controls));
            AddButton(column, "Quit", s.iconQuit, quit);

            // ---- Controls page: bindings table + Back ----
            float rowHeight = s.keyIconSize + 10f;
            float controlsHeight = 60f + s.menuTitleSize + 30f + 11f * rowHeight + s.buttonHeight + 70f;
            controlsCard = Card(ControlsWidth, controlsHeight);
            layer.Add(controlsCard);
            VisualElement controlsColumn = Column(controlsCard);
            controlsColumn.Add(Title("CONTROLS"));
            bindingList = new VisualElement { pickingMode = PickingMode.Ignore };
            bindingList.style.width = ControlsWidth - 120f;
            bindingList.style.marginBottom = 24f;
            controlsColumn.Add(bindingList);
            backButton = new UIButton(s, "Back", s.iconBack);
            backButton.Clicked += () => ShowPage(Page.Main);
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
            IsOpen = false;
            HudView.SetVisible(layer, false);
        }

        /// <summary>
        /// Called every rendered frame while open. navigate: stick/keys (y up = previous button); submit/cancel:
        /// pressed this frame. Returns false when Cancel was pressed on the main page (the caller resumes).
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

            if (page == Page.Main)
            {
                int direction = navigate.y > 0.5f ? -1 : navigate.y < -0.5f ? 1 : 0;
                if (cursor.Hold(direction, realFrames, s.menuRepeatDelay, s.menuRepeatInterval)) Select(cursor.Index);
                if (submit)
                {
                    pressedButton = buttons[cursor.Index];
                    pressedButton.SetPressed(true);
                    actions[cursor.Index]?.Invoke();
                }
                else if (cancel) return false;
            }
            else if (submit || cancel) ShowPage(Page.Main);

            mainCard.Refresh();
            controlsCard.Refresh();
            return true;
        }

        /// <summary>Pause pressed while open: back out of Controls first, otherwise close. Returns true if it closed.</summary>
        public bool Back()
        {
            if (page != Page.Controls) return true;
            ShowPage(Page.Main);
            return false;
        }

        // ---------------- pages and selection ----------------

        private void ShowPage(Page next)
        {
            page = next;
            HudView.SetVisible(mainCard, page == Page.Main);
            HudView.SetVisible(controlsCard, page == Page.Controls);
            backButton.SetSelected(page == Page.Controls);   // the only button there
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
            button.Hovered += () => Select(index);
            button.Clicked += () => action?.Invoke();
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
            Row("Pause", menu?.FindAction("Pause"), size);
            Header("Redraw (100 ink)", "Down + Special", "Down + Special", size);
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

        private Label Title(string text)
        {
            Label title = UIButton.FlowLabel(s, text, s.menuTitleSize, s.ink);
            title.style.marginBottom = 30f;
            return title;
        }
    }
}
