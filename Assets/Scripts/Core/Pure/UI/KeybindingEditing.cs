using KitchenDesigner.Core.Keybinding;

namespace KitchenDesigner.Core.UI
{
    public static class KeybindingEditing
    {
        public static KeyChord Read(KeyBindings bindings, InputAction action, bool primary) =>
            primary ? bindings.Primary(action) : bindings.Alt(action);

        public static void Write(KeyBindings bindings, InputAction action, bool primary, KeyChord chord)
        {
            if (primary) bindings.SetPrimary(action, chord);
            else bindings.SetAlt(action, chord);
        }

        public static void Clear(KeyBindings bindings, InputAction action, bool primary) =>
            Write(bindings, action, primary, KeyChord.Empty);
    }
}
