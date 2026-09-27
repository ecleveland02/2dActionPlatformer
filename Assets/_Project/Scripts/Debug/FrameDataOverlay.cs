using System;
using System.Collections.Generic;
using System.Text;
using Margin.Core;
using Margin.Input;
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
            GUI.Box(new Rect(10, 10, size.x, size.y), content, style);
        }

        private void BuildText()
        {
            text.Clear();
            int frame = GameLoop.Clock?.CurrentFrame ?? 0;

            string mode = GameLoop.Paused ? "<color=#ff6060>PAUSED (F4 steps)</color>"
                        : Controller != null && Controller.SlowMotion ? $"<color=#ffd060>SLOW x{Controller.SlowMotionScale:0.##}</color>"
                        : "running";
            text.AppendLine($"<b>Frame {frame}</b>   {mode}");
            text.AppendLine("F1 boxes  F2 overlay  F3 pause  F4 step  F5 slow");

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
