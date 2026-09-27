namespace Margin.Player
{
    /// <summary>
    /// Turning around at sprint speed: the player slides, braking over MovementData.skidFrames, then runs
    /// the other way. Only entered above run speed, so normal turns stay snappy. Jump and dash cancel it
    /// instantly (from GroundedState), so it never locks the player in.
    /// </summary>
    public sealed class SkidState : GroundedState
    {
        public SkidState(PlayerController player) : base(player) { }

        public override void Enter()
        {
            if (Player.InputX != 0) Player.Facing = Player.InputX;   // turn to face the new direction immediately
        }

        public override PlayerState CheckTransitions()
        {
            PlayerState next = Player.CheckGroundJump()
                               ?? Player.CheckParry()
                   ?? Player.CheckAttack()
                               ?? Player.CheckDash()
                               ?? (Player.Grounded ? null : Player.Fall);
            if (next != null) return next;

            int moving = (int)UnityEngine.Mathf.Sign(Player.Velocity.x);
            // Changed your mind and pushed forward again: stop skidding and keep running.
            if (Player.Velocity.x != 0f && Player.InputX == moving) return Player.Run;
            // Skid finished.
            if (Player.Velocity.x == 0f) return Player.InputX != 0 ? (PlayerState)Player.Run : Player.Idle;
            return null;
        }

        public override void Tick()
        {
            Player.Velocity.y = 0f;
            Player.Velocity.x = MovementMath.Approach(Player.Velocity.x, 0f, Data.SkidStep);
            if (Player.InputX != 0) Player.Facing = Player.InputX;
        }
    }
}
