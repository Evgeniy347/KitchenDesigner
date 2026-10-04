using System.Collections.Generic;

namespace KitchenDesigner.Core.UI
{
    internal sealed class InspectorSection
    {
        private readonly List<FormRow> _members = new();

        internal InspectorSection(string id, CollapsibleSection view, FormRow header, bool expandedByDefault)
        {
            Id = id;
            View = view;
            Header = header;
            ExpandedByDefault = expandedByDefault;
        }

        public string Id { get; }

        public CollapsibleSection View { get; }

        public FormRow Header { get; }

        public bool ExpandedByDefault { get; }

        public IReadOnlyList<FormRow> Members => _members;

        public bool HasVisibleMember()
        {
            foreach (var row in _members)
                if (row.VisibleWhen == null || row.VisibleWhen()) return true;
            return false;
        }

        public void SetCount(string? text)
        {
            if ((View.Count?.text ?? "") == (text ?? "")) return;
            View.SetCount(text);
        }

        public void SetTitle(string title)
        {
            if (View.Title == null) return;
            View.Title.text = title;
            View.SetCount(View.Count?.text);
        }

        internal void Adopt(IEnumerable<FormRow> rows) => _members.AddRange(rows);
    }
}
