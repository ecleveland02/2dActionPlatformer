namespace Margin.Player
{
    public sealed class RunState : GroundedState
    {
        public RunState(PlayerController player) : base(player) { }

        public override PlayerState CheckTransitions()
        {
            // Stay in Run while still sliding to a stop so the (future) run animation can blend out.
            bool stopped = Player.InputX == 0 && Player.Velocity.x == 0f;
            return base.CheckTransitions() ?? (stopped ? Player.Idle : null);
        }
    }
}
