namespace Margin.Player
{
    /// <summary>Down pressed in the air after the apex: drop at fast fall speed immediately.</summary>
    public sealed class FastFallState : PlayerState
    {
        public FastFallState(PlayerController player) : base(player) { }

        public override void Enter()
        {
            Player.Velocity.y = -Data.fastFallSpeed;
        }

        public override PlayerState CheckTransitions()
        {
            if (Player.Grounded) return Player.Land;

            return Player.CheckGroundJump()
                   ?? Player.CheckWallJump()
                   ?? Player.CheckDash();
        }

        public override void Tick()
        {
            Player.ApplyHorizontal(onGround: false);
            Player.ApplyGravity(Data.fastFallSpeed);
        }
    }
}
