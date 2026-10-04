using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace KitchenDesigner.Core.UI
{
    internal sealed class SceneMoveToDropdown
    {
        public const string Node = "HierMoveTo";
        public const int PlaceholderIndex = 0;
        public const int UngroupedIndex = 1;
        public const int FirstGroupIndex = 2;

        private readonly List<LinkGroup> _groups = new List<LinkGroup>();
        private readonly Action _afterMoveOfNothing;
        private bool _ignoreCallback;

        private SceneMoveToDropdown(TMP_Dropdown dropdown, Action afterMoveOfNothing)
        {
            Dropdown = dropdown;
            _afterMoveOfNothing = afterMoveOfNothing;
        }

        public TMP_Dropdown Dropdown { get; }

        public static SceneMoveToDropdown Create(Transform parent, Vector2 size, Action afterMoveOfNothing)
        {
            SceneMoveToDropdown? self = null;
            var dropdown = UIFactory.CreateDropdown(Node, parent, new List<string> { Loc.T("hierarchy.moveTo") },
                Vector2.zero, size, index => self!.OnChanged(index));
            self = new SceneMoveToDropdown(dropdown, afterMoveOfNothing);
            return self;
        }

        public void Rebuild()
        {
            _groups.Clear();
            var options = new List<string> { Loc.T("hierarchy.moveTo"), Loc.T("hierarchy.ungrouped") };
            foreach (var g in GroupManager.AllGroups())
            {
                _groups.Add(g);
                options.Add(g.name);
            }
            options.Add(Loc.T("hierarchy.newGroup"));

            _ignoreCallback = true;
            Dropdown.ClearOptions();
            Dropdown.AddOptions(options);
            Dropdown.SetValueWithoutNotify(PlaceholderIndex);
            _ignoreCallback = false;
            UIFactory.FitDropdownItems(Dropdown);
        }

        public void SetEnabled(bool enabled) => Dropdown.interactable = enabled;

        private void ResetToPlaceholder()
        {
            _ignoreCallback = true;
            Dropdown.SetValueWithoutNotify(PlaceholderIndex);
            _ignoreCallback = false;
        }

        private void OnChanged(int index)
        {
            if (_ignoreCallback || index == PlaceholderIndex) return;

            var sel = SelectionManager.Instance;
            var selected = sel != null ? new List<KitchenElement>(sel.SelectedElements) : new List<KitchenElement>();

            LinkGroup? target = null;
            int newGroupIndex = _groups.Count + FirstGroupIndex;
            if (index == newGroupIndex)
                target = GroupManager.Create(Loc.F("group.defaultName", _groups.Count + 1));
            else if (index >= FirstGroupIndex)
                target = _groups[index - FirstGroupIndex];

            foreach (var e in selected)
                if (e != null) GroupManager.MoveTo(e, target);

            ResetToPlaceholder();
            if (selected.Count == 0) _afterMoveOfNothing();
        }
    }
}
