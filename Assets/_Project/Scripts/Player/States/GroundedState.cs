namespace Margin.Player
{
    /// <summary>
    /// Shared rules for states on the ground (Idle, Run, Land): jumping, dashing, and falling
    /// when the ground disappears (which starts coyote time).
    /// </summary>
    public abstract class GroundedState : PlayerState
    {
        protected GroundedState(PlayerController player) : base(player) { }

        public override PlayerState CheckTransitions()
        {
            // Jump is checked first so pressing Jump and Dash together favors the jump.
            return Player.CheckGroundJump()
                   ?? Player.CheckDash()
                   ?? (Player.Grounded ? null : Player.Fall);
        }

        public override void Tick()
        {
            Player.Velocity.y = 0f;
            Player.ApplyHorizontal(onGround: true);
        }
    }
}
