namespace KitchenDesigner.Core
{
    public sealed class RenameLevelCommand : IUndoCommand
    {
        private readonly Level _level;
        private readonly string _before;
        private readonly string _after;

        public string Description => $"Переименовать уровень «{_before}» в «{_after}»";

        public RenameLevelCommand(Level level, string newName)
        {
            _level = level;
            _before = level.name;
            _after = newName ?? "";
        }

        public void Execute()
        {
            _level.name = _after;
            LevelRegistry.Touch();
        }

        public void Undo()
        {
            _level.name = _before;
            LevelRegistry.Touch();
        }
    }
}
