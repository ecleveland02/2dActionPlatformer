using System;
using UnityEngine.InputSystem;

namespace Margin.Input
{
    /// <summary>
    /// Changing which key or button does an action (spec 14: controls rebinding). Each action has a "main" binding per
    /// device group (Keyboard, Gamepad): the first one listed. Rebinding listens for the next key/button of that
    /// group and puts it there; if another action already used that key, the two swap so nothing ends up unbound.
    /// Changes are overrides on top of MarginControls (the asset itself never changes), saved as JSON in options.json.
    /// </summary>
    public static class Rebinding
    {
        /// <summary>The actions players can rebind, in the order the Controls page lists them.</summary>
        public static readonly string[] Rebindable =
            { "Jump", "LightAttack", "HeavyAttack", "Special", "Dash", "Parry", "Grapple", "Heal" };

        public static bool InGroup(InputBinding b, string group) =>
            !string.IsNullOrEmpty(b.groups) && Array.IndexOf(b.groups.Split(';'), group) >= 0;

        /// <summary>Index of the action's first plain (non-composite) binding in the group, or -1.</summary>
        public static int MainIndex(InputAction action, string group)
        {
            if (action == null) return -1;
            var bindings = action.bindings;
            for (int i = 0; i < bindings.Count; i++)
            {
                InputBinding b = bindings[i];
                if (!b.isComposite && !b.isPartOfComposite && InGroup(b, group)) return i;
            }
            return -1;
        }

        /// <summary>
        /// Waits for the next key (keyboard group: keys and mouse buttons) or button (gamepad group) and binds it.
        /// Escape / Start cancel. <paramref name="done"/> gets true if the binding changed.
        /// Dispose the returned operation if you need to stop early.
        /// </summary>
        public static InputActionRebindingExtensions.RebindingOperation Start(InputActionMap map, InputAction action,
                                                                              string group, Action<bool> done)
        {
            int index = MainIndex(action, group);
            if (index < 0)
            {
                done?.Invoke(false);
                return null;
            }

            bool wasEnabled = action.enabled;
            action.Disable();   // an action can't be rebound while it's listening
            string oldPath = action.bindings[index].effectivePath;
            bool keyboard = group == "Keyboard";

            var op = action.PerformInteractiveRebinding(index)
                .WithCancelingThrough(keyboard ? "<Keyboard>/escape" : "<Gamepad>/start")
                .OnMatchWaitForAnother(0.1f);
            if (keyboard)
            {
                op.WithControlsHavingToMatchPath("<Keyboard>")
                  .WithControlsHavingToMatchPath("<Mouse>")
                  .WithControlsExcluding("<Mouse>/position")
                  .WithControlsExcluding("<Mouse>/delta")
                  .WithControlsExcluding("<Mouse>/scroll")
                  .WithControlsExcluding("<Keyboard>/anyKey");
            }
            else
            {
                // Sticks drift and are for moving, so only buttons, triggers and the d-pad count.
                op.WithControlsHavingToMatchPath("<Gamepad>")
                  .WithControlsExcluding("<Gamepad>/leftStick")
                  .WithControlsExcluding("<Gamepad>/rightStick")
                  .WithControlsExcluding("<Gamepad>/leftStick/*")
                  .WithControlsExcluding("<Gamepad>/rightStick/*");
            }

            op.OnComplete(o =>
            {
                string newPath = action.bindings[index].effectivePath;
                if (newPath != oldPath) SwapDuplicate(map, action, group, newPath, oldPath);
                o.Dispose();
                if (wasEnabled) action.Enable();
                done?.Invoke(newPath != oldPath);
            });
            op.OnCancel(o =>
            {
                o.Dispose();
                if (wasEnabled) action.Enable();
                done?.Invoke(false);
            });
            return op.Start();
        }

        /// <summary>Another rebindable action that used the new key gets the old one instead.</summary>
        private static void SwapDuplicate(InputActionMap map, InputAction changed, string group, string newPath, string oldPath)
        {
            if (map == null) return;
            foreach (string name in Rebindable)
            {
                InputAction other = map.FindAction(name);
                if (other == null || other == changed) continue;
                var bindings = other.bindings;
                for (int i = 0; i < bindings.Count; i++)
                {
                    InputBinding b = bindings[i];
                    if (b.isComposite || b.isPartOfComposite || !InGroup(b, group)) continue;
                    if (b.effectivePath == newPath) other.ApplyBindingOverride(i, oldPath);
                }
            }
        }
    }
}
