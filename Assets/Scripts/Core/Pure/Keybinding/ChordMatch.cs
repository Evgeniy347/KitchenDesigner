namespace KitchenDesigner.Core.Keybinding
{
    public static class ChordMatch
    {
        public static bool ModifiersMatch(KeyChord chord, bool ctrlHeld, bool altHeld, bool shiftHeld) =>
            chord.Ctrl == ctrlHeld && chord.Alt == altHeld && chord.Shift == shiftHeld;

        public static bool Fires(KeyChord chord, bool keyEvent, bool ctrlHeld, bool altHeld, bool shiftHeld) =>
            !chord.IsEmpty && keyEvent && ModifiersMatch(chord, ctrlHeld, altHeld, shiftHeld);
    }
}
