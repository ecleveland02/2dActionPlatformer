namespace Margin.Player
{
    /// <summary>
    /// Redraw (spec 6.6): spend 100 ink (paid on entry) to heal 30% of max health at the end of a 45-frame,
    /// vulnerable animation. Getting hit during it forces Hitstun and the heal is lost.
    /// </summary>
    public sealed class RedrawState : PlayerState
    {
        public RedrawState(PlayerController player) : base(player) { }

        private Margin.Combat.CombatSettings Settings => Player.Combat.Settings;

        public override PlayerState CheckTransitions()
        {
            if (Player.FramesInState < Settings.redrawFrames) return null;
            return Player.Grounded ? (PlayerState)Player.Idle : Player.Fall;
        }

        public override void Tick()
        {
            Player.Velocity.y = Player.Grounded ? 0f : Player.Velocity.y;
            Player.Velocity.x = MovementMath.Approach(Player.Velocity.x, 0f, Data.GroundDecelStep);
            if (!Player.Grounded) Player.ApplyGravity(Data.maxFallSpeed);

            if (Player.FramesInState == Settings.redrawFrames)
            {
                int healed = Player.Health.Health.HealFraction(Settings.redrawHealFraction);
                UnityEngine.Debug.Log($"[Player] Redraw healed {healed}.");
            }
        }
    }
}
