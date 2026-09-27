using Margin.Core;
using Margin.Input;
using UnityEngine;

namespace Margin.Tests
{
    /// <summary>Scripted input for PlayMode tests. Set Move/JumpHeld/DownHeld; record presses into Buffer.</summary>
    public sealed class FakeInput : IPlayerInput
    {
        public readonly FrameCounter Clock = new FrameCounter();
        public FakeInput() { Buffer = new InputBuffer(Clock); }
        public Vector2 Move { get; set; }
        public bool JumpHeld { get; set; }
        public bool DownHeld { get; set; }
        public InputBuffer Buffer { get; }
    }
}
