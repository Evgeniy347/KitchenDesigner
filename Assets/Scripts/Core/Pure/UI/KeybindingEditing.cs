using KitchenDesigner.Core.Keybinding;

namespace KitchenDesigner.Core.UI
{
    public static class KeybindingEditing
    {
        public static InputBinding Read(KeyBindings bindings, InputAction action, bool primary) =>
            primary ? bindings.PrimaryBinding(action) : bindings.AltBinding(action);

        public static void Write(KeyBindings bindings, InputAction action, bool primary,
            InputBinding binding)
        {
            if (primary) bindings.SetPrimaryBinding(action, binding);
            else bindings.SetAltBinding(action, binding);
        }

        public static void Clear(KeyBindings bindings, InputAction action, bool primary) =>
            Write(bindings, action, primary, InputBinding.Empty);
    }
}
