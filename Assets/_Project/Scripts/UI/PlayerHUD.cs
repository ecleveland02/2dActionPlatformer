using Margin.Core;
using Margin.Player;
using UnityEngine;

namespace Margin.UI
{
    /// <summary>
    /// Placeholder HUD (spec 14): the ink meter, with a notch at 50 (the special's cost).
    /// Health joins it above the ink bar in the next chunk. The hand-drawn HUD comes in Milestone 5.
    /// Drawn with IMGUI so it needs no Canvas setup. Added automatically to gameplay scenes.
    /// </summary>
    public sealed class PlayerHUD : MonoBehaviour
    {
        private static readonly Color Paper = new Color32(0xF2, 0xEE, 0xE3, 0xE6);
        private static readonly Color Ink = new Color32(0x1A, 0x1A, 0x1A, 0xFF);
        private static readonly Color Faint = new Color32(0x9A, 0x96, 0x8C, 0xFF);

        private PlayerController player;
        private float nextSearch;
        private Texture2D pixel;
        private GUIStyle label;

        private void OnDestroy()
        {
            if (pixel != null) Destroy(pixel);
        }

        private void Update()
        {
            if (player != null || Time.unscaledTime < nextSearch) return;
            nextSearch = Time.unscaledTime + 1f;
            player = SceneQuery.FindFirst<PlayerController>();
        }

        private void OnGUI()
        {
            if (player == null || player.Combat == null) return;
            if (pixel == null)
            {
                pixel = new Texture2D(1, 1) { hideFlags = HideFlags.DontSave };
                pixel.SetPixel(0, 0, Color.white);
                pixel.Apply();
                label = new GUIStyle(GUI.skin.label) { fontSize = 13, fontStyle = FontStyle.Bold };
                label.normal.textColor = Ink;
            }

            var ink = player.Combat.Ink;
            // Bottom-left for now so it never overlaps the F2 debug overlay (top-left). Moves top-left in Milestone 5.
            float x = 16f, y = Screen.height - 46f, w = 220f, h = 14f;

            Fill(new Rect(x - 6f, y - 22f, w + 12f, h + 30f), Paper);
            GUI.Label(new Rect(x, y - 20f, w, 18f), $"INK  {ink.Value}/{ink.Max}", label);
            Fill(new Rect(x, y, w, h), Faint);
            Fill(new Rect(x, y, w * ink.Fraction, h), Ink);
            // Notch at 50: enough for the special.
            Fill(new Rect(x + w * 0.5f - 1f, y - 3f, 2f, h + 6f), Paper);
        }

        private void Fill(Rect rect, Color color)
        {
            Color old = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, pixel);
            GUI.color = old;
        }
    }
}
