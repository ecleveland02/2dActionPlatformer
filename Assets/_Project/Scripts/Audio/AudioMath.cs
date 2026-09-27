namespace Margin.Audio
{
    /// <summary>Small pure helpers for the audio system (unit tested).</summary>
    public static class AudioMath
    {
        /// <summary>Seconds in <paramref name="bars"/> bars at a tempo (e.g. 2 bars of 4/4 at 128 BPM = 3.75 s).</summary>
        public static float BarsToSeconds(float bars, float bpm, int beatsPerBar = 4) =>
            bpm <= 0f ? 0f : bars * beatsPerBar * 60f / bpm;

        /// <summary>
        /// Picks a variant 0..count-1 from a random number in [0, 1), never the same one twice in a row
        /// (hearing the identical sound back to back is what makes game audio sound cheap).
        /// </summary>
        public static int PickVariant(int count, int last, float random01)
        {
            if (count <= 1) return 0;
            if (last < 0 || last >= count)
                return System.Math.Min(count - 1, (int)(random01 * count));
            int pick = System.Math.Min(count - 2, (int)(random01 * (count - 1)));
            return pick >= last ? pick + 1 : pick;
        }

        /// <summary>Converts decibels to a volume multiplier (0 dB = 1, -6 dB ~ 0.5).</summary>
        public static float DbToGain(float db) => (float)System.Math.Pow(10.0, db / 20.0);
    }
}
