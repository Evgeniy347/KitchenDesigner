using System;

namespace KitchenDesigner.Core.MCP
{
    internal sealed class AlreadyAppliedCommand : IUndoCommand, ISerializableCommand
    {
        private readonly CompositeCommand _inner;
        private bool _firstExecuteSkipped;

        public AlreadyAppliedCommand(CompositeCommand inner) => _inner = inner;

        public string Description => _inner.Description;

        public void Execute()
        {
            if (!_firstExecuteSkipped) { _firstExecuteSkipped = true; return; }
            _inner.Execute();
        }

        public void Undo() => _inner.Undo();

        public CommandRecord? ToRecord(Func<KitchenElement, int> indexOf) => _inner.ToRecord(indexOf);
    }
}
