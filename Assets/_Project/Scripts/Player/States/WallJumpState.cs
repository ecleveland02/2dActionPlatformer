namespace Margin.Player
{
    /// <summary>
    /// A jump that pushes away from the wall. Left/right input is ignored for a few frames so the
    /// player can't instantly steer back into the wall. After that it behaves exactly like JumpState.
    /// </summary>
    public sealed class WallJumpState : JumpState
    {
        public WallJumpState(PlayerController player) : base(player) { }

        public override void Enter()
        {
            base.Enter();
            Player.Velocity.x = -Player.WallDirection * Data.wallJumpHorizontalSpeed;
            Player.Facing = -Player.WallDirection;
        }

        protected override void ApplyAirControl()
        {
            if (Player.FramesInState <= Data.wallJumpControlLockFrames) return;
            base.ApplyAirControl();
        }
    }
}
