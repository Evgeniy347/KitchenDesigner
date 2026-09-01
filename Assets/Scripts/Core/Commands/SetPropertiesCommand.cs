using System.Collections.Generic;

namespace KitchenDesigner.Core
{
    public sealed class SetPropertiesCommand : IUndoCommand
    {
        private readonly KitchenElement _element;
        private readonly ElementPropertyBag _before;
        private readonly ElementPropertyBag _after;
        private readonly List<int> _changed;

        public string Description => $"Set properties {_element.PartName}";

        public IEnumerable<string> ChangedNames
        {
            get { foreach (int i in _changed) yield return _after.PropertyAt(i).Name; }
        }

        private SetPropertiesCommand(KitchenElement element,
            ElementPropertyBag before, ElementPropertyBag after, List<int> changed)
        {
            _element = element;
            _before = before;
            _after = after;
            _changed = changed;
        }

        public static SetPropertiesCommand? TryCreate(KitchenElement element,
            ElementPropertyBag before, ElementPropertyBag after)
        {
            if (element == null) return null;
            var changed = UndoableProperties.Changed(before, after);
            if (changed.Count == 0) return null;
            return new SetPropertiesCommand(element, before, after, changed);
        }

        public void Execute() => UndoableProperties.Restore(_element, _after, _changed);

        public void Undo() => UndoableProperties.Restore(_element, _before, _changed);
    }
}
