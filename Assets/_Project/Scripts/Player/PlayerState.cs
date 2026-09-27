namespace Margin.Player
{
    /// <summary>
    /// One player state (Idle, Run, Jump, ...). Each tick the state machine first asks the current state
    /// CheckTransitions() (possibly hopping through several states in one tick, e.g. Fall -> Land -> Jump
    /// when a buffered jump fires on landing), then calls Tick() on the final state.
    /// States only change velocity; the PlayerController does the actual moving afterwards.
    /// </summary>
    public abstract class PlayerState
    {
        protected PlayerState(PlayerController player)
        {
            Player = player;
        }

        protected PlayerController Player { get; }
        protected MovementData Data => Player.Data;

        /// <summary>Short name for the debug overlay, e.g. "FastFall".</summary>
        public string Name => GetType().Name.Replace("State", "");

        public virtual void Enter() { }

        /// <summary>Return the state to switch to, or null to stay.</summary>
        public virtual PlayerState CheckTransitions() => null;

        public abstract void Tick();

        public virtual void Exit() { }

        /// <summary>Called after the body moved this tick (attacks check hitboxes here, at the new position).</summary>
        public virtual void PostMove() { }

        /// <summary>Lets a state refuse to be left (e.g. a future attack during active frames).</summary>
        public virtual bool CanTransitionTo(PlayerState next) => true;
    }
}
