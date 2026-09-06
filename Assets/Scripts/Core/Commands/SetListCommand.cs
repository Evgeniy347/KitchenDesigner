using System;
using System.Collections.Generic;

namespace KitchenDesigner.Core
{
    public sealed class SetListCommand<T> : IUndoCommand
    {
        private readonly List<T> _before;
        private readonly List<T> _after;
        private readonly Action<List<T>> _setter;

        public string Description { get; }

        public SetListCommand(string description,
            IEnumerable<T> before, IEnumerable<T> after, Action<List<T>> setter)
        {
            Description = description;
            _before = new List<T>(before);
            _after = new List<T>(after);
            _setter = setter;
        }

        public void Execute() => _setter(_after);

        public void Undo() => _setter(_before);
    }
}
