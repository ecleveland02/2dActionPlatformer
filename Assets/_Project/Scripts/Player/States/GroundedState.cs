namespace Margin.Player
{
    /// <summary>
    /// Shared rules for states on the ground (Idle, Run, Land, Skid): jumping, dashing, falling
    /// when the ground disappears (which starts coyote time), and skidding when turning at sprint speed.
    /// </summary>
    public abstract class GroundedState : PlayerState
    {
        protected GroundedState(PlayerController player) : base(player) { }

        public override PlayerState CheckTransitions()
        {
            // Jump is checked first so pressing Jump and Dash together favors the jump.
            return Player.CheckGroundJump()
                   ?? Player.CheckParry()
                   ?? Player.CheckRedraw()
                   ?? Player.CheckAttack()
                   ?? Player.CheckDash()
                   ?? (Player.Grounded ? null : Player.Fall)
                   ?? Player.CheckSkid();
        }

        public override void Tick()
        {
            Player.Velocity.y = 0f;
            Player.ApplyHorizontal(onGround: true);
        }
    }
}
