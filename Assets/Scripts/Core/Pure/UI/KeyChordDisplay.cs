using System;
using UnityEngine;
using KitchenDesigner.Core.Keybinding;

namespace KitchenDesigner.Core.UI
{
    public static class KeyChordDisplay
    {
        private static readonly (KeyCode Key, string Reading)[] NumpadReadings =
        {
            (KeyCode.Keypad0, "Num 0"),
            (KeyCode.Keypad1, "Num 1"),
            (KeyCode.Keypad2, "Num 2"),
            (KeyCode.Keypad3, "Num 3"),
            (KeyCode.Keypad4, "Num 4"),
            (KeyCode.Keypad5, "Num 5"),
            (KeyCode.Keypad6, "Num 6"),
            (KeyCode.Keypad7, "Num 7"),
            (KeyCode.Keypad8, "Num 8"),
            (KeyCode.Keypad9, "Num 9"),
            (KeyCode.KeypadPeriod, "Num ."),
            (KeyCode.KeypadDivide, "Num /"),
            (KeyCode.KeypadMultiply, "Num *"),
            (KeyCode.KeypadMinus, "Num -"),
            (KeyCode.KeypadPlus, "Num +"),
            (KeyCode.KeypadEnter, "Num Enter"),
            (KeyCode.KeypadEquals, "Num ="),
        };

        public static string Of(KeyChord chord)
        {
            string stored = KeyChord.Format(chord);
            if (!TryReadingOf(chord.Key, out string reading)) return stored;

            string bareKey = KeyChord.Format(new KeyChord(chord.Key));
            if (bareKey.Length == 0 || !stored.EndsWith(bareKey, StringComparison.Ordinal))
                return stored;

            return stored.Substring(0, stored.Length - bareKey.Length) + reading;
        }

        public static bool TryReadingOf(KeyCode key, out string reading)
        {
            foreach (var pair in NumpadReadings)
            {
                if (pair.Key != key) continue;
                reading = pair.Reading;
                return true;
            }
            reading = "";
            return false;
        }
    }
}
