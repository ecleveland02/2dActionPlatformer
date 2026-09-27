using System;
using System.Collections.Generic;
using UnityEngine;

namespace Margin.Rendering
{
    /// <summary>
    /// A sequence of poses with per-entry durations in frames and easing (spec 4.2).
    /// Create via Assets > Create > Margin > Pose Clip, or Margin > Create Starter Animations.
    /// </summary>
    [CreateAssetMenu(fileName = "PoseClip", menuName = "Margin/Pose Clip")]
    public sealed class PoseClip : ScriptableObject
    {
        [Serializable]
        public sealed class Entry
        {
            public PoseData pose;
            [Tooltip("Frames spent on this entry, blending toward the next one.")]
            [Min(1)] public int frames = 5;
            public PoseEasing easing = PoseEasing.Linear;
        }

        public List<Entry> entries = new List<Entry>();
        [Tooltip("Loop back to the first entry (run cycles, idle). Off = hold the last pose.")]
        public bool loop = true;
        [Tooltip("Frames to blend from the previous animation into this one. 0 = instant (crisp).")]
        [Min(0)] public int fadeInFrames = 4;

        private PoseTimeline timeline;

        /// <summary>The playback data, rebuilt whenever the clip is edited. Null if the clip has no valid poses.</summary>
        public PoseTimeline Timeline
        {
            get
            {
                if (timeline == null) timeline = Build();
                return timeline;
            }
        }

        private void OnValidate() => timeline = null;

        private PoseTimeline Build()
        {
            var poses = new List<FigurePose>();
            var frames = new List<int>();
            var easings = new List<PoseEasing>();
            foreach (Entry entry in entries)
            {
                if (entry == null || entry.pose == null) continue;
                poses.Add(entry.pose.pose);
                frames.Add(entry.frames);
                easings.Add(entry.easing);
            }
            return poses.Count == 0 ? null : new PoseTimeline(poses, frames, easings, loop);
        }
    }
}
