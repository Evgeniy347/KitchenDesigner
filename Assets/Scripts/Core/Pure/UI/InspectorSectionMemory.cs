using System.Collections.Generic;

namespace KitchenDesigner.Core.UI
{
    public sealed class InspectorSectionMemory
    {
        private readonly Dictionary<string, bool> _expanded = new();

        public bool IsExpanded(string elementType, string section, bool byDefault) =>
            _expanded.TryGetValue(KeyOf(elementType, section), out var expanded) ? expanded : byDefault;

        public void Remember(string elementType, string section, bool expanded) =>
            _expanded[KeyOf(elementType, section)] = expanded;

        private static string KeyOf(string elementType, string section) => elementType + "|" + section;
    }
}
