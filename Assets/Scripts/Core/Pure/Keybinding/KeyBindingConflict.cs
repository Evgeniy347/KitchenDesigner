using System.Collections.Generic;

namespace KitchenDesigner.Core.Keybinding
{
    public readonly struct KeyBindingConflict
    {
        public readonly KeyChord Chord;
        public readonly IReadOnlyList<InputAction> Actions;

        public KeyBindingConflict(KeyChord chord, IReadOnlyList<InputAction> actions)
        {
            Chord = chord;
            Actions = actions;
        }
    }
}
