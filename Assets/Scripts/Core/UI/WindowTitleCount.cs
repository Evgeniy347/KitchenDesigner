using TMPro;
using UnityEngine;

namespace KitchenDesigner.Core.UI
{
    public sealed class WindowTitleCount
    {
        public const string Node = "TitleCount";

        private readonly WindowChrome _chrome;
        private readonly TMP_Text _label;

        private WindowTitleCount(WindowChrome chrome, TMP_Text label)
        {
            _chrome = chrome;
            _label = label;
        }

        public TMP_Text Label => _label;

        public static WindowTitleCount Create(WindowChrome chrome)
        {
            var label = UIFactory.CreateLabel(Node, chrome.Panel, "", UIStyle.FontBody, Vector2.zero,
                new Vector2(UIStyle.CloseBtnSize * 2f, UIStyle.ControlH), TextAnchor.MiddleLeft);
            label.color = UIStyle.TextSecondary;
            label.enableWordWrapping = false;
            label.raycastTarget = false;
            var rt = label.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 0.5f);
            return new WindowTitleCount(chrome, label);
        }

        public void Set(int count)
        {
            _label.text = NumberFormat.Integer(count);
            float titleW = _chrome.Title.GetPreferredValues(_chrome.Title.text).x;
            _label.rectTransform.anchoredPosition = new Vector2(
                UIStyle.WindowTitleInset + Mathf.Ceil(titleW) + UIStyle.Space2, -UIStyle.WindowTitleCenterFromTop);
        }
    }
}
