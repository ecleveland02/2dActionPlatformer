namespace Margin.Player
{
    /// <summary>
    /// Parry (spec 6.5): 0 startup, active for CombatSettings.parryActiveFrames (6) from the first frame, then
    /// parryWhiffRecoveryFrames (20) of recovery if nothing was parried. A successful parry cancels the recovery:
    /// the player leaves the state on the next tick and can act at once.
    /// </summary>
    public sealed class ParryState : PlayerState
    {
        public ParryState(PlayerController player) : base(player) { }

        public bool Succeeded { get; private set; }

        private Margin.Combat.CombatSettings Settings => Player.Combat.Settings;

        /// <summary>True on the active frames, before a success.</summary>
        public bool IsActive => !Succeeded && Player.FramesInState >= 1 && Player.FramesInState <= Settings.parryActiveFrames;

        public void Succeed() => Succeeded = true;

        public override void Enter() => Succeeded = false;

        public override PlayerState CheckTransitions()
        {
            bool done = Succeeded || Player.FramesInState >= Settings.parryActiveFrames + Settings.parryWhiffRecoveryFrames;
            if (!done) return null;
            return Player.Grounded ? (PlayerState)Player.Idle : Player.Fall;
        }

        public override void Tick()
        {
            if (Player.Grounded && Player.Velocity.y <= 0f)
            {
                Player.Velocity.y = 0f;
                Player.Velocity.x = MovementMath.Approach(Player.Velocity.x, 0f, Data.GroundDecelStep);
            }
            else
            {
                Player.ApplyHorizontal(onGround: false, updateFacing: false);
                Player.ApplyGravity(Data.maxFallSpeed);
            }
        }
    }
}
