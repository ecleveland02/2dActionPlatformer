using System;
using System.Collections.Generic;
using Margin.Audio;
using Margin.Input;
using Margin.Save;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace Margin.UI
{
    /// <summary>
    /// The Controls page (spec 14): every action with the key/mouse pictures of its bindings (text for gamepad), and
    /// rebinding. Pick a Keyboard or Gamepad cell (Up/Down/Left/Right, or the mouse) and press it, then press the new
    /// key or button: it replaces that action's main binding (a clash swaps with the other action). Escape / Start
    /// cancel a rebind. "Reset to defaults" removes every change. Saved to options.json when the page closes.
    /// Move, Pause and the menu keys aren't rebindable, so the menus can always be reached.
    /// </summary>
    public sealed class ControlsPanel : IMenuPanel
    {
        private static readonly string[] Groups = { "Keyboard", "Gamepad" };
        private static readonly Dictionary<string, string> Names = new Dictionary<string, string>
        {
            ["Jump"] = "Jump", ["LightAttack"] = "Light attack", ["HeavyAttack"] = "Heavy attack", ["Special"] = "Special",
            ["Dash"] = "Dash", ["Parry"] = "Parry", ["Grapple"] = "Grapple Line", ["Heal"] = "Heal (100 ink)",
        };

        private const float Width = 1040f;
        private const float NameWidth = 300f, KeyWidth = 330f, PadWidth = 290f;

        private readonly UISettings s;
        private readonly Func<InputActionAsset> getAsset;
        private readonly InkPanel card;
        private readonly VisualElement table;
        private readonly UIButton resetButton, backButton;
        private readonly Label hint;
        private readonly List<string> rows = new List<string>();          // rebindable actions shown
        private readonly List<VisualElement[]> cells = new List<VisualElement[]>();
        private readonly MenuCursor cursor;                                 // rows, then Reset, then Back
        private int column;
        private Action onClose;
        private InputActionRebindingExtensions.RebindingOperation rebinding;
        private int rebindRow = -1;
        private float cooldown;   // real frames: the key that finished a rebind mustn't also press a menu button
        private bool changed;

        public InkPanel Card => card;
        public bool IsOpen { get; private set; }
        public bool Busy => rebinding != null || cooldown > 0f;

        private int ResetIndex => rows.Count;
        private int BackIndex => rows.Count + 1;
        private InputActionAsset Asset => getAsset != null ? getAsset() : null;

        public ControlsPanel(UISettings settings, Func<float, float, InkPanel> makeCard, Func<InputActionAsset> asset)
        {
            s = settings;
            getAsset = asset;
            rows.AddRange(Rebinding.Rebindable);
            float rowHeight = s.keyIconSize + 10f;
            float height = 60f + s.menuTitleSize + 20f + (rows.Count + 4) * rowHeight + 30f +
                           2f * (s.buttonHeight + s.buttonSpacing) + 60f;
            card = makeCard(Width, height);

            var col = new VisualElement { pickingMode = PickingMode.Ignore };
            col.style.flexDirection = FlexDirection.Column;
            col.style.alignItems = Align.Center;
            col.style.paddingTop = 44f;
            col.style.flexGrow = 1f;
            card.Add(col);
            Label title = UIButton.FlowLabel(s, "CONTROLS", s.menuTitleSize, s.ink);
            title.style.marginBottom = 8f;
            col.Add(title);
            hint = UIButton.FlowLabel(s, "", s.labelSize, s.faint);
            hint.style.marginBottom = 16f;
            col.Add(hint);

            table = new VisualElement { pickingMode = PickingMode.Ignore };
            table.style.width = NameWidth + KeyWidth + PadWidth;
            table.style.marginBottom = 20f;
            col.Add(table);

            resetButton = Button(col, "Reset to defaults", s.iconRestart, ResetIndex, ResetAll);
            backButton = Button(col, "Back", s.iconBack, BackIndex, () =>
            {
                Sfx.Play("ui_back");
                Close();
            });

            cursor = new MenuCursor(rows.Count + 2);
            HudView.SetVisible(card, false);
        }

        // ---------------- open / close ----------------

        public void Open(Action closed)
        {
            onClose = closed;
            IsOpen = true;
            changed = false;
            HudView.SetVisible(card, true);
            Rebuild();
            column = 0;
            Select(0);
        }

        public void Close()
        {
            if (!IsOpen) return;
            StopRebinding();
            IsOpen = false;
            HudView.SetVisible(card, false);
            if (changed) OptionsStore.StoreBindings(Asset);
            onClose?.Invoke();
        }

        public void Update(Vector2 navigate, bool submit, bool cancel, float realFrames)
        {
            if (!IsOpen) return;
            card.Refresh();
            if (cooldown > 0f) cooldown -= realFrames;
            if (Busy) return;   // waiting for the new key, or just got it

            int vertical = navigate.y > 0.5f ? -1 : navigate.y < -0.5f ? 1 : 0;
            int horizontal = vertical != 0 ? 0 : navigate.x > 0.5f ? 1 : navigate.x < -0.5f ? -1 : 0;
            if (cursor.Hold(vertical, realFrames, s.menuRepeatDelay, s.menuRepeatInterval))
            {
                Select(cursor.Index);
                Sfx.Play("ui_move");
            }
            if (horizontal != 0 && cursor.Index < rows.Count && column != (horizontal > 0 ? 1 : 0))
            {
                column = horizontal > 0 ? 1 : 0;
                Select(cursor.Index);
                Sfx.Play("ui_move");
            }

            if (cancel)
            {
                Sfx.Play("ui_back");
                Close();
            }
            else if (submit) Activate(cursor.Index);
        }

        // ---------------- rebinding ----------------

        private void Activate(int index)
        {
            if (index == ResetIndex) ResetAll();
            else if (index == BackIndex)
            {
                Sfx.Play("ui_back");
                Close();
            }
            else BeginRebind(index, column);
        }

        private void BeginRebind(int row, int group)
        {
            InputActionAsset asset = Asset;
            InputActionMap map = asset != null ? asset.FindActionMap("Gameplay") : null;
            InputAction action = map != null ? map.FindAction(rows[row]) : null;
            if (action == null || Rebinding.MainIndex(action, Groups[group]) < 0) return;

            Sfx.Play("ui_select");
            rebindRow = row;
            column = group;
            ShowWaiting(row, group);
            rebinding = Rebinding.Start(map, action, Groups[group], gotNew =>
            {
                rebinding = null;
                rebindRow = -1;
                cooldown = 12f;
                if (gotNew)
                {
                    changed = true;
                    Sfx.Play("ui_select");
                }
                else Sfx.Play("ui_back");
                if (IsOpen) Rebuild();
            });
        }

        private void StopRebinding()
        {
            if (rebinding == null) return;
            InputActionRebindingExtensions.RebindingOperation op = rebinding;
            rebinding = null;
            rebindRow = -1;
            op.Cancel();
        }

        private void ResetAll()
        {
            StopRebinding();
            InputActionAsset asset = Asset;
            if (asset == null) return;
            asset.RemoveAllBindingOverrides();
            changed = true;
            Sfx.Play("ui_select");
            Rebuild();
        }

        // ---------------- table ----------------

        /// <summary>Redraws every row from the asset's current bindings (after a rebind or reset).</summary>
        private void Rebuild()
        {
            table.Clear();
            cells.Clear();
            InputActionAsset asset = Asset;
            InputActionMap gameplay = asset != null ? asset.FindActionMap("Gameplay") : null;
            InputActionMap menu = asset != null ? asset.FindActionMap("Menu") : null;
            int size = s.labelSize + 2;

            Fixed("", "KEYBOARD / MOUSE", "GAMEPAD", size, s.faint);
            FixedAction("Move", gameplay?.FindAction("Move"), size);
            for (int i = 0; i < rows.Count; i++)
            {
                InputAction action = gameplay?.FindAction(rows[i]);
                VisualElement row = Row();
                AddCell(row, Cell(NameWidth, false, i, -1), UIButton.FlowLabel(s, Names[rows[i]], size, action != null ? s.ink : s.faint));
                var pair = new VisualElement[2];
                for (int g = 0; g < 2; g++)
                {
                    VisualElement cell = Cell(g == 0 ? KeyWidth : PadWidth, true, i, g);
                    if (action != null) cell.Add(new KeyHint(s, action, Groups[g], size, s.keyIconSize, s.ink));
                    else cell.Add(UIButton.FlowLabel(s, "-", size, s.faint));
                    row.Add(cell);
                    pair[g] = cell;
                }
                cells.Add(pair);
            }
            FixedAction("Pause", menu?.FindAction("Pause"), size);
            Fixed("Combo breaker (50 ink)", "Parry while hit", "Parry while hit", size, s.ink);
            Select(cursor != null ? cursor.Index : 0);
        }

        private void ShowWaiting(int row, int group)
        {
            VisualElement cell = cells[row][group];
            cell.Clear();
            cell.Add(UIButton.FlowLabel(s, group == 0 ? "press a key...  (Esc cancels)" : "press a button...  (Start cancels)",
                                        s.labelSize, s.accent));
            hint.text = "";
        }

        private VisualElement Row()
        {
            var row = new VisualElement { pickingMode = PickingMode.Ignore };
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.height = s.keyIconSize + 10f;
            table.Add(row);
            return row;
        }

        /// <summary>A table cell; rebindable ones can be hovered and clicked.</summary>
        private VisualElement Cell(float width, bool rebindable, int row, int group)
        {
            var cell = new VisualElement { pickingMode = rebindable ? PickingMode.Position : PickingMode.Ignore };
            cell.style.width = width;
            cell.style.height = s.keyIconSize + 6f;
            cell.style.flexDirection = FlexDirection.Row;
            cell.style.alignItems = Align.Center;
            cell.style.paddingLeft = 8f;
            UIButton.SetRadius(cell, s.buttonRadius * 0.6f);
            if (rebindable)
            {
                cell.RegisterCallback<PointerEnterEvent>(_ =>
                {
                    if (Busy) return;
                    column = group;
                    Select(row);
                });
                cell.RegisterCallback<ClickEvent>(_ =>
                {
                    if (!Busy) BeginRebind(row, group);
                });
            }
            return cell;
        }

        /// <summary>Adds a cell to a row with something in it.</summary>
        private static void AddCell(VisualElement row, VisualElement cell, VisualElement content)
        {
            cell.Add(content);
            row.Add(cell);
        }

        private void Fixed(string what, string keyboard, string gamepad, int size, Color color)
        {
            VisualElement row = Row();
            AddCell(row, Cell(NameWidth, false, 0, 0), UIButton.FlowLabel(s, what, size, s.ink));
            AddCell(row, Cell(KeyWidth, false, 0, 0), UIButton.FlowLabel(s, keyboard, size, color));
            AddCell(row, Cell(PadWidth, false, 0, 0), UIButton.FlowLabel(s, gamepad, size, color));
        }

        private void FixedAction(string what, InputAction action, int size)
        {
            VisualElement row = Row();
            AddCell(row, Cell(NameWidth, false, 0, 0), UIButton.FlowLabel(s, what, size, s.faint));
            AddCell(row, Cell(KeyWidth, false, 0, 0), new KeyHint(s, action, "Keyboard", size, s.keyIconSize, s.faint));
            AddCell(row, Cell(PadWidth, false, 0, 0), new KeyHint(s, action, "Gamepad", size, s.keyIconSize, s.faint));
        }

        private void Select(int index)
        {
            if (cursor == null) return;
            cursor.Select(index);
            Color highlight = new Color(s.faint.r, s.faint.g, s.faint.b, 0.55f);
            for (int r = 0; r < cells.Count; r++)
                for (int g = 0; g < 2; g++)
                    cells[r][g].style.backgroundColor = r == cursor.Index && g == column ? highlight : Color.clear;
            resetButton.SetSelected(cursor.Index == ResetIndex);
            backButton.SetSelected(cursor.Index == BackIndex);
            hint.text = cursor.Index < rows.Count
                ? "press to change   (Left / Right: keyboard or gamepad)"
                : "";
        }

        private UIButton Button(VisualElement parent, string text, Texture2D icon, int index, Action action)
        {
            var button = new UIButton(s, text, icon);
            button.Hovered += () =>
            {
                if (Busy) return;
                if (cursor.Index != index) Sfx.Play("ui_move");
                Select(index);
            };
            button.Clicked += () =>
            {
                if (!Busy) action();
            };
            parent.Add(button);
            return button;
        }
    }
}
