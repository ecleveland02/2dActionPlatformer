using System;
using System.Collections.Generic;
using Margin.Audio;
using Margin.Save;
using UnityEngine;
using UnityEngine.UIElements;

namespace Margin.UI
{
    /// <summary>
    /// The title screen's save slots (spec 15: 3 slots). Each slot shows its last ink pot and play time, or "New Game"
    /// when empty; picking one continues it or starts a new game there. "Erase" switches to erase mode: picking a
    /// saved slot asks once more ("press again to erase") before deleting it. Keyboard, gamepad and mouse.
    /// </summary>
    public sealed class SlotPanel : IMenuPanel
    {
        private const float Width = 980f;
        private const float SlotWidth = 820f;

        private readonly UISettings s;
        private readonly InkPanel card;
        private readonly Action<int> play;
        private readonly List<UIButton> buttons = new List<UIButton>();   // slots, then Erase, then Back
        private readonly MenuCursor cursor;
        private readonly int eraseIndex, backIndex;
        private Action onClose;
        private bool eraseMode;
        private int pendingErase = -1;
        private UIButton pressed;

        public InkPanel Card => card;
        public bool IsOpen { get; private set; }
        public bool Busy => false;

        /// <param name="playSlot">Called with the slot number (0-2) to continue or start.</param>
        public SlotPanel(UISettings settings, Func<float, float, InkPanel> makeCard, Action<int> playSlot)
        {
            s = settings;
            play = playSlot;
            float slotHeight = s.buttonHeight + 16f;
            float height = 60f + s.menuTitleSize + 30f + SaveSystem.SlotCount * (slotHeight + s.buttonSpacing) +
                           2f * (s.buttonHeight + s.buttonSpacing) + 70f;
            card = makeCard(Width, height);

            var column = new VisualElement { pickingMode = PickingMode.Ignore };
            column.style.flexDirection = FlexDirection.Column;
            column.style.alignItems = Align.Center;
            column.style.paddingTop = 44f;
            column.style.flexGrow = 1f;
            card.Add(column);
            Label title = UIButton.FlowLabel(s, "CHOOSE A NOTEBOOK", s.menuTitleSize, s.ink);
            title.style.marginBottom = 30f;
            column.Add(title);

            for (int i = 0; i < SaveSystem.SlotCount; i++)
            {
                UIButton slot = AddButton(column, "", s.iconPlay);
                slot.style.width = SlotWidth;
                slot.style.height = slotHeight;
            }
            eraseIndex = buttons.Count;
            AddButton(column, "Erase a slot", s.iconQuit).style.marginTop = 16f;
            backIndex = buttons.Count;
            AddButton(column, "Back", s.iconBack);

            cursor = new MenuCursor(buttons.Count);
            HudView.SetVisible(card, false);
        }

        public void Open(Action closed)
        {
            onClose = closed;
            IsOpen = true;
            eraseMode = false;
            pendingErase = -1;
            HudView.SetVisible(card, true);
            RefreshSlots();
            Select(FirstSaved());
        }

        public void Close()
        {
            if (!IsOpen) return;
            IsOpen = false;
            HudView.SetVisible(card, false);
            onClose?.Invoke();
        }

        public void Update(Vector2 navigate, bool submit, bool cancel, float realFrames)
        {
            if (!IsOpen) return;
            if (pressed != null)
            {
                pressed.SetPressed(false);
                pressed = null;
            }

            int direction = navigate.y > 0.5f ? -1 : navigate.y < -0.5f ? 1 : 0;
            if (cursor.Hold(direction, realFrames, s.menuRepeatDelay, s.menuRepeatInterval))
            {
                Select(cursor.Index);
                Sfx.Play("ui_move");
            }

            if (submit)
            {
                pressed = buttons[cursor.Index];
                pressed.SetPressed(true);
                Activate(cursor.Index);
            }
            else if (cancel)
            {
                Sfx.Play("ui_back");
                if (eraseMode) SetEraseMode(false);
                else Close();
                return;
            }
            card.Refresh();
        }

        // ---------------- actions ----------------

        private void Activate(int index)
        {
            if (index == backIndex)
            {
                Sfx.Play("ui_back");
                Close();
                return;
            }
            if (index == eraseIndex)
            {
                Sfx.Play("ui_select");
                SetEraseMode(!eraseMode);
                return;
            }

            if (!eraseMode)
            {
                Sfx.Play("ui_select");
                play?.Invoke(index);
                return;
            }

            // Erase mode: an empty slot has nothing to erase; a saved one needs a second press.
            if (!SaveSystem.Exists(index)) return;
            if (pendingErase != index)
            {
                pendingErase = index;
                Sfx.Play("ui_move");
                RefreshSlots();
                return;
            }
            SaveSystem.Delete(index);
            Sfx.Play("ui_back");
            pendingErase = -1;
            SetEraseMode(false);
        }

        private void SetEraseMode(bool on)
        {
            eraseMode = on;
            pendingErase = -1;
            buttons[eraseIndex].SetText(on ? "Erase: pick a slot (Back to stop)" : "Erase a slot");
            RefreshSlots();
        }

        // ---------------- display ----------------

        private void RefreshSlots()
        {
            for (int i = 0; i < SaveSystem.SlotCount; i++)
            {
                SaveData data = SaveSystem.Load(i);
                string text = $"Slot {i + 1}   " + (data != null ? data.Summary() : "New Game");
                if (pendingErase == i) text = $"Slot {i + 1}   press again to ERASE";
                else if (eraseMode && data != null) text = $"Erase slot {i + 1}?   " + data.Summary();
                buttons[i].SetText(text);
            }
        }

        private int FirstSaved()
        {
            for (int i = 0; i < SaveSystem.SlotCount; i++)
                if (SaveSystem.Exists(i)) return i;
            return 0;
        }

        private void Select(int index)
        {
            if (index != cursor.Index && pendingErase >= 0)
            {
                pendingErase = -1;   // moving away cancels the erase confirmation
                RefreshSlots();
            }
            cursor.Select(index);
            for (int i = 0; i < buttons.Count; i++) buttons[i].SetSelected(i == cursor.Index);
        }

        private UIButton AddButton(VisualElement column, string text, Texture2D icon)
        {
            int index = buttons.Count;
            var button = new UIButton(s, text, icon);
            button.Hovered += () =>
            {
                if (cursor.Index != index) Sfx.Play("ui_move");
                Select(index);
            };
            button.Clicked += () => Activate(index);
            column.Add(button);
            buttons.Add(button);
            return button;
        }
    }
}
