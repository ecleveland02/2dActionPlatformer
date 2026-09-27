namespace Margin.Player
{
    /// <summary>Airborne and not rising. Handles landing, coyote jumps, wall slide/jump, and fast fall.</summary>
    public sealed class FallState : PlayerState
    {
        public FallState(PlayerController player) : base(player) { }

        public override PlayerState CheckTransitions()
        {
            if (Player.Grounded) return Player.Land;

            return Player.CheckGroundJump()          // coyote time
                   ?? Player.CheckWallJump()
                   ?? Player.CheckGrapple()
                   ?? Player.CheckParry()
                   ?? Player.CheckAttack()
                   ?? Player.CheckDash()
                   ?? Player.CheckWallSlide()
                   ?? (Player.Controls.DownHeld ? Player.FastFall : null);
        }

        public override void Tick()
        {
            Player.ApplyHorizontal(onGround: false);
            Player.ApplyGravity(Data.maxFallSpeed);
        }
    }
}
