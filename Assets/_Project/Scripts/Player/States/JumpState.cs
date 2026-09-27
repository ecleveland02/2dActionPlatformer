namespace Margin.Player
{
    /// <summary>
    /// Rising part of a jump. Releasing Jump early cuts the rise so a tap reaches the minimum height.
    /// Switches to Fall at the apex (vertical speed reaches 0) or when the head hits a ceiling.
    /// </summary>
    public class JumpState : PlayerState
    {
        public JumpState(PlayerController player) : base(player) { }

        public override void Enter()
        {
            Player.StartJump();
        }

        public override PlayerState CheckTransitions()
        {
            return Player.CheckWallJump()
                   ?? Player.CheckParry()
                   ?? Player.CheckAttack()
                   ?? Player.CheckDash()
                   ?? (Player.Velocity.y <= 0f ? Player.Fall : null);
        }

        public override void Tick()
        {
            // Variable jump height: once Jump is released, cap upward speed.
            if (!Player.Controls.JumpHeld && Player.Velocity.y > 0f)
            {
                float risen = Player.Body.Position.y - Player.JumpStartY;
                Player.Velocity.y = UnityEngine.Mathf.Min(Player.Velocity.y, Data.JumpCutVelocity(risen));
            }

            ApplyAirControl();
            Player.ApplyGravity(Data.maxFallSpeed);
        }

        protected virtual void ApplyAirControl()
        {
            Player.ApplyHorizontal(onGround: false);
        }
    }
}
