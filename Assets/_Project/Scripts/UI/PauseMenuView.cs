using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace Margin.UI
{
    /// <summary>
    /// The pause menu (spec 14): a paper card over the dimmed game with Resume, Restart, Controls and Quit.
    /// Works with keyboard, gamepad (MarginControls "Menu" map, fed in by MarginUI) and mouse (hover + click).
    /// "Controls" shows the current bindings read from the input asset, so it stays right if bindings change.
    /// </summary>
    public sealed class PauseMenuView
    {
        private enum Page { Main, Controls }

        private readonly UISettings s;
        private readonly VisualElement layer;
        private readonly InkPanel mainCard, controlsCard;
        private readonly InkSelectionMark mainMark, controlsMark;
        private readonly List<VisualElement> mainRows = new List<VisualElement>();
        private readonly List<string> mainLabels = new List<string>();
        private readonly List<Action> mainActions = new List<Action>();
        private readonly VisualElement backRow;
        private readonly VisualElement bindingList;
        private readonly MenuCursor cursor;
        private Page page;
        private InputActionAsset shownBindings;

        private const float MainWidth = 560f;
        private const float ControlsWidth = 900f;

        public bool IsOpen { get; private set; }

        public PauseMenuView(VisualElement parent, UISettings settings, Action resume, Action restart, Action quit)
        {
            s = settings;
            layer = HudView.Layer(parent);
            layer.pickingMode = PickingMode.Position;   // blocks clicks to anything behind the menu
            layer.Add(new InkDim(s));

            // ---- Main page ----
            float rowHeight = s.menuItemSize + 26f;
            float firstRow = 40f + s.menuTitleSize + 34f;
            float mainHeight = firstRow + rowHeight * 4f + 40f;
            mainCard = Card(MainWidth, mainHeight);
            layer.Add(mainCard);
            mainCard.Add(Title("PAUSED", MainWidth));

            mainMark = new InkSelectionMark(s);
            mainCard.Add(mainMark);
            AddMainItem("Resume", resume, firstRow, rowHeight);
            AddMainItem("Restart", restart, firstRow, rowHeight);
            AddMainItem("Controls", () => ShowPage(Page.Controls), firstRow, rowHeight);
            AddMainItem("Quit", quit, firstRow, rowHeight);

            // ---- Controls page ----
            float controlsHeight = 700f;
            controlsCard = Card(ControlsWidth, controlsHeight);
            layer.Add(controlsCard);
            controlsCard.Add(Title("CONTROLS", ControlsWidth));
            bindingList = new VisualElement { pickingMode = PickingMode.Ignore };
            HudView.Place(bindingList, 0f, 40f + s.menuTitleSize + 24f, ControlsWidth, controlsHeight - 200f);
            controlsCard.Add(bindingList);
            controlsMark = new InkSelectionMark(s);
            controlsCard.Add(controlsMark);
            backRow = Row("Back", 60f, controlsHeight - rowHeight - 36f, rowHeight);
            backRow.RegisterCallback<ClickEvent>(_ => ShowPage(Page.Main));
            controlsCard.Add(backRow);

            cursor = new MenuCursor(mainRows.Count);
            Close();
        }

        public void Open(InputActionAsset actions)
        {
            IsOpen = true;
            HudView.SetVisible(layer, true);
            if (actions != null && actions != shownBindings)
            {
                shownBindings = actions;
                FillBindings(actions);
            }
            ShowPage(Page.Main);
            cursor.Select(0);
            UpdateMarks();
        }

        public void Close()
        {
            IsOpen = false;
            HudView.SetVisible(layer, false);
        }

        /// <summary>
        /// Called every rendered frame while open. navigate: stick/keys (y up = previous item); submit/cancel:
        /// pressed this frame. Returns false when Cancel was pressed on the main page (the caller resumes).
        /// </summary>
        public bool Update(Vector2 navigate, bool submit, bool cancel, float realFrames)
        {
            if (!IsOpen) return true;

            if (page == Page.Main)
            {
                int direction = navigate.y > 0.5f ? -1 : navigate.y < -0.5f ? 1 : 0;
                if (cursor.Hold(direction, realFrames, s.menuRepeatDelay, s.menuRepeatInterval)) UpdateMarks();
                if (submit) mainActions[cursor.Index]?.Invoke();
                else if (cancel) return false;
            }
            else if (submit || cancel) ShowPage(Page.Main);

            mainCard.Refresh();
            controlsCard.Refresh();
            mainMark.Refresh();
            controlsMark.Refresh();
            return true;
        }

        /// <summary>Pause pressed while open: back out of Controls first, otherwise close. Returns true if it closed.</summary>
        public bool Back()
        {
            if (page == Page.Controls)
            {
                ShowPage(Page.Main);
                return false;
            }
            return true;
        }

        // ---------------- pages ----------------

        private void ShowPage(Page next)
        {
            page = next;
            HudView.SetVisible(mainCard, page == Page.Main);
            HudView.SetVisible(controlsCard, page == Page.Controls);
            UpdateMarks();
        }

        private void AddMainItem(string text, Action action, float firstRow, float rowHeight)
        {
            int index = mainRows.Count;
            VisualElement row = Row(text, 60f, firstRow + rowHeight * index, rowHeight);
            row.RegisterCallback<PointerEnterEvent>(_ =>
            {
                cursor.Select(index);
                UpdateMarks();
            });
            row.RegisterCallback<ClickEvent>(_ =>
            {
                cursor.Select(index);
                action?.Invoke();
            });
            mainCard.Add(row);
            mainRows.Add(row);
            mainLabels.Add(text);
            mainActions.Add(action);
        }

        /// <summary>Moves the hand-drawn arrow + underline to the selected item.</summary>
        private void UpdateMarks()
        {
            if (page == Page.Main && mainRows.Count > 0)
            {
                VisualElement row = mainRows[cursor.Index];
                PlaceMark(mainMark, row, mainLabels[cursor.Index]);
            }
            else PlaceMark(controlsMark, backRow, "Back");
        }

        private void PlaceMark(InkSelectionMark mark, VisualElement row, string text)
        {
            // Width from the text length (an estimate: layout sizes aren't known on the first frame).
            float width = 44f + text.Length * s.menuItemSize * 0.62f;
            HudView.Place(mark, row.style.left.value.value - 10f, row.style.top.value.value, width, row.style.height.value.value);
            mark.MarkDirtyRepaint();
        }

        private void FillBindings(InputActionAsset actions)
        {
            bindingList.Clear();
            InputActionMap map = actions.FindActionMap("Gameplay");
            InputActionMap menu = actions.FindActionMap("Menu");
            int size = s.labelSize + 2;
            float y = 0f, lineHeight = size + 14f;

            void Line(string what, string keyboard, string gamepad, Color color)
            {
                bindingList.Add(MarginUI.MakeLabel(s, what, size, color, 60f, y, 230f));
                bindingList.Add(MarginUI.MakeLabel(s, keyboard, size, color, 300f, y, 300f));
                bindingList.Add(MarginUI.MakeLabel(s, gamepad, size, color, 610f, y, 260f));
                y += lineHeight;
            }

            Line("", "KEYBOARD / MOUSE", "GAMEPAD", s.faint);
            Line("Move", "WASD / Arrows", "Left Stick / D-Pad", s.ink);
            Line("Jump", Keys(map, "Jump", "Keyboard"), Keys(map, "Jump", "Gamepad"), s.ink);
            Line("Light attack", Keys(map, "LightAttack", "Keyboard"), Keys(map, "LightAttack", "Gamepad"), s.ink);
            Line("Heavy attack", Keys(map, "HeavyAttack", "Keyboard"), Keys(map, "HeavyAttack", "Gamepad"), s.ink);
            Line("Special", Keys(map, "Special", "Keyboard"), Keys(map, "Special", "Gamepad"), s.ink);
            Line("Dash", Keys(map, "Dash", "Keyboard"), Keys(map, "Dash", "Gamepad"), s.ink);
            Line("Parry", Keys(map, "Parry", "Keyboard"), Keys(map, "Parry", "Gamepad"), s.ink);
            Line("Pause", Keys(menu, "Pause", "Keyboard"), Keys(menu, "Pause", "Gamepad"), s.ink);
            Line("Redraw (100 ink)", "Down + Special", "Down + Special", s.ink);
            Line("Combo breaker (50)", "Parry while hit", "Parry while hit", s.ink);
        }

        /// <summary>The keys bound to an action for one control scheme, e.g. "Left Button | J".</summary>
        private static string Keys(InputActionMap map, string action, string group)
        {
            InputAction a = map?.FindAction(action);
            if (a == null) return "-";
            string text = a.GetBindingDisplayString(InputBinding.MaskByGroup(group));
            return string.IsNullOrEmpty(text) ? "-" : text;
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

        private Label Title(string text, float cardWidth)
        {
            Label title = MarginUI.MakeLabel(s, text, s.menuTitleSize, s.ink, 0f, 36f, cardWidth);
            title.style.unityTextAlign = TextAnchor.UpperCenter;
            return title;
        }

        private VisualElement Row(string text, float x, float y, float height)
        {
            var row = new VisualElement();   // pickable, for mouse hover and click
            HudView.Place(row, x, y, 440f, height);
            row.Add(MarginUI.MakeLabel(s, text, s.menuItemSize, s.ink, 44f, (height - s.menuItemSize) * 0.5f - 4f));
            return row;
        }
    }
}
