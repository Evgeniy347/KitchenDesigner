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
        internal const float BannerWidth = 420f;
        internal const float DoneButtonW = 80f;

        private GameObject? _root;
        private TMP_Text? _label;

        private void Awake() => Instance = this;

        public void Build(Transform canvas)
        {
            var panel = UIFactory.CreatePanel("ModuleEditBanner", canvas, Vector2.zero,
                new Vector2(BannerWidth, BannerHeight), UIStyle.NavBg);
            RoundedRectSprites.Apply(panel, RoundedRectSprites.ControlFill);
            UIFactory.AddFieldStroke(panel.rectTransform).color = UIStyle.Divider;
            var rt = panel.rectTransform;
            rt.anchorMin = new Vector2(0.5f, 1f);
            rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0, TuckedUnderTheTopToolbarY);
            _root = panel.gameObject;

            float buttonSlot = DoneButtonW + UIStyle.Space2 + UIStyle.Space3;
            _label = UIFactory.CreateLabel("MebLabel", panel.transform, "", UIStyle.FontSmall,
                Vector2.zero, new Vector2(BannerWidth - buttonSlot - UIStyle.Space3, BannerHeight),
                TextAnchor.MiddleLeft);
            _label.enableWordWrapping = false;
            _label.overflowMode = TextOverflowModes.Ellipsis;
            var lrt = _label.rectTransform;
            lrt.anchorMin = lrt.anchorMax = lrt.pivot = new Vector2(0f, 0.5f);
            lrt.anchoredPosition = new Vector2(UIStyle.Space3, 0f);

            var done = UIFactory.CreateButton("MebDone", panel.transform, Loc.T("group.editBanner.done"),
                Vector2.zero, new Vector2(DoneButtonW, UIStyle.ControlHCompact), Done);
            var drt = (RectTransform)done.transform;
            drt.anchorMin = drt.anchorMax = drt.pivot = new Vector2(1f, 0.5f);
            drt.anchoredPosition = new Vector2(-UIStyle.Space3, 0f);

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
                _label!.text = Loc.F("group.editBanner.label", m.name);
        }

        private void Done()
        {
            ModuleEditMode.Exit();
            if (SelectionManager.Instance != null)
                SelectionManager.Instance.DeselectAll();
        }
    }
}
