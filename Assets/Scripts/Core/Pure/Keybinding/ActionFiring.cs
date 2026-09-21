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
            KeyBindings bindings, IKeyState state, InputAction action,
            ChordResolution resolution = ChordResolution.OccupancyAware) =>
            Fires(bindings, state, action, state.IsKeyDown, resolution);

        public static bool Held(
            KeyBindings bindings, IKeyState state, InputAction action,
            ChordResolution resolution = ChordResolution.OccupancyAware) =>
            Fires(bindings, state, action, state.IsKeyHeld, resolution);

        public static bool Up(
            KeyBindings bindings, IKeyState state, InputAction action,
            ChordResolution resolution = ChordResolution.OccupancyAware) =>
            Fires(bindings, state, action, state.IsKeyUp, resolution);

        private static bool Fires(
            KeyBindings bindings, IKeyState state, InputAction action,
            Func<KeyCode, bool> keyEvent, ChordResolution resolution)
        {
            bool ctrl = state.IsKeyHeld(KeyCode.LeftControl) || state.IsKeyHeld(KeyCode.RightControl);
            bool alt = state.IsKeyHeld(KeyCode.LeftAlt) || state.IsKeyHeld(KeyCode.RightAlt);
            bool shift = state.IsKeyHeld(KeyCode.LeftShift) || state.IsKeyHeld(KeyCode.RightShift);

            return FiresVia(bindings, action, bindings.PrimaryBinding(action).KeyOrEmpty,
                       keyEvent, ctrl, alt, shift, resolution)
                || FiresVia(bindings, action, bindings.AltBinding(action).KeyOrEmpty,
                       keyEvent, ctrl, alt, shift, resolution);
        }

        private static bool FiresVia(
            KeyBindings bindings, InputAction action, KeyChord chord,
            Func<KeyCode, bool> keyEvent, bool ctrl, bool alt, bool shift, ChordResolution resolution)
        {
            if (chord.IsEmpty) return false;
            if (!ChordMatch.ModifiersMatch(chord, ctrl, alt, shift, ChordMatchMode.RequiredModifiersOnly)) return false;
            if (!keyEvent(chord.Key)) return false;
            if (resolution == ChordResolution.RequiredModifiersOnly) return true;

            var effective = new KeyChord(chord.Key, ctrl, alt, shift);
            if (effective.Equals(chord)) return true;

            return !ClaimedByAnotherAction(bindings, action, effective);
        }

        private static bool ClaimedByAnotherAction(KeyBindings bindings, InputAction self, KeyChord chord)
        {
            foreach (var candidate in InputActionCatalog.All)
            {
                if (candidate == self) continue;
                if (bindings.PrimaryBinding(candidate).KeyOrEmpty == chord) return true;
                if (bindings.AltBinding(candidate).KeyOrEmpty == chord) return true;
            }
            return false;
        }
    }
}
