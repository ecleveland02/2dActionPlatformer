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
        /// <summary>
        /// Extra frames a missed attack waits before it may chain into a follow-up attack. -1 = a miss can never
        /// chain into an attack (only jump/dash, the original spec 6.3 rule).
        /// </summary>
        public readonly int WhiffChainDelay;

        public AttackTiming(int startup, int active, int recovery, int cancelStart, int cancelEnd, int whiffChainDelay = -1)
        {
            Startup = startup;
            Active = active;
            Recovery = recovery;
            CancelStart = cancelStart;
            CancelEnd = cancelEnd;
            WhiffChainDelay = whiffChainDelay;
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
        /// When a listed follow-up attack may start (spec 6.3, amended):
        /// on hit, frames CancelStart..CancelEnd; on a miss, from CancelStart + WhiffChainDelay to the end of
        /// the attack (so strings keep flowing, just a little slower), or never if WhiffChainDelay is -1.
        /// </summary>
        public bool AllowsAttackCancel(int frame, bool hasHit)
        {
            if (hasHit) return frame >= CancelStart && frame <= CancelEnd;
            return WhiffChainDelay >= 0 && frame >= CancelStart + WhiffChainDelay && frame <= TotalFrames;
        }

        /// <summary>
        /// Spec 6.3: jump and dash cancels. On hit, inside the cancel window. On whiff, only from the window
        /// start onward (so a missed swing still commits you to its startup and active frames).
        /// </summary>
        public bool AllowsMovementCancel(int frame, bool hasHit) =>
            hasHit ? frame >= CancelStart && frame <= CancelEnd : frame >= CancelStart;
    }
}
