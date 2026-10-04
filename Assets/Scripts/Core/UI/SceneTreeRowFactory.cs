using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KitchenDesigner.Core.UI
{
    internal readonly struct SceneTreeRowActions
    {
        public SceneTreeRowActions(Action<SceneTree.Node> click, Action<SceneTree.Node> fold, Action<LinkGroup> menu)
        {
            Click = click;
            Fold = fold;
            Menu = menu;
        }

        public Action<SceneTree.Node> Click { get; }

        public Action<SceneTree.Node> Fold { get; }

        public Action<LinkGroup> Menu { get; }
    }

    internal static class SceneTreeRowFactory
    {
        public const string RowNode = "Row";
        public const string MainNode = "Main";
        public const string FoldNode = "Fold";
        public const string IconNode = "Icon";
        public const string CountNode = "Count";
        public const string MenuNode = "GroupMenu";

        public static SceneTreeRowView Build(RectTransform content, SceneTree.Node node, float y, bool selected,
            SceneTreeRowActions actions)
        {
            var kind = KindOf(node);
            var slots = TreeRowLayout.For(kind, node.depth, SceneTreeMetrics.Layout);
            bool rtl = LayoutDirection.IsRtl;

            var row = NewRow(content, y);
            var background = row.gameObject.AddComponent<Image>();
            background.color = UIStyle.Transparent;
            var bar = SelectionBar(row);

            Main(row, node, kind, slots, actions.Click);
            if (slots.HasIcon) Icon(row, kind, slots.IconX, rtl);
            if (slots.HasChevron && node.hasChildren) Fold(row, node, slots.ChevronX, rtl, actions.Fold);
            if (kind != TreeRowKind.Element) Count(row, node, kind, rtl);
            var menu = node.group != null ? Menu(row, node.group, actions.Menu, rtl) : null;

            var view = row.gameObject.AddComponent<SceneTreeRowView>();
            view.Init(node, background, bar, menu != null ? menu.gameObject : null);
            view.Paint(selected);
            return view;
        }

        private static TreeRowKind KindOf(SceneTree.Node node) =>
            node.isRoot ? TreeRowKind.Level : node.group != null ? TreeRowKind.Group : TreeRowKind.Element;

        private static RectTransform NewRow(RectTransform content, float y)
        {
            var row = UIFactory.CreateRect(RowNode, content);
            row.anchorMin = new Vector2(0f, 1f);
            row.anchorMax = new Vector2(1f, 1f);
            row.pivot = new Vector2(0.5f, 1f);
            row.anchoredPosition = new Vector2(0f, y);
            row.sizeDelta = new Vector2(0f, UIStyle.TreeRowH);
            return row;
        }

        private static GameObject SelectionBar(RectTransform row)
        {
            var bar = UIFactory.CreateRect(SceneTreeRowView.BarNode, row);
            LayoutDirection.PinToStartEdge(bar, UIStyle.SelectionBarW);
            var image = bar.gameObject.AddComponent<Image>();
            image.color = UIStyle.SelectionBar;
            image.raycastTarget = false;
            bar.gameObject.SetActive(false);
            return bar.gameObject;
        }

        private static Button Main(RectTransform row, SceneTree.Node node, TreeRowKind kind, TreeRowSlots slots,
            Action<SceneTree.Node> onClick)
        {
            bool rtl = LayoutDirection.IsRtl;
            var main = UIFactory.CreateButton(MainNode, row, "", Vector2.zero, new Vector2(0f, UIStyle.TreeRowH),
                () => onClick(node));
            var rect = (RectTransform)main.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            main.transition = Selectable.Transition.None;
            var tint = main.GetComponent<Image>();
            tint.sprite = null;
            tint.color = UIStyle.Transparent;

            var text = main.GetComponentInChildren<TMP_Text>();
            text.text = LabelOf(node);
            text.fontSize = kind == TreeRowKind.Level ? UIStyle.FontSection : UIStyle.FontBody;
            text.fontStyle = kind == TreeRowKind.Level ? FontStyles.Bold : FontStyles.Normal;
            text.color = kind == TreeRowKind.Level ? UIStyle.TextSecondary : UIStyle.Text;
            text.enableWordWrapping = false;
            text.overflowMode = TextOverflowModes.Ellipsis;
            text.raycastTarget = false;
            text.alignment = rtl ? TextAlignmentOptions.MidlineRight : TextAlignmentOptions.MidlineLeft;
            float reserve = SceneTreeMetrics.RowReserve(kind);
            text.margin = rtl
                ? new Vector4(reserve, 0f, slots.LabelX, 0f)
                : new Vector4(slots.LabelX, 0f, reserve, 0f);
            return main;
        }

        private static string LabelOf(SceneTree.Node node)
        {
            if (node.isRoot) return node.rootLabel ?? Loc.T("hierarchy.rootKitchen");
            return node.group != null ? node.group.name : node.element!.PartName;
        }

        private static void Icon(RectTransform row, TreeRowKind kind, float x, bool rtl)
        {
            var rect = UIFactory.CreateRect(IconNode, row);
            AnchorToStart(rect, rtl);
            rect.sizeDelta = new Vector2(UIStyle.TreeIconSize, UIStyle.TreeIconSize);
            rect.anchoredPosition = new Vector2(rtl ? -x : x, 0f);
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = kind == TreeRowKind.Group ? SceneTreeIcons.Group : SceneTreeIcons.Element;
            image.color = UIStyle.TextSecondary;
            image.raycastTarget = false;
        }

        private static void Fold(RectTransform row, SceneTree.Node node, float chevronX, bool rtl,
            Action<SceneTree.Node> onFold)
        {
            var fold = UIFactory.CreateButton(FoldNode, row,
                node.collapsed ? LayoutDirection.CollapsedGlyph : UIStyle.GlyphExpanded, Vector2.zero,
                new Vector2(SceneTreeMetrics.FoldHitW, UIStyle.TreeRowH), () => onFold(node));
            var rect = (RectTransform)fold.transform;
            AnchorToStart(rect, rtl);
            rect.pivot = new Vector2(rtl ? 1f : 0f, 0.5f);
            float x = chevronX - UIStyle.Space1;
            rect.anchoredPosition = new Vector2(rtl ? -x : x, 0f);
            fold.transition = Selectable.Transition.None;
            var image = fold.GetComponent<Image>();
            image.sprite = null;
            image.color = UIStyle.Transparent;
            var glyph = fold.GetComponentInChildren<TMP_Text>();
            glyph.fontSize = UIStyle.FontCaption;
            glyph.color = UIStyle.TextSecondary;
            glyph.raycastTarget = false;
        }

        private static void Count(RectTransform row, SceneTree.Node node, TreeRowKind kind, bool rtl)
        {
            int count = node.group != null ? GroupManager.MembersOf(node.group).Count : node.count;
            var label = UIFactory.CreateLabel(CountNode, row, NumberFormat.Integer(count), UIStyle.FontCaption,
                Vector2.zero, new Vector2(SceneTreeMetrics.CountW, UIStyle.TreeRowH),
                rtl ? TextAnchor.MiddleLeft : TextAnchor.MiddleRight);
            label.color = UIStyle.TextDisabled;
            label.raycastTarget = false;
            var rect = label.rectTransform;
            AnchorToEnd(rect, rtl);
            float inset = SceneTreeMetrics.RowEndPad + (kind == TreeRowKind.Group
                ? SceneTreeMetrics.MenuSlotW + UIStyle.Space1 : 0f);
            rect.anchoredPosition = new Vector2(rtl ? inset : -inset, 0f);
        }

        private static Button Menu(RectTransform row, LinkGroup group, Action<LinkGroup> onMenu, bool rtl)
        {
            float size = SceneTreeMetrics.MenuSlotW;
            var button = UIFactory.CreateIconButton(MenuNode, row, SceneTreeIcons.Dots, Vector2.zero,
                new Vector2(size, size), () => onMenu(group), size - UIStyle.IconSizeSmall);
            QuietButton.Apply(button);
            var rect = (RectTransform)button.transform;
            AnchorToEnd(rect, rtl);
            rect.anchoredPosition = new Vector2(rtl ? SceneTreeMetrics.RowEndPad : -SceneTreeMetrics.RowEndPad, 0f);
            TooltipUI.Attach(button.gameObject, Loc.T("hierarchy.groupMenu"));
            button.gameObject.SetActive(false);
            return button;
        }

        private static void AnchorToStart(RectTransform rect, bool rtl)
        {
            float x = rtl ? 1f : 0f;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(x, 0.5f);
        }

        private static void AnchorToEnd(RectTransform rect, bool rtl) => AnchorToStart(rect, !rtl);
    }
}
