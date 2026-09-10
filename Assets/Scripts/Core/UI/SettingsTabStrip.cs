using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace KitchenDesigner.Core.UI
{
    public sealed class SettingsTabStrip
    {
        public const float TabFontSize = 14f;
        public const float TabH = 32f;
        public const float TabGap = 4f;
        public const float TabPaddingPx = 14f;
        public const float MinTabFontSize = 11f;

        private readonly List<Button> _buttons = new();
        private readonly List<GameObject> _pages = new();

        public Action? AfterSwitch { get; set; }

        public GameObject AddPage(Transform parent, string name, float originY)
        {
            var page = new GameObject(name);
            page.transform.SetParent(parent, false);
            page.transform.localPosition = new Vector3(0f, originY, 0f);
            _pages.Add(page);
            return page;
        }

        public void BuildButtons(Transform parent, IReadOnlyList<string> labels, float stripW, float y)
        {
            for (int i = 0; i < labels.Count; i++)
            {
                int idx = i;
                var btn = UIFactory.CreateButton($"Tab_{idx}", parent, labels[idx],
                    new Vector2(0f, y), new Vector2(stripW / labels.Count, TabH),
                    () => Switch(idx));
                btn.GetComponent<Image>().color = UIStyle.SurfaceInactive;
                var caption = btn.GetComponentInChildren<TMPro.TextMeshProUGUI>();
                if (caption != null) caption.fontSize = TabFontSize;
                _buttons.Add(btn);
            }

            LayOut(labels, stripW, y);
        }

        private void LayOut(IReadOnlyList<string> labels, float stripW, float y)
        {
            var widths = new float[labels.Count];
            float sum = 0f;
            for (int i = 0; i < labels.Count; i++)
            {
                widths[i] = CaptionWidth(_buttons[i], labels[i]) + TabPaddingPx * 2f;
                sum += widths[i];
            }
            if (sum <= 0f) return;

            float available = stripW - TabGap * (labels.Count - 1);
            float scale = available / sum;

            float x = -stripW * 0.5f;
            for (int i = 0; i < labels.Count; i++)
            {
                float w = widths[i] * scale;
                var rect = (RectTransform)_buttons[i].transform;
                rect.sizeDelta = new Vector2(w, TabH);
                rect.anchoredPosition = new Vector2(x + w * 0.5f, y);
                x += w + TabGap;

                var caption = _buttons[i].GetComponentInChildren<TMPro.TextMeshProUGUI>();
                if (caption != null) caption.fontSize = CaptionFontSize(scale);
            }
        }

        public static float CaptionFontSize(float stripScale) =>
            stripScale >= 1f ? TabFontSize : Mathf.Max(MinTabFontSize, TabFontSize * stripScale);

        private static float CaptionWidth(Button button, string label)
        {
            var caption = button.GetComponentInChildren<TMPro.TextMeshProUGUI>();
            if (caption == null || caption.font == null) return label.Length * TabFontSize * 0.5f;
            return caption.GetPreferredValues(label).x;
        }

        public void Switch(int index)
        {
            for (int i = 0; i < _buttons.Count; i++)
                _buttons[i].GetComponent<Image>().color =
                    i == index ? UIStyle.SurfaceActive : UIStyle.SurfaceInactive;
            for (int i = 0; i < _pages.Count; i++)
                _pages[i].SetActive(i == index);
            AfterSwitch?.Invoke();
        }
    }
}
