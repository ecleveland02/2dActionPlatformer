using System;
using System.Collections.Generic;
using Margin.Audio;
using Margin.Save;
using UnityEngine;
using UnityEngine.UIElements;

namespace Margin.UI
{
    /// <summary>
    /// The Options page (spec 14): audio volumes, screen shake (accessibility, spec 12) and display settings, on a
    /// hand-drawn card like the rest of the menus. Up/Down pick a row, Left/Right change it (held = repeats), Submit
    /// on a choice steps it forward, Cancel or Back returns. With the mouse: hover picks a row, click the arrows.
    /// Changes apply at once (OptionsStore) and are saved to options.json when the page closes.
    /// </summary>
    public sealed class OptionsPanel
    {
        /// <summary>One adjustable line: its label, its current value as text, how to change it, and (for sliders)
        /// how full its bar is.</summary>
        private sealed class Row
        {
            public string Label;
            public Func<string> Value;
            public Action<int> Step;
            public Func<float> Fill;   // null = a choice, not a slider
            public VisualElement Root;
            public Label ValueLabel;
            public InkBar Bar;
        }

        private const float Width = 980f;
        private const float RowHeight = 58f;

        private readonly UISettings s;
        private readonly InkPanel card;
        private readonly List<Row> rows = new List<Row>();
        private readonly UIButton backButton;
        private readonly MenuCursor cursor;
        private readonly MenuCursor repeat = new MenuCursor(1000);   // only used for Left/Right hold timing
        private List<Vector2Int> resolutions = new List<Vector2Int>();
        private int resolutionIndex;
        private Action onClose;

        public InkPanel Card => card;
        public bool IsOpen { get; private set; }

        public OptionsPanel(UISettings settings, Func<float, float, InkPanel> makeCard)
        {
            s = settings;
            AddSlider("Master volume", () => OptionsStore.Current.masterVolume, v => OptionsStore.Current.masterVolume = v);
            AddSlider("Music volume", () => OptionsStore.Current.musicVolume, v => OptionsStore.Current.musicVolume = v);
            AddSlider("Effects volume", () => OptionsStore.Current.sfxVolume, v => OptionsStore.Current.sfxVolume = v);
            AddSlider("Screen shake", () => OptionsStore.Current.screenShake, v => OptionsStore.Current.screenShake = v);
            AddChoice("Window", () => ModeName(OptionsStore.Current.displayMode), dir =>
            {
                OptionsStore.Current.displayMode = (DisplayMode)OptionsData.Cycle((int)OptionsStore.Current.displayMode, dir, 3);
                OptionsStore.NotifyChanged(displayChanged: true);
            });
            AddChoice("Resolution", ResolutionName, dir =>
            {
                if (resolutions.Count == 0) return;
                resolutionIndex = OptionsData.Cycle(resolutionIndex, dir, resolutions.Count);
                OptionsStore.Current.resolutionWidth = resolutions[resolutionIndex].x;
                OptionsStore.Current.resolutionHeight = resolutions[resolutionIndex].y;
                OptionsStore.NotifyChanged(displayChanged: true);
            });
            AddChoice("VSync", () => OptionsStore.Current.vsync ? "On" : "Off", dir =>
            {
                OptionsStore.Current.vsync = !OptionsStore.Current.vsync;
                OptionsStore.NotifyChanged(displayChanged: true);
            });

            float height = 60f + s.menuTitleSize + 30f + rows.Count * RowHeight + 30f + s.buttonHeight + 60f;
            card = makeCard(Width, height);
            var column = new VisualElement { pickingMode = PickingMode.Ignore };
            column.style.flexDirection = FlexDirection.Column;
            column.style.alignItems = Align.Center;
            column.style.paddingTop = 44f;
            column.style.flexGrow = 1f;
            card.Add(column);

            Label title = UIButton.FlowLabel(s, "OPTIONS", s.menuTitleSize, s.ink);
            title.style.marginBottom = 30f;
            column.Add(title);
            for (int i = 0; i < rows.Count; i++) column.Add(BuildRow(rows[i], i));

            backButton = new UIButton(s, "Back", s.iconBack);
            backButton.style.marginTop = 30f;
            backButton.Hovered += () => Select(rows.Count);
            backButton.Clicked += () =>
            {
                Sfx.Play("ui_back");
                Close();
            };
            column.Add(backButton);

            cursor = new MenuCursor(rows.Count + 1);
            HudView.SetVisible(card, false);
        }

        // ---------------- open / close ----------------

        public void Open(Action closed)
        {
            onClose = closed;
            IsOpen = true;
            resolutions = OptionsStore.Resolutions();
            var saved = new Vector2Int(OptionsStore.Current.resolutionWidth, OptionsStore.Current.resolutionHeight);
            resolutionIndex = Mathf.Max(0, resolutions.IndexOf(saved));
            HudView.SetVisible(card, true);
            Select(0);
            RefreshValues();
        }

        public void Close()
        {
            if (!IsOpen) return;
            IsOpen = false;
            HudView.SetVisible(card, false);
            OptionsStore.Save();
            onClose?.Invoke();
        }

        /// <summary>Every rendered frame while open (same inputs as MenuView.Update).</summary>
        public void Update(Vector2 navigate, bool submit, bool cancel, float realFrames)
        {
            if (!IsOpen) return;
            int vertical = navigate.y > 0.5f ? -1 : navigate.y < -0.5f ? 1 : 0;
            int horizontal = vertical != 0 ? 0 : navigate.x > 0.5f ? 1 : navigate.x < -0.5f ? -1 : 0;

            if (cursor.Hold(vertical, realFrames, s.menuRepeatDelay, s.menuRepeatInterval))
            {
                Select(cursor.Index);
                Sfx.Play("ui_move");
            }
            if (repeat.Hold(horizontal, realFrames, s.menuRepeatDelay, s.menuRepeatInterval / 2f) && cursor.Index < rows.Count)
                Change(cursor.Index, horizontal);

            if (cancel || (submit && cursor.Index == rows.Count))
            {
                Sfx.Play("ui_back");
                Close();
                return;
            }
            if (submit && rows[cursor.Index].Fill == null) Change(cursor.Index, 1);

            card.Refresh();
        }

        // ---------------- rows ----------------

        private void AddSlider(string label, Func<float> get, Action<float> set)
        {
            rows.Add(new Row
            {
                Label = label,
                Value = () => OptionsData.Percent(get()),
                Fill = get,
                Step = dir =>
                {
                    set(OptionsData.Step(get(), dir));
                    OptionsStore.NotifyChanged();
                },
            });
        }

        private void AddChoice(string label, Func<string> value, Action<int> step) =>
            rows.Add(new Row { Label = label, Value = value, Step = step });

        private void Change(int index, int direction)
        {
            rows[index].Step(direction);
            Sfx.Play("ui_move");
            RefreshValues();
        }

        private VisualElement BuildRow(Row row, int index)
        {
            var root = new VisualElement { pickingMode = PickingMode.Position };
            root.style.flexDirection = FlexDirection.Row;
            root.style.alignItems = Align.Center;
            root.style.width = Width - 140f;
            root.style.height = RowHeight;
            root.style.paddingLeft = 16f;
            root.style.paddingRight = 16f;
            UIButton.SetRadius(root, s.buttonRadius);
            root.RegisterCallback<PointerEnterEvent>(_ => Select(index));
            row.Root = root;

            Label name = UIButton.FlowLabel(s, row.Label, s.menuItemSize - 4, s.ink);
            name.style.width = 300f;
            root.Add(name);

            root.Add(Arrow("<", () => Change(index, -1)));
            var middle = new VisualElement { pickingMode = PickingMode.Ignore };
            middle.style.flexDirection = FlexDirection.Row;
            middle.style.alignItems = Align.Center;
            middle.style.justifyContent = Justify.Center;
            middle.style.width = 420f;
            if (row.Fill != null)
            {
                row.Bar = new InkBar(s);
                row.Bar.style.width = 300f;
                row.Bar.style.height = 20f;
                row.Bar.style.marginRight = 16f;
                middle.Add(row.Bar);
            }
            row.ValueLabel = UIButton.FlowLabel(s, "", s.menuItemSize - 4, s.ink);
            middle.Add(row.ValueLabel);
            root.Add(middle);
            root.Add(Arrow(">", () => Change(index, 1)));
            return root;
        }

        /// <summary>A clickable &lt; or &gt; that steps the row.</summary>
        private Label Arrow(string text, Action click)
        {
            Label arrow = UIButton.FlowLabel(s, text, s.menuItemSize, s.ink);
            arrow.pickingMode = PickingMode.Position;
            arrow.style.width = 40f;
            arrow.style.unityTextAlign = TextAnchor.MiddleCenter;
            arrow.RegisterCallback<ClickEvent>(_ => click());
            return arrow;
        }

        private void Select(int index)
        {
            cursor.Select(index);
            for (int i = 0; i < rows.Count; i++)
                rows[i].Root.style.backgroundColor = i == cursor.Index ? new Color(s.faint.r, s.faint.g, s.faint.b, 0.45f) : Color.clear;
            backButton.SetSelected(cursor.Index == rows.Count);
        }

        private void RefreshValues()
        {
            foreach (Row row in rows)
            {
                row.ValueLabel.text = row.Value();
                if (row.Bar != null) row.Bar.SetValues(row.Fill(), row.Fill(), 0f);
            }
        }

        private static string ModeName(DisplayMode mode)
        {
            switch (mode)
            {
                case DisplayMode.Fullscreen: return "Fullscreen";
                case DisplayMode.Windowed: return "Windowed";
                default: return "Borderless";
            }
        }

        private string ResolutionName()
        {
            if (resolutions.Count == 0) return "Native";
            Vector2Int r = resolutions[resolutionIndex];
            return r == Vector2Int.zero ? "Native" : $"{r.x} x {r.y}";
        }
    }
}
