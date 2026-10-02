using Margin.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Margin.Input
{
    /// <summary>
    /// Bridges the Unity Input System to the fixed-tick gameplay code.
    ///
    /// Why the "pending" queue: Input System callbacks run during Update (once per rendered frame),
    /// but gameplay runs in FixedUpdate at 60 Hz. If gameplay polled "was pressed this frame"
    /// from FixedUpdate, quick taps could be missed or seen twice. Instead we remember every press
    /// here and hand them to the InputBuffer at the start of the next tick via Tick().
    ///
    /// The GameLoop ticks this before the player (TickOrder -100), so every tick goes:
    ///     frame counter advances -> InputReader.Tick() flushes presses -> player reads input.
    /// </summary>
    public sealed class InputReader : MonoBehaviour, IPlayerInput, IScriptedInput, ITickable
    {
        [Tooltip("The MarginControls input actions asset.")]
        [SerializeField] private InputActionAsset actions;

        [Tooltip("Per-action buffer windows in frames.")]
        [SerializeField] private InputBufferSettings bufferSettings;

        [Tooltip("Stick values below this on an axis are treated as zero for Up/Down checks.")]
        [SerializeField, Range(0f, 1f)] private float directionThreshold = 0.5f;

        private const string GameplayMap = "Gameplay";

        private InputAction moveAction;
        private InputAction jumpAction;
        private InputAction attackAction;               // tap = light, hold = heavy
        private readonly TapHoldButton attackButton = new TapHoldButton(10);

        // Maps each Input System action to the BufferedAction it feeds. Order matches BufferedAction.
        private InputAction[] bufferedActions;
        private bool[] pendingPresses;

        // Room transitions walk the player through doors (IScriptedInput).
        private bool scripted;
        private Vector2 scriptedMove;
        private bool scriptedJump;

        public InputBuffer Buffer { get; private set; }

        public int TickOrder => -100;

        // Snapshots taken in Tick() so every state sees the same values for the whole tick.
        public Vector2 Move { get; private set; }
        public bool JumpHeld { get; private set; }
        public bool DownHeld => Move.y <= -directionThreshold;
        public bool UpHeld => Move.y >= directionThreshold;
        public bool AttackHoldPending => attackButton.Pending;

        /// <summary>The controls asset (menus read its "Menu" map).</summary>
        public InputActionAsset Actions => actions;

        /// <summary>Creates the buffer. Call once before the first tick, passing the game's frame counter.</summary>
        public void Initialize(IFrameSource clock)
        {
            Buffer = new InputBuffer(clock);
            if (bufferSettings != null)
            {
                bufferSettings.ApplyTo(Buffer);
                attackButton.HoldFrames = bufferSettings.attackHoldFrames;
            }
        }

        private void Awake()
        {
            if (actions == null)
            {
                // Disabling in Awake means OnEnable/OnDisable never run, so this is the only error shown.
                Debug.LogError("InputReader has no Actions asset. Select the Player and drag " +
                               "Assets/_Project/Scripts/Input/MarginControls into the Input Reader's Actions slot.", this);
                enabled = false;
                return;
            }

            if (bufferSettings == null)
                Debug.LogWarning("InputReader: 'Buffer Settings' is empty, so every action uses the default 6 frame window.", this);

            // GameLoop runs its Awake first (DefaultExecutionOrder), so the clock exists here.
            if (Buffer == null && GameLoop.Clock != null) Initialize(GameLoop.Clock);
            if (Buffer == null) Debug.LogError("InputReader needs a GameLoop in the scene.", this);

            Margin.Save.OptionsStore.ApplyBindings(actions);   // the player's rebound keys
            InputActionMap map = actions.FindActionMap(GameplayMap, throwIfNotFound: true);
            moveAction = map.FindAction("Move", throwIfNotFound: true);
            jumpAction = map.FindAction("Jump", throwIfNotFound: true);
            // Optional tap/hold button (tap = light, hold = heavy). Not bound by default: add an "Attack" action to
            // MarginControls to enable it. Separate light/heavy buttons respond instantly, so they're the default.
            attackAction = map.FindAction("Attack", throwIfNotFound: false);

            bufferedActions = new[]
            {
                map.FindAction("Jump", throwIfNotFound: true),
                map.FindAction("LightAttack", throwIfNotFound: true),
                map.FindAction("HeavyAttack", throwIfNotFound: true),
                map.FindAction("Special", throwIfNotFound: true),
                map.FindAction("Dash", throwIfNotFound: true),
                map.FindAction("Parry", throwIfNotFound: true),
                map.FindAction("Grapple", throwIfNotFound: false),   // optional: older controls assets lack it
                map.FindAction("Heal", throwIfNotFound: false),
            };
            pendingPresses = new bool[bufferedActions.Length];
        }

        private void OnEnable()
        {
            foreach (InputAction action in bufferedActions)
                if (action != null) action.performed += OnButtonPerformed;
            if (attackAction != null)
            {
                attackAction.performed += OnAttackPressed;
                attackAction.canceled += OnAttackReleased;
            }
            actions.FindActionMap(GameplayMap).Enable();
            GameLoop.Register(this);
        }

        private void OnDisable()
        {
            foreach (InputAction action in bufferedActions)
                if (action != null) action.performed -= OnButtonPerformed;
            if (attackAction != null)
            {
                attackAction.performed -= OnAttackPressed;
                attackAction.canceled -= OnAttackReleased;
            }
            attackButton.Reset();
            actions.FindActionMap(GameplayMap).Disable();
            GameLoop.Unregister(this);
        }

        /// <summary>
        /// Menus turn gameplay input off while open. Presses queued before that are dropped, so the button that
        /// picked "Resume" doesn't also swing the sword.
        /// </summary>
        public void SetGameplayInput(bool on)
        {
            if (actions == null || pendingPresses == null) return;
            InputActionMap map = actions.FindActionMap(GameplayMap);
            if (on && isActiveAndEnabled) map.Enable();
            else map.Disable();
            for (int i = 0; i < pendingPresses.Length; i++) pendingPresses[i] = false;
            attackButton.Reset();
        }

        public bool IsScripted => scripted;

        /// <summary>
        /// Takes over the controls (room transitions): Move and JumpHeld come from here and presses are dropped
        /// until ClearScript().
        /// </summary>
        public void Script(Vector2 move, bool jumpHeld)
        {
            scripted = true;
            scriptedMove = move;
            scriptedJump = jumpHeld;
        }

        public void ClearScript() => scripted = false;

        private void OnAttackPressed(InputAction.CallbackContext context) => attackButton.QueuePress();
        private void OnAttackReleased(InputAction.CallbackContext context) => attackButton.QueueRelease();

        private void OnButtonPerformed(InputAction.CallbackContext context)
        {
            int index = System.Array.IndexOf(bufferedActions, context.action);
            if (index >= 0) pendingPresses[index] = true;
        }

        /// <summary>
        /// Called by the GameLoop once at the start of every fixed tick, after the frame counter advances.
        /// Moves queued presses into the buffer and snapshots held/axis state.
        /// </summary>
        public void Tick()
        {
            if (scripted)
            {
                // The player isn't in control: presses made now are dropped, not saved for later.
                for (int i = 0; i < pendingPresses.Length; i++) pendingPresses[i] = false;
                attackButton.Reset();
                Move = scriptedMove;
                JumpHeld = scriptedJump;
                return;
            }

            for (int i = 0; i < pendingPresses.Length; i++)
            {
                if (!pendingPresses[i]) continue;
                Buffer.Record((BufferedAction)i);
                pendingPresses[i] = false;
            }

            // Tap/hold Attack button: a tap becomes a buffered light attack, a hold a buffered heavy attack.
            switch (attackButton.Tick())
            {
                case TapHoldResult.Tap: Buffer.Record(BufferedAction.LightAttack); break;
                case TapHoldResult.Hold: Buffer.Record(BufferedAction.HeavyAttack); break;
            }

            Move = moveAction.ReadValue<Vector2>();
            JumpHeld = jumpAction.IsPressed();
        }
    }
}
