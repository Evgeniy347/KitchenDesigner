using System;
using System.Collections.Generic;

namespace KitchenDesigner.Core
{
    public interface ICommandStack
    {
        bool CanUndo { get; }
        bool CanRedo { get; }
        int UndoCount { get; }
        int RedoCount { get; }
        void Execute(IUndoCommand command);
        void Undo();
        void Redo();
        void Clear();
        string PeekUndoDescription();
        List<CommandRecord> ExportUndo(Func<KitchenElement, int> indexOf);
        List<CommandRecord> ExportRedo(Func<KitchenElement, int> indexOf);
        void Import(IEnumerable<CommandRecord> undo, IEnumerable<CommandRecord> redo,
            Func<int, KitchenElement> resolve);
    }
}
