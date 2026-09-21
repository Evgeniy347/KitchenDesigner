using System;
using UnityEngine;

namespace KitchenDesigner.Core.Keybinding
{
    public enum ChordResolution
    {
        OccupancyAware,
        RequiredModifiersOnly,
    }

    public static class ActionFiring
    {
        public static bool Down(
            KeyBindings bindings, IInputState state, InputAction action,
            ChordResolution resolution = ChordResolution.OccupancyAware) =>
            Fires(bindings, state, action, state.IsKeyDown, state.IsButtonDown, resolution);

        public static bool Held(
            KeyBindings bindings, IInputState state, InputAction action,
            ChordResolution resolution = ChordResolution.OccupancyAware) =>
            Fires(bindings, state, action, state.IsKeyHeld, state.IsButtonHeld, resolution);

        public static bool Up(
            KeyBindings bindings, IInputState state, InputAction action,
            ChordResolution resolution = ChordResolution.OccupancyAware) =>
            Fires(bindings, state, action, state.IsKeyUp, state.IsButtonUp, resolution);

        private static bool Fires(
            KeyBindings bindings, IInputState state, InputAction action,
            Func<KeyCode, bool> keyEvent, Func<MouseButtonKind, bool> buttonEvent, ChordResolution resolution)
        {
            bool ctrl = state.IsKeyHeld(KeyCode.LeftControl) || state.IsKeyHeld(KeyCode.RightControl);
            bool alt = state.IsKeyHeld(KeyCode.LeftAlt) || state.IsKeyHeld(KeyCode.RightAlt);
            bool shift = state.IsKeyHeld(KeyCode.LeftShift) || state.IsKeyHeld(KeyCode.RightShift);

            return FiresVia(bindings, action, bindings.PrimaryBinding(action),
                       keyEvent, buttonEvent, state.WheelMoved, ctrl, alt, shift, resolution)
                || FiresVia(bindings, action, bindings.AltBinding(action),
                       keyEvent, buttonEvent, state.WheelMoved, ctrl, alt, shift, resolution);
        }

        private static bool FiresVia(
            KeyBindings bindings, InputAction action, InputBinding binding,
            Func<KeyCode, bool> keyEvent, Func<MouseButtonKind, bool> buttonEvent, bool wheelMoved,
            bool ctrl, bool alt, bool shift, ChordResolution resolution)
        {
            if (binding.IsEmpty) return false;

            return binding.IsKey
                ? FiresKey(bindings, action, binding.Key, keyEvent, ctrl, alt, shift, resolution)
                : FiresGesture(
                    bindings, action, binding.Gesture, buttonEvent, wheelMoved, ctrl, alt, shift, resolution);
        }

        private static bool FiresKey(
            KeyBindings bindings, InputAction action, KeyChord chord,
            Func<KeyCode, bool> keyEvent, bool ctrl, bool alt, bool shift, ChordResolution resolution)
        {
            if (chord.IsEmpty) return false;
            if (!ChordMatch.ModifiersMatch(chord, ctrl, alt, shift, ChordMatchMode.RequiredModifiersOnly)) return false;
            if (!keyEvent(chord.Key)) return false;
            if (resolution == ChordResolution.RequiredModifiersOnly) return true;

            var effective = new KeyChord(chord.Key, ctrl, alt, shift);
            if (effective.Equals(chord)) return true;

            return !KeyClaimedByAnotherAction(bindings, action, effective);
        }

        private static bool FiresGesture(
            KeyBindings bindings, InputAction action, MouseGesture gesture,
            Func<MouseButtonKind, bool> buttonEvent, bool wheelMoved,
            bool ctrl, bool alt, bool shift, ChordResolution resolution)
        {
            if (gesture.IsEmpty) return false;
            if (!GestureModifiersMatch(gesture, ctrl, alt, shift)) return false;
            if (gesture.IsWheel) { if (!wheelMoved) return false; }
            else if (!buttonEvent(gesture.Button)) return false;
            if (resolution == ChordResolution.RequiredModifiersOnly) return true;

            var effective = gesture.IsWheel
                ? MouseGesture.Wheel(ctrl, alt, shift)
                : new MouseGesture(gesture.Button, gesture.WithMotion, ctrl, alt, shift);
            if (effective.Equals(gesture)) return true;

            return !GestureClaimedByAnotherAction(bindings, action, effective);
        }

        private static bool GestureModifiersMatch(MouseGesture gesture, bool ctrl, bool alt, bool shift) =>
            (!gesture.Ctrl || ctrl) && (!gesture.Alt || alt) && (!gesture.Shift || shift);

        private static bool KeyClaimedByAnotherAction(KeyBindings bindings, InputAction self, KeyChord chord)
        {
            foreach (var candidate in InputActionCatalog.All)
            {
                if (candidate == self) continue;
                if (bindings.PrimaryBinding(candidate).Key == chord) return true;
                if (bindings.AltBinding(candidate).Key == chord) return true;
            }
            return false;
        }

        private static bool GestureClaimedByAnotherAction(KeyBindings bindings, InputAction self, MouseGesture gesture)
        {
            foreach (var candidate in InputActionCatalog.All)
            {
                if (candidate == self) continue;
                if (bindings.PrimaryBinding(candidate).Gesture == gesture) return true;
                if (bindings.AltBinding(candidate).Gesture == gesture) return true;
            }
            return false;
        }
    }
}
