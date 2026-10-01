using System;
using KitchenDesigner.Core.Keybinding;

namespace KitchenDesigner.Core.UI
{
    public static class InputBindingDisplay
    {
        private static readonly LocalizedCache<(MouseButtonKind Button, string Reading)[]> ButtonReadingsCache =
            new LocalizedCache<(MouseButtonKind Button, string Reading)[]>(() => new (MouseButtonKind Button, string Reading)[] {
            (MouseButtonKind.Left, Loc.T("input.mouse.left")),
            (MouseButtonKind.Right, Loc.T("input.mouse.right")),
            (MouseButtonKind.Middle, Loc.T("input.mouse.middle")),
            (MouseButtonKind.XButton1, Loc.T("input.mouse.button4")),
            (MouseButtonKind.XButton2, Loc.T("input.mouse.button5")),
        });

        private static (MouseButtonKind Button, string Reading)[] ButtonReadings => ButtonReadingsCache.Value;

        private static string WheelReading => Loc.T("input.mouse.wheel");
        private static string MotionReading => Loc.T("input.mouse.withMotion");

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
