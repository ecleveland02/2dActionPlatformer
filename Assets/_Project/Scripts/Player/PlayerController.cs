using Margin.Abilities;
using Margin.Core;
using Margin.Input;
using Margin.Physics;
using UnityEngine;

namespace Margin.Player
{
    /// <summary>
    /// Owns the player's velocity and state machine, and moves the KinematicBody2D once per tick.
    ///
    /// Tick order (called by GameLoop after InputReader):
    ///   1. State machine: transitions, then the current state updates Velocity.
    ///   2. Body.Move() with this tick's displacement.
    ///   3. Collision results zero out blocked velocity and update ground timers.
    ///
    /// States call the shared helpers below (ApplyHorizontal, ApplyGravity, Check...) so the rules
    /// for jumping, dashing and walls live in one place.
    /// </summary>
    [RequireComponent(typeof(KinematicBody2D))]
    public sealed class PlayerController : MonoBehaviour, ITickable
    {
        [SerializeField] private MovementData data;
        [SerializeField] private InputReader inputReader;
        [SerializeField] private AbilityUnlocks abilities = new AbilityUnlocks();
        [Tooltip("Placeholder visual. Flipped left/right to show facing.")]
        [SerializeField] private Transform visualRoot;

        private PlayerStateMachine machine;
        private bool reportedMissingInput;
        private bool coyoteAvailable;
        private bool hasPendingDy;
        private float pendingDy;

        // ---- State instances (created once, reused) ----
        public IdleState Idle { get; private set; }
        public RunState Run { get; private set; }
        public SkidState Skid { get; private set; }
        public JumpState Jump { get; private set; }
        public FallState Fall { get; private set; }
        public LandState Land { get; private set; }
        public FastFallState FastFall { get; private set; }
        public DashState Dash { get; private set; }
        public WallSlideState WallSlide { get; private set; }
        public WallJumpState WallJump { get; private set; }

        // ---- Runtime values the states read and write ----
        /// <summary>Units per second. Public field so states can set .x / .y directly.</summary>
        [System.NonSerialized] public Vector2 Velocity;
        /// <summary>-1 left, +1 right.</summary>
        public int Facing { get; set; } = 1;
        public float JumpStartY { get; set; }
        /// <summary>Side of the wall being slid on or jumped off: -1 left, +1 right.</summary>
        public int WallDirection { get; set; }
        public bool IsInvulnerable { get; set; }
        public int DashCooldown { get; set; }
        public int AirDashesLeft { get; set; }
        /// <summary>Ticks since last grounded. 0 while on the ground, 1 on the first tick starting airborne.</summary>
        public int FramesSinceGrounded { get; private set; }
        /// <summary>Consecutive grounded ticks spent running at full speed or faster. Sprint starts at MovementData.framesToStartSprint.</summary>
        public int SprintCharge { get; private set; }
        public bool IsSprinting => SprintCharge >= data.framesToStartSprint;

        public MovementData Data => data;
        public IPlayerInput Controls { get; private set; }
        public AbilityUnlocks Abilities => abilities;
        public KinematicBody2D Body { get; private set; }
        public PlayerState CurrentState => machine.Current;
        /// <summary>Raised on every state change with (previous, next). Used by the debug overlay.</summary>
        public event System.Action<PlayerState, PlayerState> StateChanged
        {
            add => machine.Changed += value;
            remove => machine.Changed -= value;
        }
        /// <summary>True until a jump uses up the coyote window (reset on landing).</summary>
        public bool CoyoteAvailable => coyoteAvailable;
        public int FramesInState => machine.FramesInState;
        public int TickOrder => 0;

        public bool Grounded => Body.Collisions.Grounded;

        /// <summary>Digital left/right input: -1, 0 or +1.</summary>
        public int InputX
        {
            get
            {
                float x = Controls.Move.x;
                return Mathf.Abs(x) >= data.runInputThreshold ? (int)Mathf.Sign(x) : 0;
            }
        }

        private void Awake()
        {
            Body = GetComponent<KinematicBody2D>();
            if (inputReader != null) Controls = inputReader;

            Idle = new IdleState(this);
            Run = new RunState(this);
            Skid = new SkidState(this);
            Jump = new JumpState(this);
            Fall = new FallState(this);
            Land = new LandState(this);
            FastFall = new FastFallState(this);
            Dash = new DashState(this);
            WallSlide = new WallSlideState(this);
            WallJump = new WallJumpState(this);

            machine = new PlayerStateMachine();
            machine.ForceState(Fall);
        }

        private void OnEnable() => GameLoop.Register(this);
        private void OnDisable() => GameLoop.Unregister(this);

        /// <summary>Sets dependencies from code (tests, spawners). Overrides the Inspector values.</summary>
        public void Configure(MovementData movementData, IPlayerInput input, AbilityUnlocks unlocks)
        {
            data = movementData;
            Controls = input;
            abilities = unlocks;
        }

        /// <summary>Respawn at a position: stops all motion and starts falling.</summary>
        public void ResetTo(Vector2 position)
        {
            Body.Teleport(position);
            Velocity = Vector2.zero;
            FramesSinceGrounded = 0;
            SprintCharge = 0;
            coyoteAvailable = false;
            Controls?.Buffer?.ClearAll();
            machine.ForceState(Fall);
        }

        public void Tick()
        {
            if (!EnsureReady()) return;

            if (DashCooldown > 0) DashCooldown--;
            hasPendingDy = false;

            machine.Tick();

            // If the state applied gravity, use its exact displacement; otherwise move at constant speed.
            float dy = hasPendingDy ? pendingDy : Velocity.y * GameTime.TickDelta;
            float cornerCorrection = Velocity.y > 0f ? data.cornerCorrectionDistance : 0f;
            Body.Move(new Vector2(Velocity.x * GameTime.TickDelta, dy), cornerCorrection);

            ResolveCollisions();

            UpdatePlaceholderVisual();
        }

        /// <summary>
        /// Checks references before ticking. A missing MovementData is reported once and replaced with
        /// default values so the player still moves; missing input is reported once and the tick is skipped.
        /// </summary>
        private bool EnsureReady()
        {
            if (data == null)
            {
                Debug.LogError("PlayerController: 'Data' is empty, so default movement values are being used. " +
                               "Drag Assets/_Project/Data/MovementData into it, or run Margin > Wire Player References.", this);
                data = ScriptableObject.CreateInstance<MovementData>();
            }

            if (Controls == null || Controls.Buffer == null)
            {
                if (!reportedMissingInput)
                {
                    Debug.LogError("PlayerController: no working input. Assign the Input Reader field and make sure the " +
                                   "InputReader has its Actions asset (Margin > Wire Player References does both).", this);
                    reportedMissingInput = true;
                }
                return false;
            }
            return true;
        }

        /// <summary>Flips the placeholder to show facing, and leans it back while skidding. Replaced by the rig in Milestone 2.</summary>
        private void UpdatePlaceholderVisual()
        {
            if (visualRoot == null) return;
            visualRoot.localScale = new Vector3(Facing, 1f, 1f);
            // Positive Z rotation tips the top to the left, so lean against the direction of the slide.
            float lean = CurrentState == Skid ? Mathf.Sign(Velocity.x) * data.skidLeanDegrees : 0f;
            visualRoot.localRotation = Quaternion.Euler(0f, 0f, lean);
        }

        private void ResolveCollisions()
        {
            CollisionState c = Body.Collisions;

            if (c.HitCeiling && Velocity.y > 0f) Velocity.y = 0f;
            if ((c.HitWallLeft && Velocity.x < 0f) || (c.HitWallRight && Velocity.x > 0f)) Velocity.x = 0f;

            UpdateSprintCharge(c.Grounded);

            if (c.Grounded)
            {
                if (Velocity.y < 0f) Velocity.y = 0f;
                FramesSinceGrounded = 0;
                coyoteAvailable = true;
                AirDashesLeft = data.airDashes;
            }
            else
            {
                FramesSinceGrounded++;
            }
        }

        /// <summary>
        /// Sprint builds up while running at full speed on the ground and is kept through jumps as long as
        /// you keep holding the direction you are moving. A brief release while still above run speed keeps it
        /// (keyboard direction changes often pass through neutral); turning, stopping, or a wall resets it.
        /// </summary>
        private void UpdateSprintCharge(bool grounded)
        {
            bool holdingForward = InputX != 0 && Velocity.x != 0f && (int)Mathf.Sign(Velocity.x) == InputX;
            bool coasting = InputX == 0 && Mathf.Abs(Velocity.x) > data.runSpeed;

            if (holdingForward)
            {
                if (grounded && Mathf.Abs(Velocity.x) >= data.runSpeed - 0.001f) SprintCharge++;
            }
            else if (!coasting)
            {
                SprintCharge = 0;
            }
        }

        // ---------------- Helpers used by states ----------------

        /// <summary>Accelerate/decelerate toward the input direction and update facing.</summary>
        public void ApplyHorizontal(bool onGround)
        {
            if (onGround)
                Velocity.x = MovementMath.GroundStep(Velocity.x, InputX, data.runSpeed, data.sprintSpeed, IsSprinting,
                                                     data.GroundAccelStep, data.GroundDecelStep, data.SprintAccelStep,
                                                     data.SkidStep);
            else
                Velocity.x = MovementMath.AirStep(Velocity.x, InputX, data.runSpeed, data.AirAccelStep, data.AirDecelStep);

            if (InputX != 0) Facing = InputX;
        }

        /// <summary>Pushing against the direction of travel while faster than run speed (i.e. sprinting) starts a skid.</summary>
        public PlayerState CheckSkid()
        {
            if (InputX == 0 || Velocity.x == 0f) return null;
            bool reversing = (int)Mathf.Sign(Velocity.x) != InputX;
            return reversing && Mathf.Abs(Velocity.x) > data.runSpeed + 0.01f ? Skid : null;
        }

        /// <summary>Apply one tick of gravity (with fall multiplier and apex hang) capped at maxFallSpeed.</summary>
        public void ApplyGravity(float maxFallSpeed)
        {
            float gravity = data.Gravity(Velocity.y, Controls.JumpHeld);
            Velocity.y = MovementMath.VerticalStep(Velocity.y, gravity, maxFallSpeed, out pendingDy);
            hasPendingDy = true;
        }

        /// <summary>Called by JumpState.Enter: marks the jump as used so coyote time can't give a second one.</summary>
        public void StartJump()
        {
            Velocity.y = data.JumpVelocity;
            JumpStartY = Body.Position.y;
            coyoteAvailable = false;
        }

        /// <summary>
        /// Ground jump (including coyote time) or drop-through (Down + Jump on a one-way platform).
        /// Returns the state to enter, or null.
        /// </summary>
        public PlayerState CheckGroundJump()
        {
            bool onGround = Grounded;
            bool inCoyote = !onGround && coyoteAvailable && FramesSinceGrounded <= data.coyoteFrames;
            if (!onGround && !inCoyote) return null;

            if (onGround && Body.Collisions.OnOneWay && Controls.DownHeld)
            {
                if (!Controls.Buffer.Consume(BufferedAction.Jump)) return null;
                Body.DropThroughOneWay();
                coyoteAvailable = false;
                return Fall;
            }

            return Controls.Buffer.Consume(BufferedAction.Jump) ? Jump : null;
        }

        public PlayerState CheckDash()
        {
            if (!abilities.dash || DashCooldown > 0) return null;
            if (!Grounded && AirDashesLeft <= 0) return null;
            return Controls.Buffer.Consume(BufferedAction.Dash) ? Dash : null;
        }

        /// <summary>Airborne, falling, and holding into a wall you are touching.</summary>
        public PlayerState CheckWallSlide()
        {
            if (!abilities.wallCling || Grounded || Velocity.y > 0f || InputX == 0) return null;
            return Body.IsTouchingWall(InputX) ? WallSlide : null;
        }

        /// <summary>Jump pressed while airborne and touching a wall on either side.</summary>
        public PlayerState CheckWallJump()
        {
            if (!abilities.wallCling || Grounded) return null;

            int wall = Body.IsTouchingWall(1) ? 1 : Body.IsTouchingWall(-1) ? -1 : 0;
            if (wall == 0 || !Controls.Buffer.Consume(BufferedAction.Jump)) return null;

            WallDirection = wall;
            return WallJump;
        }
    }
}
