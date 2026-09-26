namespace Margin.Player
{
    /// <summary>Holding into a wall while falling: fall speed is capped at the wall slide speed.</summary>
    public sealed class WallSlideState : PlayerState
    {
        public WallSlideState(PlayerController player) : base(player) { }

        public override void Enter()
        {
            Player.WallDirection = Player.InputX;
            Player.Facing = -Player.WallDirection;   // face away from the wall, ready to jump off
        }

        public override PlayerState CheckTransitions()
        {
            if (Player.Grounded) return Player.Land;

            PlayerState next = Player.CheckWallJump() ?? Player.CheckDash();
            if (next != null) return next;

            bool stillOnWall = Player.InputX == Player.WallDirection && Player.Body.IsTouchingWall(Player.WallDirection);
            return stillOnWall ? null : Player.Fall;
        }

        public override void Tick()
        {
            Player.Velocity.x = 0f;
            Player.ApplyGravity(Data.wallSlideSpeed);
        }
    }
}
