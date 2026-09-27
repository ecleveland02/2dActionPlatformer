using System.Collections.Generic;
using System.Text.RegularExpressions;
using Margin.Audio;
using UnityEditor;
using UnityEngine;

namespace Margin.EditorTools
{
    /// <summary>
    /// Menu: Margin > Set Up Audio. Creates Data/Audio/SoundBank and fills it from the sound files under
    /// Assets/_Project/Audio: "hit_light_2.wav" becomes variant 2 of the sound "hit_light"; music is found by name
    /// (music_world1, music_boss, music_boss_layer). Only adds what's missing, so volumes you tuned are kept.
    /// Also run by the level builders.
    /// </summary>
    public static class StarterAudio
    {
        private const string AudioFolder = "Assets/_Project/Audio";
        private const string BankFolder = "Assets/_Project/Data/Audio";
        private const string BankPath = BankFolder + "/SoundBank.asset";

        // Starting mix per sound: volume (1 = as delivered) and random pitch spread (spec 13: hits +-5%).
        // boss_dash_tell is delivered ~10 dB louder on average than the rest, so it starts lower.
        private static readonly Dictionary<string, (float volume, float pitch)> Mix = new Dictionary<string, (float, float)>
        {
            ["step"] = (0.8f, 0.06f), ["jump"] = (0.8f, 0.04f), ["land"] = (0.9f, 0.04f), ["dash"] = (0.9f, 0.03f),
            ["swing_light"] = (0.9f, 0.05f), ["swing_heavy"] = (1f, 0.05f),
            ["hit_light"] = (1f, 0.05f), ["hit_heavy"] = (1f, 0.05f),
            ["parry"] = (0.9f, 0.02f), ["player_hurt"] = (1f, 0.04f), ["enemy_defeat"] = (0.9f, 0.05f),
            ["grapple_throw"] = (0.8f, 0.04f), ["grapple_hook"] = (0.9f, 0.04f),
            ["boss_dash_tell"] = (0.4f, 0f),
            ["ui_move"] = (0.7f, 0.02f), ["ui_select"] = (0.8f, 0f), ["ui_back"] = (0.8f, 0f),
        };

        [MenuItem("Margin/Set Up Audio")]
        public static void SetUpFromMenu()
        {
            SoundBank bank = EnsureCreated();
            EditorGUIUtility.PingObject(bank);
            Debug.Log($"Sound bank ready: {bank.sounds.Count} sounds, music: {(bank.worldMusic != null ? bank.worldMusic.name : "none")}, " +
                      $"{(bank.bossMusic != null ? bank.bossMusic.name : "none")}, {(bank.bossLayer != null ? bank.bossLayer.name : "none")}.");
        }

        internal static SoundBank EnsureCreated()
        {
            StickFigureRigEditor.EnsureFolder(BankFolder);
            var bank = StarterCombat.LoadOrCreate<SoundBank>(BankPath, out _);
            var variant = new Regex(@"^(.*?)(?:_(\d+))?$");

            var found = new SortedDictionary<string, SortedDictionary<int, AudioClip>>();
            foreach (string guid in AssetDatabase.FindAssets("t:AudioClip", new[] { AudioFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.Contains("/previews/")) continue;   // audition files, not game sounds
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                if (clip == null) continue;
                string name = clip.name;
                if (name == "music_world1" && bank.worldMusic == null) { bank.worldMusic = clip; continue; }
                if (name == "music_boss" && bank.bossMusic == null) { bank.bossMusic = clip; continue; }
                if (name == "music_boss_layer" && bank.bossLayer == null) { bank.bossLayer = clip; continue; }
                if (name.StartsWith("music_")) continue;
                Match m = variant.Match(name);
                string id = m.Groups[1].Value;
                int index = m.Groups[2].Success ? int.Parse(m.Groups[2].Value) : 0;
                if (!found.TryGetValue(id, out var list)) found[id] = list = new SortedDictionary<int, AudioClip>();
                list[index] = clip;
            }

            foreach (var entry in found)
            {
                SoundBank.Sound sound = bank.sounds.Find(s => s.id == entry.Key);
                if (sound == null)
                {
                    sound = new SoundBank.Sound { id = entry.Key };
                    if (Mix.TryGetValue(entry.Key, out var mix))
                    {
                        sound.volume = mix.volume;
                        sound.pitchVariance = mix.pitch;
                    }
                    bank.sounds.Add(sound);
                }
                if (sound.variants.Count == 0) sound.variants.AddRange(entry.Value.Values);
            }
            EditorUtility.SetDirty(bank);
            AssetDatabase.SaveAssets();
            return bank;
        }
    }

    /// <summary>
    /// Import settings for the game's audio, applied when a file is first imported: music streams from disk
    /// (a 2-minute WAV would otherwise sit in memory decompressed, ~46 MB), sound effects load decompressed so
    /// they start instantly. Change them freely in the Inspector afterwards; this only runs on first import.
    /// </summary>
    public sealed class MarginAudioImport : AssetPostprocessor
    {
        private void OnPreprocessAudio()
        {
            if (!assetPath.StartsWith("Assets/_Project/Audio/") || !assetImporter.importSettingsMissing) return;
            var importer = (AudioImporter)assetImporter;
            AudioImporterSampleSettings settings = importer.defaultSampleSettings;
            bool music = assetPath.Contains("/music/") || assetPath.Contains("/previews/");
            settings.loadType = music ? AudioClipLoadType.Streaming : AudioClipLoadType.DecompressOnLoad;
            settings.compressionFormat = AudioCompressionFormat.Vorbis;
            settings.quality = music ? 0.6f : 0.8f;
            importer.defaultSampleSettings = settings;
            importer.loadInBackground = music;
        }
    }
}
