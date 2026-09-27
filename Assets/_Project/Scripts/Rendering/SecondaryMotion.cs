using System;

namespace Margin.Rendering
{
    /// <summary>
    /// Follow-through for procedural animation, in pure C# for testing. Each joint follows its animated angle
    /// through a damped spring instead of copying it, so a head or a free arm lags a little behind the body,
    /// overshoots, and settles, the way loose limbs do. Weight per joint: 0 = exact animated angle (feet and
    /// the sword arm stay crisp), 1 = fully sprung.
    ///
    /// The spring: acceleration = w^2 * (target - angle) - 2 * damping * w * velocity, with w = 2 pi * frequency.
    /// Damping below 1 overshoots (bouncy), 1 settles without overshoot.
    /// </summary>
    public sealed class SecondaryMotion
    {
        private readonly float[] angle = new float[FigurePose.AllJoints.Length];
        private readonly float[] velocity = new float[FigurePose.AllJoints.Length];
        private bool started;

        /// <summary>Snaps every spring to a pose with no motion (e.g. after a teleport or respawn).</summary>
        public void Reset(FigurePose pose)
        {
            for (int i = 0; i < FigurePose.AllJoints.Length; i++)
            {
                angle[i] = pose.Get(FigurePose.AllJoints[i]);
                velocity[i] = 0f;
            }
            started = true;
        }

        /// <summary>
        /// Advances every spring one tick toward <paramref name="target"/> and returns the sprung pose.
        /// <paramref name="weights"/> is indexed like FigurePose.AllJoints.
        /// </summary>
        public FigurePose Step(FigurePose target, float[] weights, float frequencyHz, float damping, float dt)
        {
            if (!started) Reset(target);

            float w = 2f * (float)Math.PI * Math.Max(0.01f, frequencyHz);
            FigurePose result = target;
            for (int i = 0; i < FigurePose.AllJoints.Length; i++)
            {
                PoseJoint joint = FigurePose.AllJoints[i];
                float goal = target.Get(joint);
                // Work relative to the current angle so a target across the +-180 seam is taken the short way.
                float error = FigurePose.DeltaAngle(angle[i], goal);
                velocity[i] += (w * w * error - 2f * damping * w * velocity[i]) * dt;   // semi-implicit Euler: stable at 60 Hz
                angle[i] = FigurePose.NormalizeAngle(angle[i] + velocity[i] * dt);

                float weight = weights != null && i < weights.Length ? Math.Max(0f, Math.Min(1f, weights[i])) : 0f;
                if (weight > 0f) result.Set(joint, FigurePose.LerpAngle(goal, angle[i], weight));
            }
            return result;
        }
    }
}
