using System.Collections.Generic;

namespace KitchenDesigner.Core
{
    public class SetSwitchLightsCommand : IUndoCommand
    {
        private readonly LightSwitchElement? _source;
        private readonly List<string> _before;
        private readonly List<string> _after;

        public string Description => $"Switch lights {_source?.PartName}";

        public SetSwitchLightsCommand(LightSwitchElement source,
            IEnumerable<string> before, IEnumerable<string> after)
        {
            _source = source;
            _before = new List<string>(before);
            _after = new List<string>(after);
        }

        public void Execute()
        {
            if (_source != null) _source.SetLightNames(_after);
        }

        public void Undo()
        {
            if (_source != null) _source.SetLightNames(_before);
        }
    }
}
