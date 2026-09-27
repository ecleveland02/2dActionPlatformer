namespace Margin.Audio
{
    /// <summary>
    /// A fire-and-forget sound request channel (spec 3.2 event bus): anything can ask for a sound by id
    /// ("ui_select", "cap_reflect") without knowing about the AudioDirector that plays it.
    /// </summary>
    public static class Sfx
    {
        public static event System.Action<string, float> Requested;

        public static void Play(string id, float volume = 1f) => Requested?.Invoke(id, volume);
    }
}
