using System;
using System.Collections.Generic;

namespace KitchenDesigner.Core
{
    public class CommandStackInstance : ICommandStack
    {
        private readonly List<IUndoCommand> _undoStack = new List<IUndoCommand>();
        private readonly List<IUndoCommand> _redoStack = new List<IUndoCommand>();
        private const int MaxUndo = 1000;

        public bool CanUndo => _undoStack.Count > 0;
        public bool CanRedo => _redoStack.Count > 0;
        public int UndoCount => _undoStack.Count;
        public int RedoCount => _redoStack.Count;

        public void Execute(IUndoCommand command)
        {
            command.Execute();
            _undoStack.Add(command);
            if (_undoStack.Count > MaxUndo)
                _undoStack.RemoveAt(0);
            _redoStack.Clear();
        }

        public void Undo()
        {
            if (_undoStack.Count == 0) return;
            int idx = _undoStack.Count - 1;
            var cmd = _undoStack[idx];
            _undoStack.RemoveAt(idx);
            cmd.Undo();
            _redoStack.Add(cmd);
        }

        public void Redo()
        {
            if (_redoStack.Count == 0) return;
            int idx = _redoStack.Count - 1;
            var cmd = _redoStack[idx];
            _redoStack.RemoveAt(idx);
            cmd.Execute();
            _undoStack.Add(cmd);
        }

        public void Clear()
        {
            _undoStack.Clear();
            _redoStack.Clear();
        }

        public string PeekUndoDescription()
        {
            return _undoStack.Count > 0 ? _undoStack[_undoStack.Count - 1].Description : "";
        }

        public List<CommandRecord> ExportUndo(Func<KitchenElement, int> indexOf) =>
            Export(_undoStack, indexOf);

        public List<CommandRecord> ExportRedo(Func<KitchenElement, int> indexOf) =>
            Export(_redoStack, indexOf);

        private static List<CommandRecord> Export(List<IUndoCommand> stack, Func<KitchenElement, int> indexOf)
        {
            var list = new List<CommandRecord>();
            foreach (var c in stack)
            {
                var rec = (c as ISerializableCommand)?.ToRecord(indexOf);
                if (rec != null) list.Add(rec);
            }
            return list;
        }

        public void Import(IEnumerable<CommandRecord> undo, IEnumerable<CommandRecord> redo,
            Func<int, KitchenElement> resolve)
        {
            _undoStack.Clear();
            _redoStack.Clear();
            if (undo != null)
                foreach (var r in undo)
                {
                    var c = CommandSerialization.FromRecord(r, resolve);
                    if (c != null) _undoStack.Add(c);
                }
            if (redo != null)
                foreach (var r in redo)
                {
                    var c = CommandSerialization.FromRecord(r, resolve);
                    if (c != null) _redoStack.Add(c);
                }
        }
    }
}
