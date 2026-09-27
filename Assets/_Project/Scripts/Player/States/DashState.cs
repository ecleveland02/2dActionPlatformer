namespace Margin.Player
{
    /// <summary>
    /// Fixed-length horizontal burst with no gravity (spec 8: 10 frames, 16 u/s, 8 invulnerable frames).
    /// Ground dashes can be jump-cancelled. Ends at run speed so it doesn't slide.
    /// </summary>
    public sealed class DashState : PlayerState
    {
        private int direction;
        private bool startedGrounded;

        public DashState(PlayerController player) : base(player) { }

        public override void Enter()
        {
            direction = Player.InputX != 0 ? Player.InputX : Player.Facing;
            Player.Facing = direction;
            startedGrounded = Player.Grounded;
            if (!startedGrounded) Player.AirDashesLeft--;
            Player.IsInvulnerable = true;
        }

        public override PlayerState CheckTransitions()
        {
            PlayerState jump = Player.CheckGroundJump();
            if (jump != null) return jump;

            if (Player.FramesInState < Data.dashFrames) return null;
            if (!Player.Grounded) return Player.Fall;
            return Player.InputX != 0 ? (PlayerState)Player.Run : Player.Idle;
        }

        public override void Tick()
        {
            Player.Velocity.x = direction * Data.dashSpeed;
            Player.Velocity.y = 0f;
            Player.IsInvulnerable = Player.FramesInState <= Data.dashInvulnerableFrames;
        }

        public override void Exit()
        {
            Player.IsInvulnerable = false;
            // Leave the dash at run speed, or at sprint speed if you were (and still are) sprinting.
            float exitSpeed = Player.IsSprinting ? Data.sprintSpeed : Data.runSpeed;
            Player.Velocity.x = UnityEngine.Mathf.Clamp(Player.Velocity.x, -exitSpeed, exitSpeed);
            if (startedGrounded) Player.DashCooldown = Data.dashCooldownFrames;
        }
    }
}
