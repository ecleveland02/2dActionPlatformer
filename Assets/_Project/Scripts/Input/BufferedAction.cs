namespace Margin.Input
{
    /// <summary>
    /// Button actions that are remembered for a few frames by the InputBuffer.
    /// Movement (the stick / WASD) is continuous and is not buffered.
    /// Values are used as array indices, so keep them 0, 1, 2, ... with no gaps.
    /// </summary>
    public enum BufferedAction
    {
        Jump = 0,
        LightAttack = 1,
        HeavyAttack = 2,
        Special = 3,
        Dash = 4,
        Parry = 5,
    }
}
