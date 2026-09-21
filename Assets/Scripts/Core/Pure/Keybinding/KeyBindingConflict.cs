using System.Collections.Generic;

namespace KitchenDesigner.Core.Keybinding
{
    public readonly struct KeyBindingConflict
    {
        public readonly InputBinding Binding;
        public readonly KeyChord Chord;
        public readonly IReadOnlyList<InputAction> Actions;

        public KeyBindingConflict(InputBinding binding, IReadOnlyList<InputAction> actions)
        {
            Binding = binding;
            Chord = binding.IsKey ? binding.Key : KeyChord.Empty;
            Actions = actions;
        }
    }
}
