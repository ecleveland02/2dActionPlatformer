namespace Margin.Enemies
{
    /// <summary>
    /// One enemy behavior state (spec 3.2): Enter, Tick (fixed), Exit, and CheckTransitions.
    /// States set EnemyBase.Velocity; EnemyBase applies gravity and moves the body afterwards.
    /// </summary>
    public abstract class EnemyState
    {
        protected EnemyState(EnemyBase enemy) => Enemy = enemy;

        protected EnemyBase Enemy { get; }
        protected EnemyData Data => Enemy.Data;

        public virtual void Enter() { }
        /// <summary>Returns the state to switch to, or null to stay.</summary>
        public virtual EnemyState CheckTransitions() => null;
        public virtual void Tick() { }
        public virtual void Exit() { }
    }

    /// <summary>Same rules as the player's machine: transitions first (up to 4 per tick), then the state ticks.</summary>
    public sealed class EnemyStateMachine
    {
        private const int MaxTransitionsPerTick = 4;

        public EnemyState Current { get; private set; }
        /// <summary>Ticks in the current state, counting the current tick (1 on the first Tick()).</summary>
        public int FramesInState { get; private set; }

        public void ForceState(EnemyState state)
        {
            Current?.Exit();
            Current = state;
            FramesInState = 0;
            state.Enter();
        }

        public void Tick()
        {
            for (int i = 0; i < MaxTransitionsPerTick; i++)
            {
                EnemyState next = Current.CheckTransitions();
                if (next == null) break;
                ForceState(next);
            }
            FramesInState++;
            Current.Tick();
        }
    }
}
