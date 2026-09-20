namespace KitchenDesigner.Core.Keybinding
{
    public enum ChordMatchMode
    {
        ExactModifiers,
        RequiredModifiersOnly,
    }

    public static class ChordMatch
    {
        public static bool ModifiersMatch(
            KeyChord chord, bool ctrlHeld, bool altHeld, bool shiftHeld,
            ChordMatchMode mode = ChordMatchMode.ExactModifiers) =>
            mode == ChordMatchMode.ExactModifiers
                ? chord.Ctrl == ctrlHeld && chord.Alt == altHeld && chord.Shift == shiftHeld
                : (!chord.Ctrl || ctrlHeld) && (!chord.Alt || altHeld) && (!chord.Shift || shiftHeld);

        public static bool Fires(
            KeyChord chord, bool keyEvent, bool ctrlHeld, bool altHeld, bool shiftHeld,
            ChordMatchMode mode = ChordMatchMode.ExactModifiers) =>
            !chord.IsEmpty && keyEvent && ModifiersMatch(chord, ctrlHeld, altHeld, shiftHeld, mode);
    }
}
