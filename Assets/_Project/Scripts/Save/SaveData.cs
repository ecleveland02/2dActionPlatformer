using System;
using System.Collections.Generic;

namespace Margin.Save
{
    /// <summary>
    /// One save slot (spec 15): unlocked abilities, unlocked and equipped weapons, the last checkpoint, beaten bosses
    /// and play time. Plain data so it can be unit tested and written as JSON. Options are NOT in here: they belong to
    /// the player, not a playthrough (OptionsStore). Names are stable ids: Ability enum names, weapon asset names,
    /// boss data asset names, checkpoint ids (the ink pot's room title by default).
    /// </summary>
    [Serializable]
    public sealed class SaveData
    {
        public const int CurrentVersion = 1;
        public const string StartingWeapon = "BrushKatana";

        public int version = CurrentVersion;
        /// <summary>Scene of the last checkpoint (where Continue loads).</summary>
        public string scene;
        public string checkpointId;
        /// <summary>Shown on the slot, e.g. "Margin Call".</summary>
        public string checkpointName;
        public List<string> abilities = new List<string>();
        public List<string> weaponsUnlocked = new List<string> { StartingWeapon };
        public string equippedWeapon = StartingWeapon;
        public List<string> bossesBeaten = new List<string>();
        /// <summary>Game ticks played (60 per second; pauses and menus don't count).</summary>
        public long playFrames;
        /// <summary>When it was last saved (ISO 8601, local time).</summary>
        public string savedAt;

        public bool HasAbility(string ability) => abilities != null && abilities.Contains(ability);
        public bool IsBossBeaten(string boss) => !string.IsNullOrEmpty(boss) && bossesBeaten != null && bossesBeaten.Contains(boss);

        public void AddBoss(string boss)
        {
            if (!string.IsNullOrEmpty(boss) && !bossesBeaten.Contains(boss)) bossesBeaten.Add(boss);
        }

        /// <summary>Fixes anything missing from an older or hand-edited file.</summary>
        public void Repair()
        {
            if (abilities == null) abilities = new List<string>();
            if (bossesBeaten == null) bossesBeaten = new List<string>();
            if (weaponsUnlocked == null || weaponsUnlocked.Count == 0) weaponsUnlocked = new List<string> { StartingWeapon };
            if (string.IsNullOrEmpty(equippedWeapon) || !weaponsUnlocked.Contains(equippedWeapon)) equippedWeapon = weaponsUnlocked[0];
            if (playFrames < 0) playFrames = 0;
        }

        /// <summary>Play time as "m:ss", or "h:mm:ss" from an hour on.</summary>
        public static string FormatPlaytime(long frames)
        {
            long seconds = Math.Max(0, frames) / 60;
            long h = seconds / 3600, m = seconds / 60 % 60, s = seconds % 60;
            return h > 0 ? $"{h}:{m:00}:{s:00}" : $"{m}:{s:00}";
        }

        /// <summary>A room title without its number: "7 Margin Call" becomes "Margin Call".</summary>
        public static string CleanName(string title)
        {
            if (string.IsNullOrEmpty(title)) return "";
            int i = 0;
            while (i < title.Length && char.IsDigit(title[i])) i++;
            return i > 0 && i < title.Length && title[i] == ' ' ? title.Substring(i + 1) : title;
        }

        /// <summary>The slot's one-line summary, e.g. "Margin Call   0:12:34   Grapple Line".</summary>
        public string Summary()
        {
            string where = string.IsNullOrEmpty(checkpointName) ? "Start" : checkpointName;
            string text = where + "   " + FormatPlaytime(playFrames);
            if (HasAbility("GrappleLine")) text += "   Grapple Line";
            return text;
        }
    }
}
