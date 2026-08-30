using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace KitchenDesigner.Core.UI
{
    public sealed class SettingsTabStrip
    {
        public const float TabFontSize = 14f;
        public const float TabH = 32f;

        private readonly List<Button> _buttons = new();
        private readonly List<GameObject> _pages = new();

        public GameObject AddPage(Transform panel, string name)
        {
            var page = new GameObject(name);
            page.transform.SetParent(panel, false);
            _pages.Add(page);
            return page;
        }

        public void BuildButtons(Transform parent, IReadOnlyList<string> labels, float stripW, float y)
        {
            float tabW = stripW / labels.Count;

            for (int i = 0; i < labels.Count; i++)
            {
                int idx = i;
                float posX = -stripW * 0.5f + tabW * i + tabW * 0.5f;
                var btn = UIFactory.CreateButton($"Tab_{idx}", parent, labels[idx],
                    new Vector2(posX, y), new Vector2(tabW - 4, TabH),
                    () => Switch(idx));
                btn.GetComponent<Image>().color = UIStyle.SurfaceInactive;
                var caption = btn.GetComponentInChildren<TMPro.TextMeshProUGUI>();
                if (caption != null) caption.fontSize = TabFontSize;
                _buttons.Add(btn);
            }
        }

        public void Switch(int index)
        {
            for (int i = 0; i < _buttons.Count; i++)
                _buttons[i].GetComponent<Image>().color =
                    i == index ? UIStyle.SurfaceActive : UIStyle.SurfaceInactive;
            for (int i = 0; i < _pages.Count; i++)
                _pages[i].SetActive(i == index);
        }
    }
}
