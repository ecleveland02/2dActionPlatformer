using System;
using Margin.Core;

namespace Margin.Player
{
    /// <summary>
    /// Pure movement formulas shared by MovementData and the player states.
    /// Velocities are in units/second, accelerations in units/second², durations in frames.
    /// No UnityEngine here so every formula can be unit tested.
    /// </summary>
    public static class MovementMath
    {
        private const float SnapEpsilon = 0.0001f;

        /// <summary>
        /// Gravity that makes a jump of <paramref name="height"/> peak exactly <paramref name="framesToApex"/>
        /// ticks after takeoff. From h = g·t²/2 with t = frames / 60.
        /// </summary>
        public static float RiseGravity(float height, int framesToApex)
        {
            float t = framesToApex * GameTime.TickDelta;
            return 2f * height / (t * t);
        }

        /// <summary>Takeoff speed for the same jump. From h = v·t/2 (speed falls linearly to 0 at the apex).</summary>
        public static float JumpVelocity(float height, int framesToApex)
        {
            float t = framesToApex * GameTime.TickDelta;
            return 2f * height / t;
        }

        /// <summary>
        /// Highest upward speed allowed after the jump button is released.
        /// It is chosen so the jump still peaks at <paramref name="minJumpHeight"/> above takeoff:
        /// with speed v and gravity g you rise v²/(2g) more, so v = sqrt(2g · heightStillAllowed).
        /// Once the player has already risen past the minimum, this is 0 and the jump stops rising at once.
        /// </summary>
        public static float JumpCutVelocity(float riseGravity, float minJumpHeight, float heightRisen)
        {
            float remaining = Math.Max(0f, minJumpHeight - heightRisen);
            return (float)Math.Sqrt(2f * riseGravity * remaining);
        }

        /// <summary>How much speed changes per tick to go from 0 to <paramref name="maxSpeed"/> in <paramref name="frames"/> ticks.</summary>
        public static float SpeedStepPerTick(float maxSpeed, int frames)
        {
            return maxSpeed / frames;
        }

        /// <summary>Moves <paramref name="current"/> toward <paramref name="target"/> by at most <paramref name="maxDelta"/>, never overshooting.</summary>
        public static float Approach(float current, float target, float maxDelta)
        {
            // Snap when the remaining gap is within float rounding of one step, so e.g. 20 steps of
            // 13/20 land exactly on 0 instead of leaving 0.000001 and costing an extra frame.
            if (Math.Abs(target - current) <= maxDelta + SnapEpsilon) return target;
            if (current < target) return Math.Min(current + maxDelta, target);
            if (current > target) return Math.Max(current - maxDelta, target);
            return target;
        }

        /// <summary>
        /// One tick of horizontal speed. <paramref name="inputX"/> is the stick/keys from -1 to 1.
        /// Uses the deceleration rate when letting go or turning around (so turning feels as snappy
        /// as stopping), and the acceleration rate when speeding up.
        /// </summary>
        public static float HorizontalStep(float velocityX, float inputX, float maxSpeed, float accelStep, float decelStep)
        {
            float target = inputX * maxSpeed;
            bool stopping = inputX == 0f;
            bool turning = velocityX != 0f && Math.Sign(velocityX) != Math.Sign(inputX);
            float step = stopping || turning ? decelStep : accelStep;
            return Approach(velocityX, target, step);
        }

        /// <summary>
        /// One tick of horizontal speed on the ground, including sprint.
        /// While sprinting and still holding the direction of travel, speed ramps from run speed up to
        /// sprint speed by <paramref name="sprintAccelStep"/> per tick. Otherwise it is a normal run step
        /// (which also eases back down to run speed when sprint ends).
        /// </summary>
        public static float GroundStep(float velocityX, int inputX, float runSpeed, float sprintSpeed, bool sprinting,
                                       float accelStep, float decelStep, float sprintAccelStep)
        {
            bool holdingForward = inputX != 0 && Math.Sign(velocityX) == inputX;
            if (sprinting && holdingForward && Math.Abs(velocityX) >= runSpeed)
                return Approach(velocityX, inputX * sprintSpeed, sprintAccelStep);
            return HorizontalStep(velocityX, inputX, runSpeed, accelStep, decelStep);
        }

        /// <summary>
        /// One tick of horizontal speed in the air. Holding the direction you are already moving keeps any
        /// speed above run speed (a sprint jump keeps its momentum), but air control can never accelerate
        /// you past run speed. Letting go or pushing the other way slows you down as normal.
        /// </summary>
        public static float AirStep(float velocityX, int inputX, float runSpeed, float accelStep, float decelStep)
        {
            bool holdingForward = inputX != 0 && Math.Sign(velocityX) == inputX;
            if (holdingForward && Math.Abs(velocityX) > runSpeed) return velocityX;
            return HorizontalStep(velocityX, inputX, runSpeed, accelStep, decelStep);
        }

        /// <summary>
        /// Gravity for this tick. Rising uses rise gravity, falling uses rise gravity × fall multiplier.
        /// Near the apex (|vy| below the threshold) gravity is multiplied by the apex hang multiplier.
        /// </summary>
        public static float Gravity(float velocityY, float riseGravity, float fallMultiplier,
                                    float apexThreshold, float apexMultiplier, bool apexHangActive)
        {
            float gravity = velocityY > 0f ? riseGravity : riseGravity * fallMultiplier;
            if (apexHangActive && Math.Abs(velocityY) < apexThreshold) gravity *= apexMultiplier;
            return gravity;
        }

        /// <summary>
        /// Applies one tick of gravity. Returns the new vertical speed (clamped to <paramref name="maxFallSpeed"/>)
        /// and outputs how far to move this tick.
        ///
        /// The move uses the average of the old and new speed. That is exact for constant gravity,
        /// so jumps reach exactly the configured height. The common "move by new speed" version
        /// undershoots: a 3.2 unit jump would only reach about 3.05.
        /// </summary>
        public static float VerticalStep(float velocityY, float gravity, float maxFallSpeed, out float displacement)
        {
            float next = Math.Max(velocityY - gravity * GameTime.TickDelta, -maxFallSpeed);
            displacement = (velocityY + next) * 0.5f * GameTime.TickDelta;
            return next;
        }
    }
}
