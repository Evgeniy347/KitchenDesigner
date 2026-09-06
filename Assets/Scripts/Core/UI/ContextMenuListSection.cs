using System.Collections.Generic;

namespace KitchenDesigner.Core.UI
{
    internal abstract class ContextMenuListSection<T>
    {
        protected readonly IContextMenuHost Host;
        protected bool Expanded;
        private int _fingerprint;

        protected ContextMenuListSection(IContextMenuHost host) => Host = host;

        public abstract bool Eligible();

        protected abstract IReadOnlyList<T>? CurrentItems();

        protected abstract void RefreshRows();

        public virtual int Count() => CurrentItems()?.Count ?? 0;

        public bool ChangedOutsideTheMenu() =>
            Eligible() && Fingerprint(CurrentItems()) != _fingerprint;

        public virtual void Collapse() => Expanded = false;

        public void Toggle()
        {
            Expanded = !Expanded;
            OnToggled();
            Refresh();
            Host.Relayout();
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
