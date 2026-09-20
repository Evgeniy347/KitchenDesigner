using KitchenDesigner.Core.Keybinding;

namespace KitchenDesigner.Core.UI
{
    internal sealed class SetKeyBindingCommand : IUndoCommand
    {
        private readonly KeyBindings _bindings;
        private readonly InputAction _action;
        private readonly bool _primary;
        private readonly KeyChord _before;
        private readonly KeyChord _after;

        public string Description =>
            (_primary ? "Основная клавиша: " : "Альтернативная клавиша: ")
            + InputActionCatalog.DisplayNameOf(_action);

        public SetKeyBindingCommand(KeyBindings bindings, InputAction action, bool primary,
            KeyChord before, KeyChord after)
        {
            _bindings = bindings;
            _action = action;
            _primary = primary;
            _before = before;
            _after = after;
        }

        public void Execute() => KeybindingEditing.Write(_bindings, _action, _primary, _after);

        public void Undo() => KeybindingEditing.Write(_bindings, _action, _primary, _before);
    }
}
