using UnityEngine;

namespace Margin.Player
{
    /// <summary>
    /// All movement tuning for a character (Section 5.2 and the dash defaults in Section 8).
    /// Create via Assets > Create > Margin > Movement Data. Speeds are units/second, durations are frames.
    ///
    /// The jump input buffer window is NOT here: it lives in InputBufferSettings, so there is one source of truth.
    /// </summary>
    [CreateAssetMenu(fileName = "MovementData", menuName = "Margin/Movement Data")]
    public sealed class MovementData : ScriptableObject
    {
        [Header("Run")]
        [Min(0f)] public float runSpeed = 9f;
        [Tooltip("Stick tilt (0 to 1) needed to count as pressing left/right. Running is digital: full speed or none.")]
        [Range(0.05f, 1f)] public float runInputThreshold = 0.25f;
        [Tooltip("Frames to go from standing to full run speed on the ground.")]
        [Min(1)] public int groundAccelerationFrames = 4;
        [Tooltip("Frames to go from full run speed to stopped on the ground. Also used when turning around.")]
        [Min(1)] public int groundDecelerationFrames = 3;
        [Tooltip("Frames to reach full speed in the air.")]
        [Min(1)] public int airAccelerationFrames = 8;
        [Tooltip("Frames to stop in the air with no input. Not in the spec; defaults to match air acceleration.")]
        [Min(1)] public int airDecelerationFrames = 8;

        [Header("Sprint")]
        [Tooltip("Top speed after running long enough. Sprint happens automatically; there is no sprint button.")]
        [Min(0f)] public float sprintSpeed = 13f;
        [Tooltip("Frames of continuous running at full run speed before sprint starts.")]
        [Min(1)] public int framesToStartSprint = 40;
        [Tooltip("Frames to ramp from run speed up to sprint speed.")]
        [Min(1)] public int sprintAccelerationFrames = 15;
        [Tooltip("Frames to skid from full sprint speed to a stop when turning around. Jump and dash cancel the skid. " +
                 "Letting go at sprint speed also coasts down to run speed at this rate.")]
        [Min(1)] public int skidFrames = 20;

        [Header("Jump")]
        [Tooltip("Apex height when jump is held, in units.")]
        [Min(0.1f)] public float jumpHeight = 3.2f;
        [Tooltip("Apex height when jump is tapped (released early), in units.")]
        [Min(0.1f)] public float minJumpHeight = 1.2f;
        [Tooltip("Ticks from takeoff to apex on a full jump. Rise gravity is derived from this and jumpHeight.")]
        [Min(1)] public int framesToApex = 22;
        [Tooltip("Frames after walking off a ledge during which a jump still works.")]
        [Min(0)] public int coyoteFrames = 6;

        [Header("Fall")]
        [Tooltip("Falling gravity = rise gravity × this. Higher = snappier descent.")]
        [Min(1f)] public float fallGravityMultiplier = 1.8f;
        [Min(0f)] public float maxFallSpeed = 20f;
        [Tooltip("Speed set when pressing Down in the air after the apex.")]
        [Min(0f)] public float fastFallSpeed = 28f;

        [Header("Apex Hang")]
        [Tooltip("Gravity is reduced while |vertical speed| is below this.")]
        [Min(0f)] public float apexHangThreshold = 1.5f;
        [Range(0f, 1f)] public float apexHangGravityMultiplier = 0.5f;
        [Tooltip("Only hang while jump is held (Celeste style). Tapped jumps then stay crisp. Off = spec-literal, always hang.")]
        public bool apexHangRequiresJumpHeld = true;

        [Header("Ground and Collision Helpers")]
        [Tooltip("Frames spent in the Land state after touching down.")]
        [Min(0)] public int landFrames = 2;
        [Tooltip("Max sideways nudge, in units, to slide past a ceiling corner instead of bonking.")]
        [Min(0f)] public float cornerCorrectionDistance = 0.15f;

        [Header("Dash")]
        [Min(1)] public int dashFrames = 10;
        [Min(0f)] public float dashSpeed = 16f;
        [Tooltip("Invulnerable frames at the start of the dash.")]
        [Min(0)] public int dashInvulnerableFrames = 8;
        [Min(0)] public int airDashes = 1;
        [Tooltip("Frames after a ground dash before another dash is allowed.")]
        [Min(0)] public int dashCooldownFrames = 20;

        [Header("Walls (requires Wall Cling ability)")]
        [Tooltip("Max fall speed while sliding down a wall.")]
        [Min(0f)] public float wallSlideSpeed = 4f;
        [Tooltip("Horizontal speed pushed away from the wall on a wall jump.")]
        [Min(0f)] public float wallJumpHorizontalSpeed = 9f;
        [Tooltip("Frames after a wall jump during which left/right input is ignored, so you can't instantly drift back.")]
        [Min(0)] public int wallJumpControlLockFrames = 8;

        // Derived values. Computed from the fields above so designers only tune the intuitive numbers.
        public float RiseGravity => MovementMath.RiseGravity(jumpHeight, framesToApex);
        public float FallGravity => RiseGravity * fallGravityMultiplier;
        public float JumpVelocity => MovementMath.JumpVelocity(jumpHeight, framesToApex);
        public float GroundAccelStep => MovementMath.SpeedStepPerTick(runSpeed, groundAccelerationFrames);
        public float GroundDecelStep => MovementMath.SpeedStepPerTick(runSpeed, groundDecelerationFrames);
        public float AirAccelStep => MovementMath.SpeedStepPerTick(runSpeed, airAccelerationFrames);
        public float AirDecelStep => MovementMath.SpeedStepPerTick(runSpeed, airDecelerationFrames);
        public float SprintAccelStep => MovementMath.SpeedStepPerTick(sprintSpeed - runSpeed, sprintAccelerationFrames);
        public float SkidStep => MovementMath.SpeedStepPerTick(sprintSpeed, skidFrames);

        public float JumpCutVelocity(float heightRisen)
        {
            return MovementMath.JumpCutVelocity(RiseGravity, minJumpHeight, heightRisen);
        }

        public float Gravity(float velocityY, bool jumpHeld)
        {
            bool hang = !apexHangRequiresJumpHeld || jumpHeld;
            return MovementMath.Gravity(velocityY, RiseGravity, fallGravityMultiplier,
                                        apexHangThreshold, apexHangGravityMultiplier, hang);
        }

        // Called by Unity when a value changes in the Inspector. Keeps related values consistent.
        private void OnValidate()
        {
            if (minJumpHeight > jumpHeight) minJumpHeight = jumpHeight;
            if (sprintSpeed < runSpeed) sprintSpeed = runSpeed;
            if (fastFallSpeed < maxFallSpeed) fastFallSpeed = maxFallSpeed;
            if (dashInvulnerableFrames > dashFrames) dashInvulnerableFrames = dashFrames;
        }
    }
}
