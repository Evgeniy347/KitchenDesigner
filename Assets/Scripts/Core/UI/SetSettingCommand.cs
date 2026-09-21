using System;
using System.Collections.Generic;

namespace KitchenDesigner.Core.UI
{
    internal sealed class SetSettingCommand<T> : IUndoCommand
    {
        private readonly string _title;
        private readonly Action<T> _write;
        private readonly Action? _afterApply;
        private readonly T _before;
        private readonly T _after;

        public string Description => _title;

        public SetSettingCommand(string title, Action<T> write, T before, T after,
            Action? afterApply = null)
        {
            _title = title;
            _write = write;
            _before = before;
            _after = after;
            _afterApply = afterApply;
        }

        public void Execute() => Apply(_after);

        public void Undo() => Apply(_before);

        private void Apply(T value)
        {
            _write(value);
            _afterApply?.Invoke();
        }
    }

    internal static class SetSettingCommand
    {
        public static void Push<T>(string title, Action<T> write, T before, T after,
            Action? afterApply = null)
        {
            if (EqualityComparer<T>.Default.Equals(before, after)) return;
            CommandStack.Execute(new SetSettingCommand<T>(title, write, before, after, afterApply));
        }
    }
}
