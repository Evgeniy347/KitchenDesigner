using System;
using UnityEngine;

namespace KitchenDesigner.Core.Keybinding
{
    public readonly struct KeyChord : IEquatable<KeyChord>
    {
        public static readonly KeyChord Empty = default;

        public readonly KeyCode Key;
        public readonly bool Ctrl;
        public readonly bool Alt;
        public readonly bool Shift;

        public KeyChord(KeyCode key, bool ctrl = false, bool alt = false, bool shift = false)
        {
            Key = key;
            Ctrl = ctrl;
            Alt = alt;
            Shift = shift;
        }

        public bool IsEmpty => Key == KeyCode.None;

        public bool Equals(KeyChord other) =>
            Key == other.Key && Ctrl == other.Ctrl && Alt == other.Alt && Shift == other.Shift;

        public override bool Equals(object? obj) => obj is KeyChord other && Equals(other);

        public override int GetHashCode()
        {
            int hash = 17;
            hash = hash * 31 + (int)Key;
            hash = hash * 31 + Ctrl.GetHashCode();
            hash = hash * 31 + Alt.GetHashCode();
            hash = hash * 31 + Shift.GetHashCode();
            return hash;
        }

        public static bool operator ==(KeyChord a, KeyChord b) => a.Equals(b);
        public static bool operator !=(KeyChord a, KeyChord b) => !a.Equals(b);

        private static readonly (KeyCode Key, string Text)[] KeyText =
        {
            (KeyCode.Alpha0, "0"),
            (KeyCode.Alpha1, "1"),
            (KeyCode.Alpha2, "2"),
            (KeyCode.Alpha3, "3"),
            (KeyCode.Alpha4, "4"),
            (KeyCode.Alpha5, "5"),
            (KeyCode.Alpha6, "6"),
            (KeyCode.Alpha7, "7"),
            (KeyCode.Alpha8, "8"),
            (KeyCode.Alpha9, "9"),
            (KeyCode.LeftArrow, "←"),
            (KeyCode.RightArrow, "→"),
            (KeyCode.UpArrow, "↑"),
            (KeyCode.DownArrow, "↓"),
            (KeyCode.Equals, "="),
            (KeyCode.Minus, "-"),
            (KeyCode.BackQuote, "`"),
            (KeyCode.Slash, "/"),
            (KeyCode.LeftBracket, "["),
            (KeyCode.RightBracket, "]"),
        };

        private static bool TryTextForKey(KeyCode key, out string text)
        {
            foreach (var pair in KeyText)
            {
                if (pair.Key != key) continue;
                text = pair.Text;
                return true;
            }
            text = "";
            return false;
        }

        private static bool TryKeyForText(string text, out KeyCode key)
        {
            foreach (var pair in KeyText)
            {
                if (!string.Equals(pair.Text, text, StringComparison.Ordinal)) continue;
                key = pair.Key;
                return true;
            }
            key = KeyCode.None;
            return false;
        }

        public static string Format(KeyChord chord)
        {
            if (chord.IsEmpty) return "";

            string keyText = TryTextForKey(chord.Key, out var text) ? text : chord.Key.ToString();

            string modifiers = "";
            if (chord.Ctrl) modifiers += "Ctrl+";
            if (chord.Alt) modifiers += "Alt+";
            if (chord.Shift) modifiers += "Shift+";

            return modifiers + keyText;
        }

        public static bool TryParse(string? text, out KeyChord chord)
        {
            chord = Empty;
            if (string.IsNullOrEmpty(text)) return true;

            var parts = text!.Split('+');
            bool ctrl = false, alt = false, shift = false;
            string keyText = parts[parts.Length - 1];

            for (int i = 0; i < parts.Length - 1; i++)
            {
                string modifier = parts[i];
                if (string.Equals(modifier, "Ctrl", StringComparison.OrdinalIgnoreCase)) ctrl = true;
                else if (string.Equals(modifier, "Alt", StringComparison.OrdinalIgnoreCase)) alt = true;
                else if (string.Equals(modifier, "Shift", StringComparison.OrdinalIgnoreCase)) shift = true;
                else return false;
            }

            if (TryKeyForText(keyText, out var mapped))
            {
                chord = new KeyChord(mapped, ctrl, alt, shift);
                return true;
            }

            if (Enum.TryParse<KeyCode>(keyText, ignoreCase: true, out var parsed) && parsed != KeyCode.None)
            {
                chord = new KeyChord(parsed, ctrl, alt, shift);
                return true;
            }

            return false;
        }

        public static KeyChord Parse(string? text) => TryParse(text, out var chord) ? chord : Empty;
    }
}
