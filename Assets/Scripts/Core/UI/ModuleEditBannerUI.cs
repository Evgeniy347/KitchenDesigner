using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KitchenDesigner.Core.UI
{
    public class ModuleEditBannerUI : MonoBehaviour
    {
        public static ModuleEditBannerUI? Instance { get; private set; }

        internal const float TuckedUnderTheTopToolbarY = -46f;
        internal const float BannerHeight = 40f;

        private GameObject? _root;
        private TMP_Text? _label;

        private void Awake() => Instance = this;

        public void Build(Transform canvas)
        {
            var panel = UIFactory.CreatePanel("ModuleEditBanner", canvas, Vector2.zero,
                new Vector2(420, BannerHeight));
            var rt = panel.rectTransform;
            rt.anchorMin = new Vector2(0.5f, 1f);
            rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0, TuckedUnderTheTopToolbarY);
            panel.color = new Color(0.15f, 0.35f, 0.6f, 0.92f);
            _root = panel.gameObject;

            _label = UIFactory.CreateLabel("MebLabel", panel.transform, "", 16,
                new Vector2(-30, 0), new Vector2(320, 32), TextAnchor.MiddleCenter);
            _label.color = Color.white;

            UIFactory.CreateButton("MebDone", panel.transform, "Готово",
                new Vector2(165, 0), new Vector2(80, 30), Done);

            _root.SetActive(false);
            ModuleEditMode.Changed += Refresh;
            Refresh();
        }

        private void OnDestroy()
        {
            ModuleEditMode.Changed -= Refresh;
        }

        private void Refresh()
        {
            if (_root == null) return;
            var m = ModuleEditMode.Active;
            _root.SetActive(m != null);
            if (m != null)
                _label!.text = $"Редактирование модуля: {m.name}  (Esc — выход)";
        }

        private void Done()
        {
            ModuleEditMode.Exit();
            if (SelectionManager.Instance != null)
                SelectionManager.Instance.DeselectAll();
        }
    }
}
