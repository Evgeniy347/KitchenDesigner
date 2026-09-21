using System;
using KitchenDesigner.Core.Keybinding;

namespace KitchenDesigner.Core.UI
{
    public static class InputBindingDisplay
    {
        private static readonly (MouseButtonKind Button, string Reading)[] ButtonReadings =
        {
            (MouseButtonKind.Left, "ЛКМ"),
            (MouseButtonKind.Right, "ПКМ"),
            (MouseButtonKind.Middle, "СКМ"),
            (MouseButtonKind.XButton1, "Кнопка 4"),
            (MouseButtonKind.XButton2, "Кнопка 5"),
        };

        private const string WheelReading = "Колесо";
        private const string MotionReading = " с движением";

        public static string Of(InputBinding binding)
        {
            if (binding.IsEmpty) return string.Empty;
            return binding.IsGesture ? OfGesture(binding.Gesture) : KeyChordDisplay.Of(binding.Key);
        }

        public static string OfGesture(MouseGesture gesture)
        {
            string stored = MouseGesture.Format(gesture);
            if (!TryReadingOf(gesture, out string reading)) return stored;

            var bare = gesture.IsWheel
                ? MouseGesture.Wheel()
                : new MouseGesture(gesture.Button, gesture.WithMotion);
            string bareToken = MouseGesture.Format(bare);
            if (bareToken.Length == 0 || !stored.EndsWith(bareToken, StringComparison.Ordinal))
                return stored;

            return stored.Substring(0, stored.Length - bareToken.Length) + reading;
        }

        public static bool TryReadingOf(MouseGesture gesture, out string reading)
        {
            if (gesture.IsEmpty)
            {
                reading = string.Empty;
                return false;
            }

            if (gesture.IsWheel)
            {
                reading = WheelReading;
                return true;
            }

            foreach (var pair in ButtonReadings)
            {
                if (pair.Button != gesture.Button) continue;
                reading = gesture.WithMotion ? pair.Reading + MotionReading : pair.Reading;
                return true;
            }

            reading = string.Empty;
            return false;
        }
    }
}
