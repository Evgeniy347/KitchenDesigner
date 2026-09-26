using System.Collections.Generic;

namespace KitchenDesigner.Core
{
    public sealed class CreateLevelCommand : IUndoCommand
    {
        private readonly Level _created;

        public string Description => $"Добавить уровень «{_created.name}»";

        public CreateLevelCommand(Level created)
        {
            _created = created;
        }

        public static CreateLevelCommand AboveTop()
        {
            var current = LevelRegistry.Snapshot();
            var next = LevelPlacement.NextAbove(current, KitchenSettings.Instance.ConstructionFloorHeightMm);
            return new CreateLevelCommand(next);
        }

        public void Execute() => LevelRegistry.Add(_created);

        public void Undo() => LevelRegistry.Remove(_created.id);
    }
}
