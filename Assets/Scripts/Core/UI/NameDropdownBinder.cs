using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace KitchenDesigner.Core.UI
{
    internal sealed class NameDropdownBinder
    {
        private const int NoneIndex = 0;

        private readonly TMP_Dropdown _dropdown;
        private readonly string _noneCaption;
        private readonly Func<string> _currentName;
        private readonly Func<IEnumerable<string>> _candidates;
        private readonly Func<bool> _isDetached;
        private readonly Action<string> _commit;
        private readonly Color _normalCaptionColor;

        public NameDropdownBinder(TMP_Dropdown dropdown, string noneCaption,
            Func<string> currentName, Func<IEnumerable<string>> candidates,
            Func<bool> isDetached, Action<string> commit)
        {
            _dropdown = dropdown;
            _noneCaption = noneCaption;
            _currentName = currentName;
            _candidates = candidates;
            _isDetached = isDetached;
            _commit = commit;
            _normalCaptionColor = dropdown.captionText.color;

            dropdown.onValueChanged.AddListener(Select);
            var hook = dropdown.template.gameObject.AddComponent<DropdownOpenHook>();
            hook.OnOpen = () =>
            {
                Rebuild();
                SetValue(_currentName());
            };
            hook.OnAfterShow = PaintDetachedItem;
        }

        public void Rebuild()
        {
            var options = new List<TMP_Dropdown.OptionData> { new(_noneCaption) };
            var listed = new HashSet<string>();
            foreach (var name in _candidates())
            {
                if (string.IsNullOrEmpty(name) || !listed.Add(name)) continue;
                options.Add(new TMP_Dropdown.OptionData(name));
            }

            var current = _currentName();
            if (!string.IsNullOrEmpty(current) && !listed.Contains(current))
                options.Add(new TMP_Dropdown.OptionData(current));

            _dropdown.options = options;
        }

        public int IndexOf(string name)
        {
            if (string.IsNullOrEmpty(name)) return NoneIndex;
            var options = _dropdown.options;
            for (int i = NoneIndex + 1; i < options.Count; i++)
                if (options[i].text == name) return i;
            return NoneIndex;
        }

        public void SetValue(string name)
        {
            _dropdown.SetValueWithoutNotify(IndexOf(name));
            _dropdown.RefreshShownValue();
            UpdateCaptionColor();
        }

        public void UpdateCaptionColor()
        {
            if (_dropdown.captionText == null) return;
            _dropdown.captionText.color = _isDetached() ? Color.red : _normalCaptionColor;
        }

        public void Select(int index)
        {
            var options = _dropdown.options;
            string name = index <= NoneIndex || index >= options.Count ? "" : options[index].text;
            _commit(name);
            UpdateCaptionColor();
            SceneRevision.Bump();
        }

        private void PaintDetachedItem()
        {
            if (!_isDetached()) return;
            var current = _currentName();
            if (string.IsNullOrEmpty(current)) return;

            var content = _dropdown.template.Find("Viewport/Content");
            if (content == null) return;
            foreach (Transform child in content)
            {
                var label = child.GetComponentInChildren<TMP_Text>();
                if (label != null && label.text == current)
                    label.color = Color.red;
            }
        }
    }
}
