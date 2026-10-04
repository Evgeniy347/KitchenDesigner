using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KitchenDesigner.Core.UI
{
    public sealed class SettingsNavEntry
    {
        public SettingsNavEntry(string id, string title, string? group)
        {
            Id = id;
            Title = title;
            Group = group;
        }

        public string Id { get; }

        public string Title { get; }

        public string? Group { get; }
    }

    public sealed class SettingsNav
    {
        public const string RootName = "SettingsNav";
        public const string SearchNode = "NavSearch";
        public const string SearchHintNode = "NavSearchHint";
        public const string ItemPrefix = "NavItem_";
        public const string GroupPrefix = "NavGroup_";
        public const string BarNode = "Bar";

        private sealed class Item
        {
            public Item(SettingsNavEntry entry, Button button, Image background, RectTransform bar)
            {
                Entry = entry;
                Button = button;
                Background = background;
                Bar = bar;
            }

            public SettingsNavEntry Entry { get; }

            public Button Button { get; }

            public Image Background { get; }

            public RectTransform Bar { get; }

            public bool Shown { get; set; } = true;
        }

        private readonly List<Item> _items = new();
        private readonly Dictionary<string, TextMeshProUGUI> _groups = new();
        private TextMeshProUGUI? _searchHint;

        public RectTransform Root { get; private set; } = null!;

        public TMP_InputField Search { get; private set; } = null!;

        public int Count => _items.Count;

        public string Query => Search.text;

        public IEnumerable<Button> Buttons
        {
            get
            {
                foreach (var item in _items) yield return item.Button;
            }
        }

        public bool IsShown(int index) => _items[index].Shown;

        public static float ItemInnerWidth => UIStyle.NavW - 2f * UIStyle.Space3;

        public static SettingsNav Create(RectTransform panel, IReadOnlyList<SettingsNavEntry> entries,
            Action<int> onSelect, Action<string> onQuery)
        {
            var nav = new SettingsNav();
            nav.Build(panel, entries, onSelect, onQuery);
            return nav;
        }

        public void SetCurrent(int index)
        {
            for (int i = 0; i < _items.Count; i++) Paint(_items[i], i == index);
        }

        public void ClearSearch()
        {
            if (Search.text.Length > 0) Search.text = "";
        }

        public void Layout(IReadOnlyList<bool> shown)
        {
            for (int i = 0; i < _items.Count; i++) _items[i].Shown = shown[i];
            LayOut();
        }

        private void Build(RectTransform panel, IReadOnlyList<SettingsNavEntry> entries,
            Action<int> onSelect, Action<string> onQuery)
        {
            bool rtl = LayoutDirection.IsRtl;
            Root = UIFactory.CreateRect(RootName, panel);
            Root.anchorMin = new Vector2(rtl ? 1f : 0f, 0f);
            Root.anchorMax = new Vector2(rtl ? 1f : 0f, 1f);
            Root.pivot = new Vector2(rtl ? 1f : 0f, 1f);
            Root.sizeDelta = new Vector2(UIStyle.NavW, -(UIStyle.TitleBarH + UIStyle.FooterH));
            Root.anchoredPosition = new Vector2(0f, -UIStyle.TitleBarH);

            var background = Root.gameObject.AddComponent<Image>();
            background.color = UIStyle.NavBg;
            Edge(rtl);

            BuildSearch(onQuery);

            for (int i = 0; i < entries.Count; i++)
            {
                int index = i;
                var item = BuildItem(entries[i], () => onSelect(index));
                _items.Add(item);
                string? group = entries[i].Group;
                if (group != null && !_groups.ContainsKey(group)) _groups[group] = BuildGroup(group);
            }

            LayOut();
        }

        private void Edge(bool rtl)
        {
            var line = UIFactory.CreateRect("Edge", Root);
            line.anchorMin = new Vector2(rtl ? 0f : 1f, 0f);
            line.anchorMax = new Vector2(rtl ? 0f : 1f, 1f);
            line.pivot = new Vector2(rtl ? 0f : 1f, 0.5f);
            line.sizeDelta = new Vector2(UIStyle.DividerPx, 0f);
            line.anchoredPosition = Vector2.zero;
            var image = line.gameObject.AddComponent<Image>();
            image.color = UIStyle.Divider;
            image.raycastTarget = false;
        }

        private void BuildSearch(Action<string> onQuery)
        {
            float width = UIStyle.NavW - 2f * UIStyle.Space2;
            Search = UIFactory.CreateInputField(SearchNode, Root, "", Vector2.zero,
                new Vector2(width, UIStyle.ControlH));
            Top(Search.GetComponent<RectTransform>(), UIStyle.Space2, UIStyle.Space2);

            var hint = UIFactory.CreateLabel(SearchHintNode, Search.transform, Loc.T("settings.nav.search"),
                UIStyle.FontBody, Vector2.zero, new Vector2(width, UIStyle.ControlH), TextAnchor.MiddleLeft);
            hint.color = UIStyle.TextSecondary;
            hint.raycastTarget = false;
            hint.enableWordWrapping = false;
            hint.overflowMode = TextOverflowModes.Ellipsis;
            var rt = hint.rectTransform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(UIStyle.Space2, 0f);
            rt.offsetMax = new Vector2(-UIStyle.Space2, 0f);
            _searchHint = hint;

            Search.onValueChanged.AddListener(text =>
            {
                _searchHint.gameObject.SetActive(text.Length == 0);
                onQuery(text);
            });
        }

        private Item BuildItem(SettingsNavEntry entry, Action onClick)
        {
            var button = UIFactory.CreateButton(ItemPrefix + entry.Id, Root, entry.Title, Vector2.zero,
                new Vector2(UIStyle.NavW - UIStyle.DividerPx, UIStyle.NavItemH), onClick);
            var image = (Image)button.targetGraphic;
            var label = button.GetComponentInChildren<TMP_Text>();
            label.alignment = TextAlignmentOptions.MidlineLeft;
            label.enableWordWrapping = false;
            label.overflowMode = TextOverflowModes.Ellipsis;
            var lrt = label.rectTransform;
            lrt.offsetMin = new Vector2(UIStyle.Space3, 0f);
            lrt.offsetMax = new Vector2(-UIStyle.Space3, 0f);

            var bar = UIFactory.CreateRect(BarNode, button.transform);
            LayoutDirection.PinToStartEdge(bar, UIStyle.SelectionBarW);
            var barImage = bar.gameObject.AddComponent<Image>();
            barImage.color = UIStyle.Accent;
            barImage.raycastTarget = false;

            var item = new Item(entry, button, image, bar);
            Paint(item, false);
            return item;
        }

        private TextMeshProUGUI BuildGroup(string title)
        {
            var label = UIFactory.CreateLabel(GroupPrefix + title, Root, title, UIStyle.FontCaption, Vector2.zero,
                new Vector2(ItemInnerWidth, UIStyle.NavItemH), TextAnchor.MiddleLeft);
            label.color = UIStyle.TextDisabled;
            label.raycastTarget = false;
            label.enableWordWrapping = false;
            label.overflowMode = TextOverflowModes.Ellipsis;
            return label;
        }

        private void Paint(Item item, bool selected)
        {
            item.Background.color = selected ? UIStyle.AccentSubtle : UIStyle.SurfaceHover;
            var colors = item.Button.colors;
            colors.normalColor = selected ? UIStyle.NoTint : UIStyle.TintHidden;
            colors.selectedColor = colors.normalColor;
            colors.highlightedColor = UIStyle.NoTint;
            colors.pressedColor = UIStyle.TintPressed;
            item.Button.colors = colors;
            item.Bar.gameObject.SetActive(selected);
            item.Button.GetComponentInChildren<TMP_Text>().color = UIStyle.Text;
        }

        private void LayOut()
        {
            float y = UIStyle.Space2 + UIStyle.ControlH + UIStyle.Space2;
            string? lastGroup = null;
            foreach (var group in _groups.Values) group.gameObject.SetActive(false);

            foreach (var item in _items)
            {
                item.Button.gameObject.SetActive(item.Shown);
                if (!item.Shown) continue;

                string? group = item.Entry.Group;
                if (group != lastGroup && group != null)
                {
                    var label = _groups[group];
                    label.gameObject.SetActive(true);
                    Place(label.rectTransform, UIStyle.Space3, y);
                    y += UIStyle.NavItemH;
                }
                lastGroup = group;

                Place((RectTransform)item.Button.transform, 0f, y);
                y += UIStyle.NavItemH;
            }
        }

        private static void Top(RectTransform rect, float inset, float y) => Place(rect, inset, y);

        private static void Place(RectTransform rect, float inset, float y)
        {
            bool rtl = LayoutDirection.IsRtl;
            float x = rtl ? 1f : 0f;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(x, 1f);
            rect.anchoredPosition = new Vector2(rtl ? -inset : inset, -y);
        }
    }
}
