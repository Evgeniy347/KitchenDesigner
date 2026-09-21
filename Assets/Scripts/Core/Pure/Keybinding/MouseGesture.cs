using System;

namespace KitchenDesigner.Core.Keybinding
{
    public readonly struct MouseGesture : IEquatable<MouseGesture>
    {
        public static readonly MouseGesture Empty = default;

        public readonly MouseButtonKind Button;
        public readonly bool IsWheel;
        public readonly bool WithMotion;
        public readonly bool Ctrl;
        public readonly bool Alt;
        public readonly bool Shift;

        public MouseGesture(MouseButtonKind button, bool withMotion = false,
            bool ctrl = false, bool alt = false, bool shift = false)
        {
            Button = button;
            IsWheel = false;
            WithMotion = withMotion;
            Ctrl = ctrl;
            Alt = alt;
            Shift = shift;
        }

        private MouseGesture(bool isWheel, bool ctrl, bool alt, bool shift)
        {
            Button = MouseButtonKind.None;
            IsWheel = isWheel;
            WithMotion = false;
            Ctrl = ctrl;
            Alt = alt;
            Shift = shift;
        }

        public static MouseGesture Wheel(bool ctrl = false, bool alt = false, bool shift = false) =>
            new MouseGesture(isWheel: true, ctrl, alt, shift);

        public bool IsEmpty => !IsWheel && Button == MouseButtonKind.None;

        public bool Equals(MouseGesture other) =>
            Button == other.Button && IsWheel == other.IsWheel && WithMotion == other.WithMotion
                && Ctrl == other.Ctrl && Alt == other.Alt && Shift == other.Shift;

        public override bool Equals(object? obj) => obj is MouseGesture other && Equals(other);

        public override int GetHashCode()
        {
            int hash = 17;
            hash = hash * 31 + (int)Button;
            hash = hash * 31 + IsWheel.GetHashCode();
            hash = hash * 31 + WithMotion.GetHashCode();
            hash = hash * 31 + Ctrl.GetHashCode();
            hash = hash * 31 + Alt.GetHashCode();
            hash = hash * 31 + Shift.GetHashCode();
            return hash;
        }

        public static bool operator ==(MouseGesture a, MouseGesture b) => a.Equals(b);
        public static bool operator !=(MouseGesture a, MouseGesture b) => !a.Equals(b);

        private const string LeftText = "LMB";
        private const string RightText = "RMB";
        private const string MiddleText = "MMB";
        private const string XButton1Text = "MB4";
        private const string XButton2Text = "MB5";
        private const string WheelText = "Wheel";
        private const string MotionText = "Move";

        private static bool TryButtonForText(string text, out MouseButtonKind button)
        {
            switch (text)
            {
                case LeftText: button = MouseButtonKind.Left; return true;
                case RightText: button = MouseButtonKind.Right; return true;
                case MiddleText: button = MouseButtonKind.Middle; return true;
                case XButton1Text: button = MouseButtonKind.XButton1; return true;
                case XButton2Text: button = MouseButtonKind.XButton2; return true;
                default: button = MouseButtonKind.None; return false;
            }
        }

        private static string TextForButton(MouseButtonKind button) => button switch
        {
            MouseButtonKind.Left => LeftText,
            MouseButtonKind.Right => RightText,
            MouseButtonKind.Middle => MiddleText,
            MouseButtonKind.XButton1 => XButton1Text,
            MouseButtonKind.XButton2 => XButton2Text,
            _ => "",
        };

        public static string Format(MouseGesture gesture)
        {
            if (gesture.IsEmpty) return "";

            string baseToken = gesture.IsWheel ? WheelText : TextForButton(gesture.Button);
            if (!gesture.IsWheel && gesture.WithMotion) baseToken += "+" + MotionText;

            string modifiers = "";
            if (gesture.Ctrl) modifiers += "Ctrl+";
            if (gesture.Alt) modifiers += "Alt+";
            if (gesture.Shift) modifiers += "Shift+";

            return modifiers + baseToken;
        }

        public static bool TryParse(string? text, out MouseGesture gesture)
        {
            gesture = Empty;
            if (string.IsNullOrEmpty(text)) return true;

            var parts = text!.Split('+');
            int end = parts.Length;

            bool withMotion = false;
            if (end > 0 && string.Equals(parts[end - 1], MotionText, StringComparison.Ordinal))
            {
                withMotion = true;
                end--;
            }
            if (end == 0) return false;

            string baseToken = parts[end - 1];
            end--;

            bool ctrl = false, alt = false, shift = false;
            for (int i = 0; i < end; i++)
            {
                string modifier = parts[i];
                if (string.Equals(modifier, "Ctrl", StringComparison.OrdinalIgnoreCase)) ctrl = true;
                else if (string.Equals(modifier, "Alt", StringComparison.OrdinalIgnoreCase)) alt = true;
                else if (string.Equals(modifier, "Shift", StringComparison.OrdinalIgnoreCase)) shift = true;
                else return false;
            }

            if (string.Equals(baseToken, WheelText, StringComparison.Ordinal))
            {
                if (withMotion) return false;
                gesture = Wheel(ctrl, alt, shift);
                return true;
            }

            if (!TryButtonForText(baseToken, out var button)) return false;
            gesture = new MouseGesture(button, withMotion, ctrl, alt, shift);
            return true;
        }

        public static MouseGesture Parse(string? text) => TryParse(text, out var gesture) ? gesture : Empty;
    }
}
