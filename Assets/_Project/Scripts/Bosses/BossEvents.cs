namespace Margin.Bosses
{
    /// <summary>Static boss events (spec 3.2 event bus: OnBossPhaseChange). Music and sounds listen here.</summary>
    public static class BossEvents
    {
        public static event System.Action<BossBase> Engaged;
        /// <summary>A move's telegraph started; the string is its tell sound id (may be empty).</summary>
        public static event System.Action<BossBase, string> MoveStarted;
        public static event System.Action<BossBase, int> PhaseChanged;
        public static event System.Action<BossBase> Beaten;
        /// <summary>Back to sleep (the player died or left): the fight will start over.</summary>
        public static event System.Action<BossBase> Reset;

        public static void RaiseEngaged(BossBase boss) => Engaged?.Invoke(boss);
        public static void RaiseMoveStarted(BossBase boss, string tell) => MoveStarted?.Invoke(boss, tell);
        public static void RaisePhaseChanged(BossBase boss, int phase) => PhaseChanged?.Invoke(boss, phase);
        public static void RaiseBeaten(BossBase boss) => Beaten?.Invoke(boss);
        public static void RaiseReset(BossBase boss) => Reset?.Invoke(boss);
    }
}
