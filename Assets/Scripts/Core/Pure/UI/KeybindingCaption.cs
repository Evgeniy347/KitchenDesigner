using System.Collections.Generic;
using KitchenDesigner.Core.Keybinding;

namespace KitchenDesigner.Core.UI
{
    public static class KeybindingCaption
    {
        public const string MarkerText = "!";
        public const string EmptyCellText = "—";

        public static string CellText(InputBinding binding) =>
            binding.IsEmpty ? EmptyCellText : InputBindingDisplay.Of(binding);

        public static string WorstCaseOf(InputBinding binding) =>
            InputBindingDisplay.Of(WithEveryModifier(binding));

        public static int LongestBoundLength(KeyBindings bindings)
        {
            int longest = 0;
            foreach (var action in InputActionCatalog.All)
            {
                longest = Longer(longest, bindings.PrimaryBinding(action));
                longest = Longer(longest, bindings.AltBinding(action));
            }
            return longest;
        }

        private static int Longer(int longest, InputBinding binding)
        {
            int length = InputBindingDisplay.Of(binding).Length;
            return length > longest ? length : longest;
        }

        public static IEnumerable<string> WorstCases()
        {
            foreach (var gesture in EveryGestureShape())
                yield return WorstCaseOf(InputBinding.FromGesture(gesture));

            foreach (var action in InputActionCatalog.All)
            {
                yield return WorstCaseOf(KeyBindingDefaults.PrimaryOf(action));
                var alt = KeyBindingDefaults.AltOf(action);
                if (!alt.IsEmpty) yield return WorstCaseOf(alt);
            }
        }

        public static string Longest()
        {
            string longest = string.Empty;
            foreach (var caption in WorstCases())
                if (caption.Length > longest.Length) longest = caption;
            return longest;
        }

        public static int LongestLength() => Longest().Length;

        public static IEnumerable<MouseGesture> EveryGestureShape()
        {
            yield return MouseGesture.Wheel();
            foreach (var button in EveryButton())
            {
                yield return new MouseGesture(button);
                yield return new MouseGesture(button, withMotion: true);
            }
        }

        private static IEnumerable<MouseButtonKind> EveryButton()
        {
            yield return MouseButtonKind.Left;
            yield return MouseButtonKind.Right;
            yield return MouseButtonKind.Middle;
            yield return MouseButtonKind.XButton1;
            yield return MouseButtonKind.XButton2;
        }

        private static InputBinding WithEveryModifier(InputBinding binding)
        {
            if (binding.IsEmpty) return binding;

            if (binding.IsGesture)
            {
                var gesture = binding.Gesture;
                return InputBinding.FromGesture(gesture.IsWheel
                    ? MouseGesture.Wheel(ctrl: true, alt: true, shift: true)
                    : new MouseGesture(gesture.Button, gesture.WithMotion,
                        ctrl: true, alt: true, shift: true));
            }

            return InputBinding.FromKey(
                new KeyChord(binding.Key.Key, ctrl: true, alt: true, shift: true));
        }
    }
}
