using System;
using UnityEngine;

namespace Margin.UI
{
    /// <summary>
    /// A page a MenuView can open from one of its buttons (Options, save slots, key bindings): its own hand-drawn
    /// card, driven by the same navigate/submit/cancel inputs. It calls the "closed" callback when it's done.
    /// </summary>
    public interface IMenuPanel
    {
        InkPanel Card { get; }
        bool IsOpen { get; }
        void Open(Action closed);
        void Close();
        void Update(Vector2 navigate, bool submit, bool cancel, float realFrames);
    }
}
