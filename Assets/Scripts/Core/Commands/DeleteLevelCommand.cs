namespace KitchenDesigner.Core
{
    public sealed class DeleteLevelCommand : IUndoCommand
    {
        private readonly Level _removed;
        private readonly int _index;

        public string Description => $"Удалить уровень «{_removed.name}»";

        public DeleteLevelCommand(string levelId)
        {
            _index = LevelRegistry.IndexOf(levelId);
            _removed = _index >= 0
                ? LevelRegistry.Items[_index]
                : throw new System.ArgumentException($"уровень {levelId} не найден в LevelRegistry", nameof(levelId));
        }

        public void Execute() => LevelRegistry.Remove(_removed.id);

        public void Undo() => LevelRegistry.Insert(_index, _removed);
    }
}
