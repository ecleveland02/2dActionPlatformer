namespace Margin.Combat
{
    public enum AttackPhase { Startup, Active, Recovery, Finished }

    /// <summary>
    /// Frame data for one attack (spec 6.2), in pure C# so the rules can be unit tested.
    /// Frame numbers follow the project rule: the tick the attack starts is frame 1.
    /// Example (Brush Katana Light 1): startup 4, active 3, recovery 10 means frames 1-4 startup,
    /// 5-7 active (hitbox out), 8-17 recovery, 17 frames total.
    /// </summary>
    public readonly struct AttackTiming
    {
        public readonly int Startup;
        public readonly int Active;
        public readonly int Recovery;
        public readonly int CancelStart;
        public readonly int CancelEnd;

        public AttackTiming(int startup, int active, int recovery, int cancelStart, int cancelEnd)
        {
            Startup = startup;
            Active = active;
            Recovery = recovery;
            CancelStart = cancelStart;
            CancelEnd = cancelEnd;
        }

        public int TotalFrames => Startup + Active + Recovery;
        public int FirstActiveFrame => Startup + 1;
        public int LastActiveFrame => Startup + Active;

        public AttackPhase PhaseAt(int frame)
        {
            if (frame <= Startup) return AttackPhase.Startup;
            if (frame <= LastActiveFrame) return AttackPhase.Active;
            if (frame <= TotalFrames) return AttackPhase.Recovery;
            return AttackPhase.Finished;
        }

        public bool IsActive(int frame) => PhaseAt(frame) == AttackPhase.Active;

        /// <summary>
        /// Spec 6.3: after landing a hit, the listed follow-up attacks may start on frames CancelStart..CancelEnd.
        /// </summary>
        public bool AllowsAttackCancel(int frame, bool hasHit) =>
            hasHit && frame >= CancelStart && frame <= CancelEnd;

        /// <summary>
        /// Spec 6.3: jump and dash cancels. On hit, inside the cancel window. On whiff, only from the window
        /// start onward (so a missed swing still commits you to its startup and active frames).
        /// </summary>
        public bool AllowsMovementCancel(int frame, bool hasHit) =>
            hasHit ? frame >= CancelStart && frame <= CancelEnd : frame >= CancelStart;
    }
}
