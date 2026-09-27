using Margin.Core;
using Margin.Player;
using UnityEngine;
using UnityEngine.UIElements;

namespace Margin.UI
{
    /// <summary>
    /// The in-game HUD (spec 14), built from code into the UI root:
    /// - top-left paper card: health bar (red damage chip, pulses red when low) and ink meter below it, with a notch
    ///   at the combo breaker / special cost and "REDRAW" when full;
    /// - combo counter next to it while the player is being comboed, with a prompt when the breaker is affordable;
    /// - boss bar at the bottom centre while an IBossBarSource wants it.
    /// </summary>
    public sealed class HudView
    {
        private readonly UISettings s;
        private readonly VisualElement root;
        private readonly InkPanel card;
        private readonly InkBar healthBar, inkBar, bossBar;
        private readonly Label healthValue, inkValue, redraw, combo, breakPrompt, bossName;
        private readonly VisualElement bossRoot;
        private readonly ChipBar healthChip = new ChipBar(), bossChip = new ChipBar();

        private PlayerController player;
        private IBossBarSource boss;
        private int lastHealth = -1;
        private float shakeFrames;
        private float pulseClock;
        private float inkNotch = -1f;

        public HudView(VisualElement parent, UISettings settings)
        {
            s = settings;
            root = Layer(parent);

            // ---- Health and ink card (top-left) ----
            float w = s.barWidth + 48f;
            float healthY = 16f + s.labelSize + 8f;
            float inkLabelY = healthY + s.healthBarHeight + 12f;
            float inkY = inkLabelY + s.labelSize + 6f;
            float h = inkY + s.inkBarHeight + 22f;

            card = new InkPanel(s);
            Place(card, s.hudMargin.x, s.hudMargin.y, w, h);
            root.Add(card);

            card.Add(Text("HEALTH", s.labelSize, s.ink, 24f, 14f));
            healthValue = Text("", s.labelSize, s.ink, 24f, 14f, s.barWidth);
            healthValue.style.unityTextAlign = TextAnchor.UpperRight;
            card.Add(healthValue);

            healthBar = new InkBar(s);
            Place(healthBar, 18f, healthY, s.barWidth + 12f, s.healthBarHeight + 6f);
            card.Add(healthBar);

            card.Add(Text("INK", s.labelSize, s.ink, 24f, inkLabelY));
            inkValue = Text("", s.labelSize, s.ink, 24f, inkLabelY, s.barWidth);
            inkValue.style.unityTextAlign = TextAnchor.UpperRight;
            card.Add(inkValue);
            redraw = Text("REDRAW", s.labelSize, s.accent, 24f, inkLabelY, s.barWidth);
            redraw.style.unityTextAlign = TextAnchor.UpperCenter;
            card.Add(redraw);

            inkBar = new InkBar(s);
            Place(inkBar, 18f, inkY, s.barWidth + 12f, s.inkBarHeight + 6f);
            card.Add(inkBar);

            // ---- Combo counter (right of the card) ----
            combo = Text("", s.comboSize, s.accent, s.hudMargin.x + w + 12f, s.hudMargin.y + 10f, 360f);
            root.Add(combo);
            breakPrompt = Text("PARRY TO BREAK", s.labelSize, s.ink, s.hudMargin.x + w + 14f, s.hudMargin.y + 20f + s.comboSize, 360f);
            root.Add(breakPrompt);

            // ---- Boss bar (bottom centre) ----
            bossRoot = new VisualElement { pickingMode = PickingMode.Ignore };
            bossRoot.style.position = Position.Absolute;
            bossRoot.style.left = Length.Percent(50f);
            bossRoot.style.marginLeft = -s.bossBarWidth * 0.5f;
            bossRoot.style.bottom = s.bossBarBottom;
            bossRoot.style.width = s.bossBarWidth;
            bossRoot.style.height = s.bossBarHeight + s.labelSize + 20f;
            root.Add(bossRoot);
            bossName = Text("", s.labelSize + 4, s.ink, 0f, 0f, s.bossBarWidth);
            bossName.style.unityTextAlign = TextAnchor.UpperCenter;
            bossRoot.Add(bossName);
            bossBar = new InkBar(s);
            Place(bossBar, 0f, s.labelSize + 12f, s.bossBarWidth, s.bossBarHeight + 6f);
            bossRoot.Add(bossBar);

            SetVisible(card, false);
            SetVisible(combo, false);
            SetVisible(breakPrompt, false);
            SetVisible(bossRoot, false);
        }

        /// <summary>Called every rendered frame. frames = gameplay frames that passed (0 while paused).</summary>
        public void Update(PlayerController currentPlayer, float frames)
        {
            if (currentPlayer != player)
            {
                player = currentPlayer;
                lastHealth = -1;
            }

            bool hasPlayer = player != null && player.Health != null && player.Combat != null;
            SetVisible(card, hasPlayer);
            if (hasPlayer) UpdatePlayer(frames);
            else
            {
                SetVisible(combo, false);
                SetVisible(breakPrompt, false);
            }
            UpdateBoss(frames);

            card.Refresh();
            healthBar.Refresh();
            inkBar.Refresh();
            bossBar.Refresh();
        }

        private void UpdatePlayer(float frames)
        {
            Margin.Combat.Health hp = player.Health.Health;
            Margin.Combat.InkMeter ink = player.Combat.Ink;

            // A respawn (or first sight of this player) snaps the bar; losing health leaves a chip and shakes the card.
            if (lastHealth < 0 || hp.Current > lastHealth) healthChip.Snap(hp.Fraction);
            else if (hp.Current < lastHealth)
            {
                healthChip.Set(hp.Fraction, s.chipHoldFrames);
                shakeFrames = s.hurtShakeFrames;
            }
            lastHealth = hp.Current;
            healthChip.Tick(frames, s.chipDrainPerFrame);

            bool low = hp.Current > 0 && hp.Fraction <= s.lowHealth;
            pulseClock += frames;
            float pulse = low ? 0.5f + 0.5f * Mathf.Sin(pulseClock / s.lowHealthPulseFrames * Mathf.PI * 2f) : 0f;
            healthBar.SetValues(healthChip.Value, healthChip.Trail, pulse);
            healthValue.text = $"{hp.Current} / {hp.Max}";

            float notch = player.Combat.Settings != null ? player.Combat.Settings.comboBreakerInkCost / (float)Mathf.Max(1, ink.Max) : 0.5f;
            if (!Mathf.Approximately(notch, inkNotch))
            {
                inkNotch = notch;
                inkBar.SetNotches(notch);
            }
            inkBar.SetValues(ink.Fraction, ink.Fraction, 0f);
            inkValue.text = $"{ink.Value} / {ink.Max}";
            SetVisible(redraw, ink.Value >= ink.Max);

            // Hurt shake: the card jitters left/right, fading out.
            if (shakeFrames > 0f)
            {
                shakeFrames = Mathf.Max(0f, shakeFrames - frames);
                float amount = s.hurtShake * shakeFrames / Mathf.Max(1, s.hurtShakeFrames);
                float side = ((int)shakeFrames % 2 == 0) ? 1f : -1f;
                card.style.translate = new Translate(side * amount, 0f, 0f);
            }
            else card.style.translate = new Translate(0f, 0f, 0f);

            // Combo counter while being comboed (spec 6.7), with the breaker prompt when it's affordable.
            bool comboed = player.Health.ComboTaken.Active;
            SetVisible(combo, comboed);
            if (comboed)
            {
                int hits = player.Health.ComboTaken.Hits;
                combo.text = hits == 1 ? "1 HIT" : $"{hits} HITS";
            }
            bool canBreak = comboed && player.Combat.Settings != null && ink.CanSpend(player.Combat.Settings.comboBreakerInkCost);
            SetVisible(breakPrompt, canBreak);
        }

        private void UpdateBoss(float frames)
        {
            IBossBarSource current = BossBars.Current;
            if (current != boss)
            {
                boss = current;
                if (boss != null)
                {
                    bossChip.Snap(boss.HealthFraction);
                    bossName.text = boss.BossName;
                    var marks = new float[boss.PhaseMarks?.Count ?? 0];
                    for (int i = 0; i < marks.Length; i++) marks[i] = boss.PhaseMarks[i];
                    bossBar.SetNotches(marks);
                }
            }
            SetVisible(bossRoot, boss != null);
            if (boss == null) return;
            bossChip.Set(boss.HealthFraction, s.chipHoldFrames);
            bossChip.Tick(frames, s.chipDrainPerFrame);
            bossBar.SetValues(bossChip.Value, bossChip.Trail, 0f);
        }

        // ---------------- building helpers ----------------

        /// <summary>A full-screen layer that lets clicks pass through to whatever is below.</summary>
        internal static VisualElement Layer(VisualElement parent)
        {
            var layer = new VisualElement { pickingMode = PickingMode.Ignore };
            layer.style.position = Position.Absolute;
            layer.style.left = 0f;
            layer.style.top = 0f;
            layer.style.right = 0f;
            layer.style.bottom = 0f;
            parent.Add(layer);
            return layer;
        }

        internal static void Place(VisualElement e, float x, float y, float width, float height)
        {
            e.style.position = Position.Absolute;
            e.style.left = x;
            e.style.top = y;
            e.style.width = width;
            e.style.height = height;
        }

        internal Label Text(string text, int size, Color color, float x, float y, float width = -1f) =>
            MarginUI.MakeLabel(s, text, size, color, x, y, width);

        internal static void SetVisible(VisualElement e, bool visible) =>
            e.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
    }
}
