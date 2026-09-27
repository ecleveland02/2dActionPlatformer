using System.Collections.Generic;

namespace Margin.UI
{
    /// <summary>
    /// Which key/mouse picture (Art/UI/InputIcons) shows an Input System binding (pure C# for testing).
    /// Icon names are the pack's file names without ".png": "f", "space", "shift", "mouse-left", "keyboard-wasd"...
    /// Returns null when there's no picture (gamepad buttons, unusual keys): callers fall back to text.
    /// </summary>
    public static class KeyIcons
    {
        // Input System key names that differ from the icon file names.
        private static readonly Dictionary<string, string> Renamed = new Dictionary<string, string>
        {
            ["escape"] = "esc", ["leftShift"] = "shift", ["rightShift"] = "shift", ["shift"] = "shift",
            ["leftCtrl"] = "ctrl", ["rightCtrl"] = "ctrl", ["ctrl"] = "ctrl",
            ["leftAlt"] = "alt", ["rightAlt"] = "alt", ["alt"] = "alt",
            ["upArrow"] = "arrow-up", ["downArrow"] = "arrow-down", ["leftArrow"] = "arrow-left", ["rightArrow"] = "arrow-right",
            ["enter"] = "enter", ["numpadEnter"] = "enter", ["space"] = "space", ["tab"] = "tab", ["backspace"] = "backspace",
            ["delete"] = "del", ["insert"] = "ins", ["home"] = "home", ["end"] = "end", ["pageUp"] = "pgup", ["pageDown"] = "pgdn",
            ["capsLock"] = "caps", ["printScreen"] = "prtsc", ["scrollLock"] = "scrlk", ["pause"] = "pause",
            ["backquote"] = "tilde", ["minus"] = "hyphen", ["equals"] = "equals", ["leftBracket"] = "bracket-open",
            ["rightBracket"] = "bracket-close", ["backslash"] = "backward-slash", ["slash"] = "forward-slash",
            ["semicolon"] = "semi-colon", ["quote"] = "quote", ["comma"] = "comma", ["period"] = "dot",
            ["contextMenu"] = "context", ["leftMeta"] = "windows", ["rightMeta"] = "windows",
        };

        /// <summary>
        /// Icon name for one control path like "&lt;Keyboard&gt;/f" or "&lt;Mouse&gt;/leftButton"; null if none.
        /// </summary>
        public static string ForPath(string controlPath)
        {
            if (string.IsNullOrEmpty(controlPath)) return null;
            int slash = controlPath.IndexOf('/');
            if (slash < 0 || slash == controlPath.Length - 1) return null;
            string device = controlPath.Substring(0, slash);
            string control = controlPath.Substring(slash + 1);

            if (device == "<Mouse>")
            {
                switch (control)
                {
                    case "leftButton": return "mouse-left";
                    case "rightButton": return "mouse-right";
                    case "middleButton": return "mouse-middle";
                    default: return null;
                }
            }
            if (device != "<Keyboard>") return null;

            if (Renamed.TryGetValue(control, out string name)) return name;
            if (control.Length == 1 && char.IsLetter(control[0])) return control.ToLowerInvariant();   // a..z
            if (control.StartsWith("digit") && control.Length == 6 && char.IsDigit(control[5])) return control.Substring(5);
            if (control.Length >= 2 && control.Length <= 3 && control[0] == 'f' && int.TryParse(control.Substring(1), out int n) && n >= 1 && n <= 12)
                return control;   // f1..f12
            return null;
        }

        /// <summary>
        /// Icon for a four-way composite (up, down, left, right parts): the pack's WASD or arrow-cluster picture.
        /// </summary>
        public static string ForComposite(IList<string> partPaths)
        {
            if (partPaths == null || partPaths.Count == 0) return null;
            var names = new HashSet<string>();
            foreach (string path in partPaths) names.Add(ForPath(path) ?? "?");
            if (names.SetEquals(new[] { "w", "a", "s", "d" })) return "keyboard-wasd";
            if (names.SetEquals(new[] { "arrow-up", "arrow-down", "arrow-left", "arrow-right" })) return "keyboard-arrows";
            return null;
        }
    }
}
