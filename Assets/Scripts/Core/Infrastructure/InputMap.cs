using System;
using KitchenDesigner.Core.Keybinding;
using UnityEngine;

namespace KitchenDesigner.Core
{
    public static class InputMap
    {
        public static bool Down(InputAction action, ChordMatchMode mode = ChordMatchMode.ExactModifiers) =>
            Fires(action, Input.GetKeyDown, mode);

        public static bool Held(InputAction action, ChordMatchMode mode = ChordMatchMode.ExactModifiers) =>
            Fires(action, Input.GetKey, mode);

        public static bool Up(InputAction action, ChordMatchMode mode = ChordMatchMode.ExactModifiers) =>
            Fires(action, Input.GetKeyUp, mode);

        private static bool Fires(InputAction action, Func<KeyCode, bool> keyEvent, ChordMatchMode mode)
        {
            bool ctrl = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
            bool alt = Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);
            bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);

            var bindings = KitchenSettings.Instance.KeyBindings;
            return FiresFor(bindings.Primary(action), keyEvent, ctrl, alt, shift, mode)
                || FiresFor(bindings.Alt(action), keyEvent, ctrl, alt, shift, mode);
        }

        private static bool FiresFor(
            KeyChord chord, Func<KeyCode, bool> keyEvent, bool ctrl, bool alt, bool shift, ChordMatchMode mode) =>
            ChordMatch.Fires(chord, !chord.IsEmpty && keyEvent(chord.Key), ctrl, alt, shift, mode);
    }
}
