using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KitchenDesigner.Core.UI
{
    public sealed class ScrollableDropdown : TMP_Dropdown
    {
        private GameObject? _openList;

        public IReadOnlyList<string?>? OptionLanguages { get; set; }

        protected override GameObject CreateDropdownList(GameObject template)
        {
            _openList = base.CreateDropdownList(template);
            return _openList;
        }

        protected override GameObject CreateBlocker(Canvas rootCanvas)
        {
            var blocker = base.CreateBlocker(rootCanvas);
            if (_openList != null) FinishOpenList(_openList, (RectTransform)rootCanvas.transform);
            return blocker;
        }

        private void FinishOpenList(GameObject list, RectTransform canvas)
        {
            var items = list.GetComponentsInChildren<DropdownItem>(false);
            StyleNativeNames(items);
            DropdownListPlacement.KeepInside((RectTransform)list.transform, canvas);

            var scroll = list.GetComponent<ScrollRect>();
            if (scroll == null || value < 0 || value >= items.Length) return;
            DropdownListPlacement.ScrollToItem(scroll, items[value].rectTransform);
        }

        private void StyleNativeNames(DropdownItem[] items)
        {
            var languages = OptionLanguages;
            if (languages == null) return;
            for (int i = 0; i < items.Length && i < languages.Count; i++)
                if (items[i].text != null) NativeNameLabel.Apply(items[i].text, languages[i]);
        }
    }
}
