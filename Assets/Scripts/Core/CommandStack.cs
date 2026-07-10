using System.Collections.Generic;
using UnityEngine;

namespace KitchenDesigner.Core
{
    public interface IUndoCommand
    {
        void Execute();
        void Undo();
        string Description { get; }
    }

    public static class CommandStack
    {
        private static readonly List<IUndoCommand> _undoStack = new List<IUndoCommand>();
        private static readonly List<IUndoCommand> _redoStack = new List<IUndoCommand>();
        private const int MaxUndo = 20;

        public static bool CanUndo => _undoStack.Count > 0;
        public static bool CanRedo => _redoStack.Count > 0;

        public static void Execute(IUndoCommand command)
        {
            command.Execute();
            _undoStack.Add(command);
            if (_undoStack.Count > MaxUndo)
                _undoStack.RemoveAt(0);
            _redoStack.Clear();
        }

        public static void Undo()
        {
            if (_undoStack.Count == 0) return;
            int idx = _undoStack.Count - 1;
            var cmd = _undoStack[idx];
            _undoStack.RemoveAt(idx);
            cmd.Undo();
            _redoStack.Add(cmd);
        }

        public static void Redo()
        {
            if (_redoStack.Count == 0) return;
            int idx = _redoStack.Count - 1;
            var cmd = _redoStack[idx];
            _redoStack.RemoveAt(idx);
            cmd.Execute();
            _undoStack.Add(cmd);
        }

        public static void Clear()
        {
            _undoStack.Clear();
            _redoStack.Clear();
        }

        public static string PeekUndoDescription()
        {
            return _undoStack.Count > 0 ? _undoStack[_undoStack.Count - 1].Description : "";
        }
    }

    public class MoveCommand : IUndoCommand
    {
        private KitchenElement _element;
        private Vector3 _before;
        private Vector3 _after;
        private Quaternion _rotBefore;
        private Quaternion _rotAfter;

        public string Description => $"Move {_element?.BoardName}";

        public MoveCommand(KitchenElement element, Vector3 before, Vector3 after,
            Quaternion rotBefore, Quaternion rotAfter)
        {
            _element = element;
            _before = before;
            _after = after;
            _rotBefore = rotBefore;
            _rotAfter = rotAfter;
        }

        public void Execute()
        {
            if (_element == null) return;
            _element.transform.position = _after;
            _element.transform.rotation = _rotAfter;
        }

        public void Undo()
        {
            if (_element == null) return;
            _element.transform.position = _before;
            _element.transform.rotation = _rotBefore;
        }
    }

    public class CreateCommand : IUndoCommand
    {
        private GameObject _created;
        private KitchenElement _element;

        public string Description => $"Create {_element?.BoardName}";

        public CreateCommand(GameObject created)
        {
            _created = created;
            _element = created?.GetComponent<KitchenElement>();
        }

        public void Execute()
        {
            if (_created == null) return;
            _created.SetActive(true);
            if (_element != null)
                BoardRegistry.Register(_element);
        }

        public void Undo()
        {
            if (_created == null) return;
            _created.SetActive(false);
            if (_element != null)
                BoardRegistry.Unregister(_element);
        }
    }

    public class DeleteCommand : IUndoCommand
    {
        private GameObject _deleted;
        private KitchenElement _element;
        private int _siblingIndex;
        private Transform _parent;

        public string Description => $"Delete {_element?.BoardName}";

        public DeleteCommand(GameObject deleted)
        {
            _deleted = deleted;
            _element = deleted?.GetComponent<KitchenElement>();
            _parent = deleted?.transform.parent;
            _siblingIndex = deleted != null ? deleted.transform.GetSiblingIndex() : 0;
        }

        public void Execute()
        {
            if (_deleted == null) return;
            _deleted.SetActive(false);
            if (_element != null)
                BoardRegistry.Unregister(_element);
        }

        public void Undo()
        {
            if (_deleted == null) return;
            _deleted.SetActive(true);
            if (_element != null)
                BoardRegistry.Register(_element);
            _deleted.transform.SetSiblingIndex(_siblingIndex);
        }
    }

    public class ResizeCommand : IUndoCommand
    {
        private KitchenElement _element;
        private Vector3Int _dimsBefore;
        private Vector3Int _dimsAfter;
        private Vector3 _posBefore;
        private Vector3 _posAfter;
        private Quaternion _rotBefore;
        private Quaternion _rotAfter;

        public string Description => $"Resize {_element?.BoardName}";

        public ResizeCommand(KitchenElement element,
            Vector3Int dimsBefore, Vector3Int dimsAfter,
            Vector3 posBefore, Vector3 posAfter,
            Quaternion rotBefore, Quaternion rotAfter)
        {
            _element = element;
            _dimsBefore = dimsBefore;
            _dimsAfter = dimsAfter;
            _posBefore = posBefore;
            _posAfter = posAfter;
            _rotBefore = rotBefore;
            _rotAfter = rotAfter;
        }

        public void Execute()
        {
            if (_element == null) return;
            _element.DimensionsMM = _dimsAfter;
            _element.transform.position = _posAfter;
            _element.transform.rotation = _rotAfter;
        }

        public void Undo()
        {
            if (_element == null) return;
            _element.DimensionsMM = _dimsBefore;
            _element.transform.position = _posBefore;
            _element.transform.rotation = _rotBefore;
        }
    }

    /// <summary>Несколько команд как одна операция отмены (например, перемещение группы).</summary>
    public class CompositeCommand : IUndoCommand
    {
        private readonly List<IUndoCommand> _commands;
        public string Description { get; }

        public CompositeCommand(string description, List<IUndoCommand> commands)
        {
            Description = description;
            _commands = commands;
        }

        public void Execute()
        {
            for (int i = 0; i < _commands.Count; i++) _commands[i].Execute();
        }

        public void Undo()
        {
            for (int i = _commands.Count - 1; i >= 0; i--) _commands[i].Undo();
        }
    }
}
