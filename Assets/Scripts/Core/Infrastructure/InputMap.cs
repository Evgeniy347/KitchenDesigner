using System;
using KitchenDesigner.Core.Keybinding;
using UnityEngine;

namespace KitchenDesigner.Core
{
    public static class InputMap
    {
        public static bool Down(InputAction action) => Fires(action, Input.GetKeyDown);

        public static bool Held(InputAction action) => Fires(action, Input.GetKey);

        public static bool Up(InputAction action) => Fires(action, Input.GetKeyUp);

        private static bool Fires(InputAction action, Func<KeyCode, bool> keyEvent)
        {
            bool ctrl = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
            bool alt = Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);
            bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);

            var bindings = KitchenSettings.Instance.KeyBindings;
            return FiresFor(bindings.Primary(action), keyEvent, ctrl, alt, shift)
                || FiresFor(bindings.Alt(action), keyEvent, ctrl, alt, shift);
        }

        private static bool FiresFor(
            KeyChord chord, Func<KeyCode, bool> keyEvent, bool ctrl, bool alt, bool shift) =>
            ChordMatch.Fires(chord, !chord.IsEmpty && keyEvent(chord.Key), ctrl, alt, shift);
    }
}
