using System.Collections.Generic;

namespace KitchenDesigner.Core
{
    public sealed class MoveToLevelCommand : IUndoCommand
    {
        private readonly List<KitchenElement> _elements;
        private readonly List<string> _before;
        private readonly string _levelId;

        public string Description => "Переместить на другой уровень";

        public MoveToLevelCommand(IEnumerable<KitchenElement> elements, string levelId)
        {
            _elements = new List<KitchenElement>(elements);
            _levelId = levelId ?? "";
            _before = new List<string>(_elements.Count);
            foreach (var e in _elements) _before.Add(e.LevelId);
        }

        public void Execute()
        {
            foreach (var e in _elements) e.LevelId = _levelId;
        }

        public void Undo()
        {
            for (int i = 0; i < _elements.Count; i++)
                _elements[i].LevelId = _before[i];
        }
    }
}
