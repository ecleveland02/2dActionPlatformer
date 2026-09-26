namespace Margin.Player
{
    public sealed class IdleState : GroundedState
    {
        public IdleState(PlayerController player) : base(player) { }

        public override PlayerState CheckTransitions()
        {
            return base.CheckTransitions() ?? (Player.InputX != 0 ? Player.Run : null);
        }
    }
}
