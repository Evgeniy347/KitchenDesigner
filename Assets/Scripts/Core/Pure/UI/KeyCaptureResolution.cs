using UnityEngine;
using KitchenDesigner.Core.Keybinding;

namespace KitchenDesigner.Core.UI
{
    public static class KeyCaptureResolution
    {
        public static KeyChord? Resolve(KeyCode key, bool ctrl, bool alt, bool shift)
        {
            if (key == KeyCode.None) return null;
            if (IsModifierKey(key)) return null;
            return new KeyChord(key, ctrl, alt, shift);
        }

        public static bool IsModifierKey(KeyCode key) => key switch
        {
            KeyCode.LeftControl or KeyCode.RightControl => true,
            KeyCode.LeftAlt or KeyCode.RightAlt => true,
            KeyCode.LeftShift or KeyCode.RightShift => true,
            KeyCode.LeftCommand or KeyCode.RightCommand => true,
            KeyCode.LeftWindows or KeyCode.RightWindows => true,
            KeyCode.AltGr => true,
            _ => false,
        };
    }
}
