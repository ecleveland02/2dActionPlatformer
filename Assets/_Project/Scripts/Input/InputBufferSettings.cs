using UnityEngine;

namespace Margin.Input
{
    /// <summary>
    /// Tunable buffer windows, in frames. Create via Assets > Create > Margin > Input Buffer Settings
    /// and assign it to the InputReader. Section 5.3: default 6 frames, configurable per action.
    /// </summary>
    [CreateAssetMenu(fileName = "InputBufferSettings", menuName = "Margin/Input Buffer Settings")]
    public sealed class InputBufferSettings : ScriptableObject
    {
        [Min(1)] public int jumpFrames = 6;
        [Min(1)] public int lightAttackFrames = 6;
        [Min(1)] public int heavyAttackFrames = 6;
        [Min(1)] public int specialFrames = 6;
        [Min(1)] public int dashFrames = 6;
        [Min(1)] public int parryFrames = 6;

        [Header("Attack button (left click / right trigger)")]
        [Tooltip("Frames the Attack button must be held to count as a heavy attack. Released sooner = light attack " +
                 "(which starts on release). Lower = heavies come out faster but slow taps may turn into heavies.")]
        [Min(2)] public int attackHoldFrames = 10;

        public void ApplyTo(InputBuffer buffer)
        {
            buffer.SetWindow(BufferedAction.Jump, jumpFrames);
            buffer.SetWindow(BufferedAction.LightAttack, lightAttackFrames);
            buffer.SetWindow(BufferedAction.HeavyAttack, heavyAttackFrames);
            buffer.SetWindow(BufferedAction.Special, specialFrames);
            buffer.SetWindow(BufferedAction.Dash, dashFrames);
            buffer.SetWindow(BufferedAction.Parry, parryFrames);
        }
    }
}
