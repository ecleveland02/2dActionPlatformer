using System;
using System.IO;
using UnityEngine;

namespace Margin.Save
{
    /// <summary>
    /// Reads and writes the 3 save slots (spec 15) as JSON in persistentDataPath: save_1.json .. save_3.json.
    /// Writes go to a temporary file first and the previous save is kept as .bak, so a crash mid-write (or a
    /// damaged file) never loses more than the last save.
    /// </summary>
    public static class SaveSystem
    {
        public const int SlotCount = 3;

        public static string PathFor(int slot) => Path.Combine(Application.persistentDataPath, $"save_{slot + 1}.json");

        public static bool Exists(int slot) => File.Exists(PathFor(slot)) || File.Exists(PathFor(slot) + ".bak");

        /// <summary>The slot's save, or null if it's empty (or unreadable, which is logged).</summary>
        public static SaveData Load(int slot)
        {
            string path = PathFor(slot);
            return Read(path) ?? Read(path + ".bak");
        }

        private static SaveData Read(string path)
        {
            if (!File.Exists(path)) return null;
            try
            {
                var data = JsonUtility.FromJson<SaveData>(File.ReadAllText(path));
                if (data == null) return null;
                data.Repair();
                return data;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"Couldn't read save {path}: {e.Message}");
                return null;
            }
        }

        public static bool Write(int slot, SaveData data)
        {
            if (slot < 0 || slot >= SlotCount || data == null) return false;
            string path = PathFor(slot), temp = path + ".tmp", backup = path + ".bak";
            try
            {
                data.version = SaveData.CurrentVersion;
                data.savedAt = DateTime.Now.ToString("s");
                File.WriteAllText(temp, JsonUtility.ToJson(data, true));
                if (File.Exists(path))
                {
                    if (File.Exists(backup)) File.Delete(backup);
                    File.Move(path, backup);
                }
                File.Move(temp, path);
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"Couldn't save slot {slot + 1} to {path}: {e.Message}");
                return false;
            }
        }

        public static void Delete(int slot)
        {
            string path = PathFor(slot);
            foreach (string p in new[] { path, path + ".bak", path + ".tmp" })
                if (File.Exists(p)) File.Delete(p);
        }
    }
}
