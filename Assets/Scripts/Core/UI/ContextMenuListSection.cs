using System;
using System.Collections.Generic;

namespace KitchenDesigner.Core.UI
{
    internal abstract class ContextMenuListSection<T>
    {
        protected readonly IContextMenuHost Host;
        private InspectorSection? _section;
        private int _fingerprint;

        protected ContextMenuListSection(IContextMenuHost host) => Host = host;

        protected InspectorSection Section => _section!;

        protected bool Expanded => _section != null && _section.View.Expanded;

        public abstract bool Eligible();

        protected abstract IReadOnlyList<T>? CurrentItems();

        protected abstract void RefreshRows();

        public virtual int Count() => CurrentItems()?.Count ?? 0;

        public bool ChangedOutsideTheMenu() =>
            Eligible() && Fingerprint(CurrentItems()) != _fingerprint;

        public void Toggle() => Section.View.Toggle();

        protected InspectorSection BeginSection(string id, string title, bool expandedByDefault,
            string? actionCaption = null, Action? onAction = null)
        {
            _section = Host.Rows.BeginSection(id, title, expandedByDefault, actionCaption, onAction);
            _section.View.Toggled += _ =>
            {
                OnToggled();
                Refresh();
            };
            return _section;
        }

        protected void EnsureExpanded()
        {
            if (!Expanded) Section.View.SetExpanded(true, notify: true);
        }

        protected virtual void OnToggled()
        {
        }

        public void Refresh()
        {
            ConfirmDeleteButton.DisarmAll();
            _fingerprint = Fingerprint(CurrentItems());
            RefreshRows();
        }

        protected void AfterChange()
        {
            Refresh();
            Host.Relayout();
            OnAfterChange();
        }

        protected virtual void OnAfterChange()
        {
        }

        protected static int Fingerprint(IReadOnlyList<T>? items)
        {
            if (items == null) return 0;
            unchecked
            {
                int h = 17;
                foreach (var item in items)
                    h = h * 31 + EqualityComparer<T>.Default.GetHashCode(item);
                return h;
            }
        }
    }
}
