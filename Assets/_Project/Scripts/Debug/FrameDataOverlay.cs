using System;
using System.Collections.Generic;
using System.Text;
using Margin.Core;
using Margin.Input;
using Margin.Level;
using Margin.Player;
using UnityEngine;

namespace Margin.DebugTools
{
    /// <summary>
    /// F2 overlay: what the player is doing on this exact tick. Use it with F3/F4 frame-stepping to verify
    /// timing windows (coyote, jump buffer, dash i-frames, skid length) frame by frame.
    /// Drawn with IMGUI (OnGUI), which works with any render pipeline.
    /// </summary>
    public sealed class FrameDataOverlay : MonoBehaviour
    {
        private const int HistoryLength = 10;

        private readonly Queue<string> history = new Queue<string>();
        private readonly StringBuilder text = new StringBuilder(1024);
        private PlayerController player;
        private GUIStyle style;

        public bool Visible { get; set; }
        public DebugController Controller { get; set; }

        public PlayerController Player
        {
            get => player;
            set
            {
                if (player == value) return;
                if (player != null) player.StateChanged -= OnStateChanged;
                player = value;
                history.Clear();
                if (player != null) player.StateChanged += OnStateChanged;
            }
        }

        private void OnDestroy()
        {
            Player = null;
        }

        private void OnStateChanged(PlayerState previous, PlayerState next)
        {
            int frame = GameLoop.Clock?.CurrentFrame ?? 0;
            history.Enqueue($"f{frame}  {previous?.Name ?? "-"} > {next.Name}");
            while (history.Count > HistoryLength) history.Dequeue();
        }

        private void OnGUI()
        {
            if (!Visible) return;

            if (style == null)
            {
                style = new GUIStyle(GUI.skin.box)
                {
                    alignment = TextAnchor.UpperLeft,
                    fontSize = 14,
                    richText = true,
                    padding = new RectOffset(10, 10, 8, 8),
                };
            }

            BuildText();
            var content = new GUIContent(text.ToString());
            Vector2 size = style.CalcSize(content);
            // Top-right: the HUD owns the top-left corner (spec 14).
            GUI.Box(new Rect(Screen.width - size.x - 10, 10, size.x, size.y), content, style);
        }

        private void BuildText()
        {
            text.Clear();
            int frame = GameLoop.Clock?.CurrentFrame ?? 0;

            string mode = GameLoop.FreezeTicks > 0 ? $"<color=#ff9040>HITSTOP (global) {GameLoop.FreezeTicks}</color>"
                        : GameLoop.Paused ? "<color=#ff6060>PAUSED (F4 steps)</color>"
                        : Controller != null && Controller.SlowMotion ? $"<color=#ffd060>SLOW x{Controller.SlowMotionScale:0.##}</color>"
                        : "running";
            text.AppendLine($"<b>Frame {frame}</b>   {mode}");
            text.AppendLine("F1 boxes  F2 overlay  F3 pause  F4 step  F5 slow");
            LevelDirector level = LevelDirector.Instance;
            if (level != null && level.CurrentRoom != null)
                text.AppendLine($"Room     {level.CurrentRoom.Title}" + (level.InTransition ? "   (transition)" : ""));

            if (player == null || player.Data == null || player.CurrentState == null)
            {
                text.Append("(no player)");
                return;
            }

            MovementData d = player.Data;
            var c = player.Body.Collisions;

            text.AppendLine();
            text.AppendLine($"<b>State</b>  {player.CurrentState.Name}   frame {player.FramesInState}");
            text.AppendLine($"Velocity  x {player.Velocity.x,6:F2}   y {player.Velocity.y,6:F2}");
            text.AppendLine($"Grounded {YesNo(c.Grounded)}   one-way {YesNo(c.OnOneWay)}   " +
                            $"wall L {YesNo(c.HitWallLeft)} R {YesNo(c.HitWallRight)}   ceiling {YesNo(c.HitCeiling)}");
            text.AppendLine($"Coyote   {CoyoteText(d)}");
            text.AppendLine($"Sprint   charge {player.SprintCharge}/{d.framesToStartSprint}   {(player.IsSprinting ? "<b>SPRINTING</b>" : "")}");
            if (player.CurrentState is AttackState attack) AppendAttack(attack);
            if (player.InHitstop) text.AppendLine($"<color=#ff9040>Hitstop  {player.Combat.HitstopFrames} frames left</color>");
            if (player.Combat != null)
                text.AppendLine($"Ink      {player.Combat.Ink.Value}/{player.Combat.Ink.Max}   (no hit for {player.Combat.Ink.FramesSinceHit} f)");
            if (player.Health != null)
            {
                var hp = player.Health.Health;
                text.AppendLine($"Health   {hp.Current}/{hp.Max}" + (hp.IsInvulnerable ? $"   <color=#60c0ff>hurt i-frames {hp.InvulnerableFramesLeft}</color>" : ""));
                var taken = player.Health.ComboTaken;
                if (taken.Active) text.AppendLine($"<color=#ff7070>Comboed  {taken.Hits} hits, {taken.Damage} dmg</color>");
            }
            if (player.CurrentState is ParryState parry)
                text.AppendLine(parry.Succeeded ? "<color=#80ff80>PARRY SUCCESS</color>"
                              : parry.IsActive ? "<color=#80ff80>PARRY ACTIVE</color>" : "parry recovery (whiffed)");
            text.AppendLine($"Dash     cooldown {player.DashCooldown}   air dashes {player.AirDashesLeft}   " +
                            $"{(player.IsInvulnerable ? "<color=#60c0ff>INVULNERABLE</color>" : "")}");

            IPlayerInput input = player.Controls;
            if (input != null)
            {
                text.AppendLine($"Input    move ({input.Move.x:F2}, {input.Move.y:F2})   jump held {YesNo(input.JumpHeld)}   down {YesNo(input.DownHeld)}");
                if (input.Buffer != null) AppendBuffer(input.Buffer);
            }

            text.AppendLine();
            text.AppendLine("<b>Recent states</b>");
            foreach (string line in history) text.AppendLine(line);
        }

        private void AppendAttack(AttackState attack)
        {
            Margin.Combat.AttackTiming t = attack.Attack.Timing;
            int f = attack.Frame;
            string phase = t.PhaseAt(f).ToString().ToUpperInvariant();
            string color = phase == "ACTIVE" ? "#ff9040" : phase == "STARTUP" ? "#ffd060" : "#a0a0a0";
            string cancel = t.AllowsAttackCancel(f, attack.HasHit) ? "<color=#80ff80>attacks+moves</color>"
                          : t.AllowsMovementCancel(f, attack.HasHit) ? "<color=#80ff80>jump/dash only</color>"
                          : "closed";
            text.AppendLine($"<b>Attack</b>   {attack.Attack.name}  f{f}/{t.TotalFrames}  <color={color}>{phase}</color>" +
                            $"   hit {YesNo(attack.HasHit)}   cancel {cancel}");
        }

        private string CoyoteText(MovementData d)
        {
            if (player.Grounded) return "on ground";
            if (!player.CoyoteAvailable) return "used (jumped)";
            int f = player.FramesSinceGrounded;
            return f <= d.coyoteFrames ? $"<color=#80ff80>frame {f}/{d.coyoteFrames}</color>" : $"expired (frame {f})";
        }

        private void AppendBuffer(InputBuffer buffer)
        {
            text.Append("Buffer  ");
            foreach (BufferedAction action in Enum.GetValues(typeof(BufferedAction)))
            {
                int age = buffer.FramesSincePress(action);
                int window = buffer.GetWindow(action);
                // Frame numbers follow the project rule: the press tick is frame 1.
                if (age >= 0 && age < window) text.Append($" <color=#80ff80>{action} f{age + 1}/{window}</color>");
            }
            text.AppendLine();
        }

        private static string YesNo(bool value) => value ? "<b>Y</b>" : "n";
    }
}
