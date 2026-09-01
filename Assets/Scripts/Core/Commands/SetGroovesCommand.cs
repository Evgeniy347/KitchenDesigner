using System.Collections.Generic;

namespace KitchenDesigner.Core
{
    public class SetGroovesCommand : IUndoCommand
    {
        private readonly KitchenElement? _element;
        private readonly List<GrooveSpec> _before;
        private readonly List<GrooveSpec> _after;

        public string Description => $"Grooves {_element?.PartName}";

        public SetGroovesCommand(KitchenElement element,
            IEnumerable<GrooveSpec> before, IEnumerable<GrooveSpec> after)
        {
            _element = element;
            _before = new List<GrooveSpec>(before);
            _after = new List<GrooveSpec>(after);
        }

        public void Execute()
        {
            if (_element != null) _element.SetGrooves(_after);
        }

        public void Undo()
        {
            if (_element != null) _element.SetGrooves(_before);
        }
    }
}
