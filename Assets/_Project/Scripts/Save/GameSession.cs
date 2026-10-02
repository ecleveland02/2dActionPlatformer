using System;
using System.Collections.Generic;
using Margin.Abilities;
using Margin.Bosses;
using Margin.Level;
using Margin.Player;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Margin.Save
{
    /// <summary>
    /// The playthrough in progress: which save slot it is and its SaveData (spec 15). The title screen starts one
    /// (Begin); the LevelDirector restores it when a world loads (Restore); it saves itself at ink pots (spec: autosave
    /// at checkpoints), and also when a boss is beaten or an ability is gained, so a boss win can't be lost by
    /// quitting before the next ink pot. Playing a world straight from the editor has no slot, so nothing is saved.
    /// </summary>
    public static class GameSession
    {
        public static int Slot { get; private set; } = -1;
        public static SaveData Data { get; private set; }
        public static bool Active => Slot >= 0 && Data != null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Initialize()
        {
            Slot = -1;
            Data = null;
            LevelEvents.CheckpointReached -= OnCheckpoint;
            LevelEvents.CheckpointReached += OnCheckpoint;
            BossEvents.Beaten -= OnBossBeaten;
            BossEvents.Beaten += OnBossBeaten;
            AbilityEvents.Unlocked -= OnAbilityUnlocked;
            AbilityEvents.Unlocked += OnAbilityUnlocked;
        }

        /// <summary>Continues the slot's save, or starts a new game in it if it's empty.</summary>
        public static void Begin(int slot, string firstScene)
        {
            Slot = slot;
            Data = SaveSystem.Load(slot) ?? new SaveData { scene = firstScene };
            if (string.IsNullOrEmpty(Data.scene)) Data.scene = firstScene;
        }

        public static void End()
        {
            Slot = -1;
            Data = null;
        }

        public static bool IsBossBeaten(BossBase boss) => Active && boss != null && Data.IsBossBeaten(boss.SaveId);

        /// <summary>
        /// Called by the LevelDirector as a world starts. Gives the player their saved abilities and returns the ink pot
        /// to start at (null = the scene's normal start). A brand-new game saves straight away, so the slot isn't empty.
        /// </summary>
        public static Checkpoint Restore(PlayerController player, IEnumerable<Checkpoint> checkpoints)
        {
            if (!Active || player == null) return null;
            if (string.IsNullOrEmpty(Data.savedAt))
            {
                Capture(player);   // new game: the player's starting abilities become the save's
                Save();
                return null;
            }

            foreach (Ability a in (Ability[])Enum.GetValues(typeof(Ability)))
                if (Data.HasAbility(a.ToString())) player.Abilities.Unlock(a);
                else Lock(player.Abilities, a);

            if (Data.scene != SceneManager.GetActiveScene().name) return null;
            foreach (Checkpoint c in checkpoints)
                if (c != null && c.Id == Data.checkpointId) return c;
            return null;
        }

        /// <summary>
        /// The player walked out of this world into <paramref name="scene"/> (e.g. World 1 into World 2): the save
        /// keeps their abilities and moves to the new scene's start, so Continue begins there.
        /// </summary>
        public static void EnterScene(string scene, PlayerController player)
        {
            if (!Active) return;
            if (player != null) Capture(player);
            Data.scene = scene;
            Data.checkpointId = "";
            Data.checkpointName = SaveData.SceneTitle(scene);
            Save();
        }

        /// <summary>One game tick played (LevelDirector calls this; it pauses with the game).</summary>
        public static void CountTick()
        {
            if (Active) Data.playFrames++;
        }

        public static void Save()
        {
            if (Active) SaveSystem.Write(Slot, Data);
        }

        // ---------------- autosave triggers ----------------

        private static void OnCheckpoint(Checkpoint checkpoint)
        {
            if (!Active) return;
            Data.scene = SceneManager.GetActiveScene().name;
            Data.checkpointId = checkpoint.Id;
            Room room = Room.Of(checkpoint);
            Data.checkpointName = SaveData.CleanName(room != null ? room.Title : checkpoint.name);
            CaptureAndSave();
        }

        private static void OnBossBeaten(BossBase boss)
        {
            if (!Active) return;
            Data.AddBoss(boss.SaveId);
            CaptureAndSave();
        }

        private static void OnAbilityUnlocked(Ability ability) => CaptureAndSave();

        private static void CaptureAndSave()
        {
            if (!Active) return;   // no save slot (a world played straight from the editor): nothing to save
            LevelDirector director = LevelDirector.Instance;
            if (director != null && director.Player != null) Capture(director.Player);
            Save();
        }

        /// <summary>Copies the player's current abilities into the save.</summary>
        private static void Capture(PlayerController player)
        {
            if (Data == null || player == null || player.Abilities == null) return;
            if (Data.abilities == null) Data.abilities = new List<string>();
            Data.abilities.Clear();
            foreach (Ability a in (Ability[])Enum.GetValues(typeof(Ability)))
                if (player.Abilities.Has(a)) Data.abilities.Add(a.ToString());
            if (string.IsNullOrEmpty(Data.scene)) Data.scene = SceneManager.GetActiveScene().name;
        }

        private static void Lock(AbilityUnlocks unlocks, Ability ability)
        {
            switch (ability)
            {
                case Ability.Dash: unlocks.dash = false; break;
                case Ability.GrappleLine: unlocks.grappleLine = false; break;
                case Ability.DoubleJump: unlocks.doubleJump = false; break;
                case Ability.GroundPound: unlocks.groundPound = false; break;
                case Ability.WallCling: unlocks.wallCling = false; break;
                default: unlocks.carbonCopy = false; break;
            }
        }
    }
}
