using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Margin.Core
{
    /// <summary>
    /// The one place gameplay time moves forward. Each FixedUpdate it advances the frame counter,
    /// then ticks every registered ITickable in TickOrder. Because everything goes through here,
    /// pausing or frame-stepping the game (debug tools) only needs to control this component.
    ///
    /// Runs before other scripts (DefaultExecutionOrder) so Clock exists when they Awake.
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public sealed class GameLoop : MonoBehaviour
    {
        // Static so tickables can register from OnEnable without needing a reference to the loop.
        private static readonly List<ITickable> tickables = new List<ITickable>();
        private static ITickable[] ordered = new ITickable[0];
        private static bool orderDirty;

        private static GameLoop instance;
        private readonly FrameCounter counter = new FrameCounter();

        // Debug frame-stepping (F3/F4). While paused, ticks only run when a step is requested.
        private static bool paused;
        private static int pendingSteps;

        /// <summary>When true, gameplay stops ticking (the frame counter freezes too) until Step() is called.</summary>
        public static bool Paused
        {
            get => paused;
            set
            {
                paused = value;
                if (!paused) pendingSteps = 0;
            }
        }

        /// <summary>Pauses (if needed) and runs exactly one gameplay tick on the next FixedUpdate.</summary>
        public static void Step()
        {
            paused = true;
            pendingSteps++;
        }

        /// <summary>The gameplay clock. Null if no GameLoop is in the scene.</summary>
        public static IFrameSource Clock => instance != null ? instance.counter : null;

        public static void Register(ITickable tickable)
        {
            if (tickables.Contains(tickable)) return;
            tickables.Add(tickable);
            orderDirty = true;
        }

        public static void Unregister(ITickable tickable)
        {
            if (tickables.Remove(tickable)) orderDirty = true;
        }

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Debug.LogError("Only one GameLoop may exist. Disabling the duplicate.", this);
                enabled = false;
                return;
            }
            instance = this;
            paused = false;       // statics survive between Play sessions when domain reload is off
            pendingSteps = 0;

            // The whole game counts time in 60 Hz ticks, so enforce it here even if Project Settings disagree.
            if (Mathf.Abs(Time.fixedDeltaTime - GameTime.TickDelta) > 0.0001f)
            {
                Debug.LogWarning($"Fixed Timestep was {Time.fixedDeltaTime}; forcing 1/60 for this session. " +
                                 "Set Project Settings > Time > Fixed Timestep to 0.0166667 to remove this warning.");
                Time.fixedDeltaTime = GameTime.TickDelta;
            }
        }

        private void OnDestroy()
        {
            if (instance == this) instance = null;
        }

        private void FixedUpdate()
        {
            if (paused)
            {
                if (pendingSteps == 0) return;
                pendingSteps--;
            }

            counter.Advance();

            if (orderDirty)
            {
                // OrderBy is a stable sort: equal TickOrder keeps registration order.
                ordered = tickables.OrderBy(t => t.TickOrder).ToArray();
                orderDirty = false;
            }

            // Iterate a snapshot so a tickable disabling itself mid-tick can't break the loop.
            foreach (ITickable tickable in ordered) tickable.Tick();
        }
    }
}
