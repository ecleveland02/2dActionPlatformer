using System;
using System.Collections.Generic;

namespace Margin.Rendering
{
    /// <summary>
    /// The playback math of a PoseClip, in pure C# so it can be unit tested.
    /// Each entry holds for its number of frames while blending toward the next entry with its easing.
    /// Looping clips blend the last entry back into the first; one-shot clips hold the last pose at the end.
    /// </summary>
    public sealed class PoseTimeline
    {
        private readonly FigurePose[] poses;
        private readonly int[] frames;
        private readonly PoseEasing[] easings;

        public bool Loop { get; }
        public int TotalFrames { get; }
        public int Count => poses.Length;

        public PoseTimeline(IList<FigurePose> poses, IList<int> frames, IList<PoseEasing> easings, bool loop)
        {
            if (poses == null || poses.Count == 0) throw new ArgumentException("A timeline needs at least one pose.");
            if (frames.Count != poses.Count || easings.Count != poses.Count)
                throw new ArgumentException("poses, frames and easings must have the same length.");

            this.poses = new FigurePose[poses.Count];
            this.frames = new int[poses.Count];
            this.easings = new PoseEasing[poses.Count];
            for (int i = 0; i < poses.Count; i++)
            {
                this.poses[i] = poses[i];
                this.frames[i] = Math.Max(1, frames[i]);
                this.easings[i] = easings[i];
                TotalFrames += this.frames[i];
            }
            Loop = loop;
        }

        public FigurePose FirstPose => poses[0];

        /// <summary>The entry before (-1) or after (+1) entry i: wraps around in a loop, stops at the ends otherwise.</summary>
        private int Neighbour(int i, int step)
        {
            int j = i + step;
            if (Loop) return (j % poses.Length + poses.Length) % poses.Length;
            return Math.Max(0, Math.Min(poses.Length - 1, j));
        }

        /// <summary>The pose at a tick (0 = first tick of the clip).</summary>
        public FigurePose Sample(int tick)
        {
            if (tick < 0) tick = 0;
            if (Loop) tick %= TotalFrames;
            else if (tick >= TotalFrames) return poses[poses.Length - 1];

            int start = 0;
            for (int i = 0; i < poses.Length; i++)
            {
                if (tick < start + frames[i])
                {
                    int next = Neighbour(i, +1);
                    float u = (tick - start) / (float)frames[i];
                    if (easings[i] == PoseEasing.Smooth)
                        return FigurePose.CatmullRom(poses[Neighbour(i, -1)], poses[i], poses[next], poses[Neighbour(next, +1)], u);
                    return FigurePose.Lerp(poses[i], poses[next], PoseEasingMath.Apply(easings[i], u));
                }
                start += frames[i];
            }
            return poses[poses.Length - 1];
        }
    }
}
