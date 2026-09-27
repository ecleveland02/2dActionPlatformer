using System;
using System.Collections.Generic;
using UnityEngine;

namespace Margin.Audio
{
    /// <summary>
    /// Every sound and music track the game plays (spec 13), by id. Sounds with several variants
    /// (step_1..3) pick one at random, never the same twice in a row. Volumes and pitch spread are per sound.
    /// Filled from Assets/_Project/Audio by Margin > Set Up Audio (only empty entries; your tuning is kept).
    /// </summary>
    [CreateAssetMenu(menuName = "Margin/Sound Bank", fileName = "SoundBank")]
    public sealed class SoundBank : ScriptableObject
    {
        [Serializable]
        public sealed class Sound
        {
            public string id;
            public List<AudioClip> variants = new List<AudioClip>();
            [Tooltip("Playback volume for this sound (1 = as recorded).")]
            [Range(0f, 2f)] public float volume = 1f;
            [Tooltip("Random pitch change each play, e.g. 0.05 = +-5% (spec 13 for hits).")]
            [Range(0f, 0.3f)] public float pitchVariance;
        }

        [Header("Mix")]
        [Range(0f, 1f)] public float masterVolume = 1f;
        [Range(0f, 1f)] public float sfxVolume = 0.8f;
        [Range(0f, 1f)] public float musicVolume = 0.55f;

        [Header("Sounds")]
        public List<Sound> sounds = new List<Sound>();

        [Header("Music")]
        public AudioClip worldMusic;
        [Tooltip("Boss track. Its layer must be the same length and tempo: both start together, the layer silent.")]
        public AudioClip bossMusic;
        public AudioClip bossLayer;
        [Min(1f)] public float bossBpm = 128f;
        [Tooltip("Bars the boss layer takes to fade in when phase 2 starts.")]
        [Min(0f)] public float layerFadeBars = 2f;
        [Tooltip("Seconds to crossfade between the world and boss tracks.")]
        [Min(0f)] public float crossfadeSeconds = 1.2f;

        [Header("Footsteps")]
        [Tooltip("Units run between pen-scratch steps (half the run cycle's 3.87 unit stride).")]
        [Min(0.1f)] public float runStepDistance = 1.94f;
        [Tooltip("Units between steps while sprinting (half the 5.08 unit sprint stride).")]
        [Min(0.1f)] public float sprintStepDistance = 2.54f;

        private Dictionary<string, Sound> lookup;

        public Sound Find(string id)
        {
            if (string.IsNullOrEmpty(id) || sounds == null) return null;
            if (lookup == null)
            {
                lookup = new Dictionary<string, Sound>();
                foreach (Sound s in sounds)
                    if (s != null && !string.IsNullOrEmpty(s.id)) lookup[s.id] = s;
            }
            return lookup.TryGetValue(id, out Sound found) ? found : null;
        }

        private void OnValidate() => lookup = null;
    }
}
