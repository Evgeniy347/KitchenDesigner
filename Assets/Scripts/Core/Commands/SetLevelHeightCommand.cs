namespace KitchenDesigner.Core
{
    public sealed class SetLevelHeightCommand : IUndoCommand
    {
        private readonly Level _level;
        private readonly int _before;
        private readonly int _after;

        public string Description => $"Высота уровня «{_level.name}»";

        public SetLevelHeightCommand(Level level, int newHeightMm)
        {
            _level = level;
            _before = level.heightMm;
            _after = newHeightMm;
        }

        public void Execute()
        {
            _level.heightMm = _after;
            LevelRegistry.Touch();
        }

        public void Undo()
        {
            _level.heightMm = _before;
            LevelRegistry.Touch();
        }
    }
}
