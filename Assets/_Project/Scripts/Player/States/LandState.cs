namespace Margin.Player
{
    /// <summary>
    /// Short landing state (spec: 2 frames) for the landing pose later. Fully cancellable into jump or dash,
    /// and running still works, so it never locks the player in place.
    /// </summary>
    public sealed class LandState : GroundedState
    {
        public LandState(PlayerController player) : base(player) { }

        public override PlayerState CheckTransitions()
        {
            PlayerState next = base.CheckTransitions();
            if (next != null) return next;
            if (Player.FramesInState < Data.landFrames) return null;
            return Player.InputX != 0 ? (PlayerState)Player.Run : Player.Idle;
        }
    }
}
