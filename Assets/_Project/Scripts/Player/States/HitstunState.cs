namespace Margin.Player
{
    /// <summary>
    /// Knocked back by a hit (spec 5.4: any state can be interrupted by Hitstun). No control until it ends.
    /// Basic version: knockback slides out and gravity applies. Knockdown, death and respawn come in Milestone 4.
    /// </summary>
    public sealed class HitstunState : PlayerState
    {
        public HitstunState(PlayerController player) : base(player) { }

        public int Frames { get; set; }

        public override PlayerState CheckTransitions()
        {
            if (Player.FramesInState < Frames) return null;
            return Player.Grounded ? (PlayerState)Player.Idle : Player.Fall;
        }

        public override void Tick()
        {
            if (Player.Grounded && Player.Velocity.y <= 0f)
            {
                Player.Velocity.y = 0f;
                Player.Velocity.x = MovementMath.Approach(Player.Velocity.x, 0f, Data.GroundDecelStep * 0.5f);
            }
            else
            {
                Player.ApplyGravity(Data.maxFallSpeed);
            }
        }
    }
}
