using Margin.Save;
using NUnit.Framework;
using UnityEngine;

namespace Margin.Tests
{
    /// <summary>Save slots (spec 15): the data survives a JSON round trip, old or damaged files are repaired.</summary>
    public class SaveDataTests
    {
        [Test]
        public void JsonRoundTrip_KeepsEverything()
        {
            var a = new SaveData { scene = "World1", checkpointId = "7 Margin Call", checkpointName = "Margin Call", playFrames = 4567 };
            a.abilities.Add("Dash");
            a.abilities.Add("GrappleLine");
            a.AddBoss("Highlighter");
            var b = JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(a));
            Assert.AreEqual("World1", b.scene);
            Assert.AreEqual("7 Margin Call", b.checkpointId);
            Assert.IsTrue(b.HasAbility("GrappleLine"));
            Assert.IsTrue(b.IsBossBeaten("Highlighter"));
            Assert.AreEqual(4567, b.playFrames);
            Assert.AreEqual(SaveData.StartingWeapon, b.equippedWeapon);
        }

        [Test]
        public void AddBoss_OnlyOnce()
        {
            var d = new SaveData();
            d.AddBoss("Highlighter");
            d.AddBoss("Highlighter");
            d.AddBoss("");
            Assert.AreEqual(1, d.bossesBeaten.Count);
        }

        [Test]
        public void Repair_FillsMissingLists_AndFixesTheWeapon()
        {
            var d = new SaveData { abilities = null, bossesBeaten = null, weaponsUnlocked = null, equippedWeapon = "Nope", playFrames = -5 };
            d.Repair();
            Assert.IsNotNull(d.abilities);
            Assert.IsNotNull(d.bossesBeaten);
            Assert.AreEqual(SaveData.StartingWeapon, d.equippedWeapon);
            Assert.AreEqual(0, d.playFrames);
        }

        [Test]
        public void Playtime_AndNames_ForTheSlotSummary()
        {
            Assert.AreEqual("0:00", SaveData.FormatPlaytime(0));
            Assert.AreEqual("12:34", SaveData.FormatPlaytime((12 * 60 + 34) * 60));
            Assert.AreEqual("1:02:03", SaveData.FormatPlaytime((3600 + 2 * 60 + 3) * 60L));
            Assert.AreEqual("Margin Call", SaveData.CleanName("7 Margin Call"));
            Assert.AreEqual("Ink Pot", SaveData.CleanName("Ink Pot"));
            Assert.AreEqual("World 2", SaveData.SceneTitle("World2"));
            Assert.AreEqual("World 2", SaveData.SceneTitle("World 2"));
            Assert.AreEqual("Title", SaveData.SceneTitle("Title"));
            var d = new SaveData { checkpointName = "Margin Call", playFrames = 60 * 75 };
            d.abilities.Add("GrappleLine");
            Assert.AreEqual("Margin Call   1:15   Grapple Line", d.Summary());
            Assert.AreEqual("Start   0:00", new SaveData().Summary());
        }
    }
}
