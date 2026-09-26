namespace Margin.Player
{
    public sealed class PlayerStateMachine
    {
        // Guards against two states endlessly handing control back and forth in one tick.
        private const int MaxTransitionsPerTick = 4;

        public PlayerState Current { get; private set; }

        /// <summary>Ticks spent in the current state, counting the current tick (1 on the first Tick()).</summary>
        public int FramesInState { get; private set; }

        public void ForceState(PlayerState state)
        {
            Current?.Exit();
            Current = state;
            FramesInState = 0;
            state.Enter();
        }

        /// <summary>Switches state unless the current state refuses. Re-entering the same state is allowed.</summary>
        public bool ChangeState(PlayerState next)
        {
            if (next == null) return false;
            if (Current != null && !Current.CanTransitionTo(next)) return false;
            ForceState(next);
            return true;
        }

        public void Tick()
        {
            for (int i = 0; i < MaxTransitionsPerTick; i++)
            {
                PlayerState next = Current.CheckTransitions();
                if (!ChangeState(next)) break;
            }

            FramesInState++;
            Current.Tick();
        }
    }
}
