namespace Margin.Player
{
    /// <summary>
    /// The Spring Doodle (spec 8, Boss 2's ability): a second jump in the air. Rises like a normal jump (Jump
    /// released early cuts it short) to MovementData.doubleJumpHeight, with a small hitbox under the feet for the
    /// first few frames that knocks enemies below away. Lands, grabs a wall or grapples to get it back.
    /// </summary>
    public sealed class DoubleJumpState : JumpState
    {
        public DoubleJumpState(PlayerController player) : base(player) { }

        public override void Enter() => Player.StartDoubleJump();
    }
}
