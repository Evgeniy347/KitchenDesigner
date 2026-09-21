using KitchenDesigner.Core.Keybinding;

namespace KitchenDesigner.Core.UI
{
    internal sealed class SetInputBindingCommand : IUndoCommand
    {
        private readonly KeyBindings _bindings;
        private readonly InputAction _action;
        private readonly bool _primary;
        private readonly InputBinding _before;
        private readonly InputBinding _after;

        public string Description =>
            (_primary ? "Основная привязка: " : "Альтернативная привязка: ")
            + InputActionCatalog.DisplayNameOf(_action);

        public SetInputBindingCommand(KeyBindings bindings, InputAction action, bool primary,
            InputBinding before, InputBinding after)
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
