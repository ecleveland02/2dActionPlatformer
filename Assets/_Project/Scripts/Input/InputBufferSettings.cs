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
