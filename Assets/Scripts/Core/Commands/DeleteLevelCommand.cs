using System.Collections.Generic;

namespace KitchenDesigner.Core
{
    public sealed class DeleteLevelCommand : IUndoCommand
    {
        private readonly Level _removed;
        private readonly int _index;
        private readonly List<DeleteCommand> _elementDeletes;

        public string Description => _elementDeletes.Count == 0
            ? $"Удалить уровень «{_removed.name}»"
            : $"Удалить уровень «{_removed.name}» ({_elementDeletes.Count} дет.)";

        public DeleteLevelCommand(string levelId)
        {
            _index = LevelRegistry.IndexOf(levelId);
            _removed = _index >= 0
                ? LevelRegistry.Items[_index]
                : throw new System.ArgumentException($"уровень {levelId} не найден в LevelRegistry", nameof(levelId));

            _elementDeletes = new List<DeleteCommand>();
            var levels = LevelRegistry.Snapshot();
            foreach (var el in PartRegistry.All)
            {
                if (el == null) continue;
                if (LevelResolution.ResolveElementLevel(el.LevelId, levels).id == _removed.id)
                    _elementDeletes.Add(new DeleteCommand(el.gameObject));
            }
        }

        public void Execute()
        {
            foreach (var delete in _elementDeletes) delete.Execute();
            LevelRegistry.Remove(_removed.id);
        }

        public void Undo()
        {
            LevelRegistry.Insert(_index, _removed);
            for (int i = _elementDeletes.Count - 1; i >= 0; i--) _elementDeletes[i].Undo();
        }
    }
}
