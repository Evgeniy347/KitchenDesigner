using KitchenDesigner.Core.Keybinding;
using TMPro;
using UnityEngine;

namespace KitchenDesigner.Core.UI
{
    internal sealed class SidebarSearchHint
    {
        public const string NodeName = "SbSearchHint";
        public const string ShortcutNodeName = "SbSearchShortcut";

        private readonly TMP_Text _hint;
        private readonly TMP_Text _shortcut;

        private SidebarSearchHint(TMP_Text hint, TMP_Text shortcut)
        {
            _hint = hint;
            _shortcut = shortcut;
        }

        public string ShortcutText => _shortcut.text;

        public static SidebarSearchHint Create(Transform field)
        {
            var hint = CreateLabel(field, NodeName, Loc.T("sidebar.searchHint"), TextAnchor.MiddleLeft);
            hint.overflowMode = TextOverflowModes.Ellipsis;
            hint.rectTransform.offsetMin = new Vector2(UIStyle.Space2, 0f);
            hint.rectTransform.offsetMax = new Vector2(-(UIStyle.Space2 + UIStyle.Space4), 0f);

            var shortcut = CreateLabel(field, ShortcutNodeName, ShortcutLabel(), TextAnchor.MiddleRight);
            shortcut.rectTransform.offsetMin = Vector2.zero;
            shortcut.rectTransform.offsetMax = new Vector2(-UIStyle.Space2, 0f);
            return new SidebarSearchHint(hint, shortcut);
        }

        public void SetVisible(bool visible)
        {
            _hint.gameObject.SetActive(visible);
            _shortcut.gameObject.SetActive(visible);
        }

        private static string ShortcutLabel() =>
            InputBinding.Format(KitchenSettings.Instance.KeyBindings.PrimaryBinding(InputAction.CatalogOpenSearch));

        private static TMP_Text CreateLabel(Transform field, string name, string text, TextAnchor anchor)
        {
            var label = UIFactory.CreateLabel(name, field, text, UIStyle.FontSmall, Vector2.zero, Vector2.zero, anchor);
            label.color = UIStyle.TextSecondary;
            label.raycastTarget = false;
            label.enableWordWrapping = false;
            var rect = label.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            return label;
        }
    }
}
