namespace Margin.Player
{
    /// <summary>
    /// Health reached 0. The player collapses (knockback slides out, gravity applies), can't act and can't be hit.
    /// PlayerHealth respawns the player after CombatSettings.playerDeathFrames.
    /// </summary>
    public sealed class DefeatedState : PlayerState
    {
        public DefeatedState(PlayerController player) : base(player) { }

        public override void Enter() => Player.IsInvulnerable = true;
        public override void Exit() => Player.IsInvulnerable = false;

        public override void Tick()
        {
            if (Player.Grounded && Player.Velocity.y <= 0f)
            {
                Player.Velocity.y = 0f;
                Player.Velocity.x = MovementMath.Approach(Player.Velocity.x, 0f, Data.GroundDecelStep * 0.5f);
            }
            else
            {
                Player.ApplyGravity(Data.maxFallSpeed);
            }
        }
    }
}
