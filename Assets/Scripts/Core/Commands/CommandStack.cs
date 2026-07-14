using System;
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
        internal static ICommandStack Instance
        {
            get
            {
                if (GameContext.Services != null)
                    return GameContext.Services.CommandStack;
                if (_fallback == null)
                    _fallback = new CommandStackInstance();
                return _fallback;
            }
            set => _fallback = value;
        }
        private static ICommandStack _fallback = null!;

        public static bool CanUndo => Instance.CanUndo;
        public static bool CanRedo => Instance.CanRedo;
        public static int UndoCount => Instance.UndoCount;
        public static int RedoCount => Instance.RedoCount;

        public static void Execute(IUndoCommand command) => Instance.Execute(command);
        public static void Undo() => Instance.Undo();
        public static void Redo() => Instance.Redo();
        public static void Clear() => Instance.Clear();
        public static string PeekUndoDescription() => Instance.PeekUndoDescription();

        public static List<CommandRecord> ExportUndo(Func<KitchenElement, int> indexOf) =>
            Instance.ExportUndo(indexOf);

        public static List<CommandRecord> ExportRedo(Func<KitchenElement, int> indexOf) =>
            Instance.ExportRedo(indexOf);

        public static void Import(IEnumerable<CommandRecord> undo, IEnumerable<CommandRecord> redo,
            Func<int, KitchenElement> resolve) =>
            Instance.Import(undo, redo, resolve);
    }

    public class MoveCommand : IUndoCommand, ISerializableCommand
    {
        private KitchenElement _element = null!;
        private Vector3 _before;
        private Vector3 _after;
        private Quaternion _rotBefore;
        private Quaternion _rotAfter;

        public string Description => $"Move {_element?.PartName}";

        public CommandRecord? ToRecord(Func<KitchenElement, int> indexOf)
        {
            int idx = _element != null ? indexOf(_element) : -1;
            if (idx < 0) return null;
            return new CommandRecord
            {
                type = "move",
                description = Description,
                elementIndex = idx,
                posBefore = CommandRecord.V3(_before),
                posAfter = CommandRecord.V3(_after),
                rotBefore = CommandRecord.V4(_rotBefore),
                rotAfter = CommandRecord.V4(_rotAfter)
            };
        }

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
        private KitchenElement? _element;

        public string Description => $"Create {_element?.PartName}";

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
                PartRegistry.Register(_element);
        }

        public void Undo()
        {
            if (_created == null) return;
            _created.SetActive(false);
            if (_element != null)
                PartRegistry.Unregister(_element);
        }
    }

    public class DeleteCommand : IUndoCommand
    {
        private GameObject _deleted;
        private KitchenElement? _element;
        private int _siblingIndex;
        private Transform? _parent;

        public string Description => $"Delete {_element?.PartName}";

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
                PartRegistry.Unregister(_element);
        }

        public void Undo()
        {
            if (_deleted == null) return;
            _deleted.SetActive(true);
            if (_element != null)
                PartRegistry.Register(_element);
            _deleted.transform.SetSiblingIndex(_siblingIndex);
        }
    }

    public class ResizeCommand : IUndoCommand, ISerializableCommand
    {
        private KitchenElement _element = null!;
        private Vector3Int _dimsBefore;
        private Vector3Int _dimsAfter;
        private Vector3 _posBefore;
        private Vector3 _posAfter;
        private Quaternion _rotBefore;
        private Quaternion _rotAfter;

        public string Description => $"Resize {_element?.PartName}";

        public CommandRecord? ToRecord(Func<KitchenElement, int> indexOf)
        {
            int idx = _element != null ? indexOf(_element) : -1;
            if (idx < 0) return null;
            return new CommandRecord
            {
                type = "resize",
                description = Description,
                elementIndex = idx,
                dimsBefore = CommandRecord.VI(_dimsBefore),
                dimsAfter = CommandRecord.VI(_dimsAfter),
                posBefore = CommandRecord.V3(_posBefore),
                posAfter = CommandRecord.V3(_posAfter),
                rotBefore = CommandRecord.V4(_rotBefore),
                rotAfter = CommandRecord.V4(_rotAfter)
            };
        }

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

    public class CompositeCommand : IUndoCommand, ISerializableCommand
    {
        private readonly List<IUndoCommand> _commands;
        public string Description { get; }

        public CompositeCommand(string description, List<IUndoCommand> commands)
        {
            Description = description;
            _commands = commands;
        }

        public CommandRecord? ToRecord(Func<KitchenElement, int> indexOf)
        {
            // Плоская сериализация: рекурсивно собираем ВСЕ листовые команды
            // поддерева в один уровень children. Глубина JSON становится
            // константой (~6) при любой вложенности composite — иначе JsonUtility
            // на WebGL/IL2CPP жёстко падает при глубине сериализации >10.
            // Семантика undo не теряется: composite и так отменяет все свои
            // листовые команды атомарно, вложенность на это не влияет.
            var kids = new List<CommandRecord>();
            CollectLeafRecords(_commands, indexOf, kids);
            if (kids.Count == 0) return null;
            return new CommandRecord
            {
                type = "composite",
                description = Description,
                children = kids.ToArray()
            };
        }

        private static void CollectLeafRecords(List<IUndoCommand> commands,
            Func<KitchenElement, int> indexOf, List<CommandRecord> outList)
        {
            foreach (var c in commands)
            {
                if (c is CompositeCommand nested)
                {
                    CollectLeafRecords(nested._commands, indexOf, outList);
                    continue;
                }
                var rec = (c as ISerializableCommand)?.ToRecord(indexOf);
                if (rec != null) outList.Add(rec);
            }
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
