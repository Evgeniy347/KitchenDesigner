using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KitchenDesigner.Core.UI
{
    public sealed class DemoModeDialogUI : MonoBehaviour
    {
        public static DemoModeDialogUI? Instance { get; private set; }

        private const float W = 460f;
        private const float H = 230f;

        private RectTransform? _root;
        private TMP_Text? _title;
        private TMP_Text? _message;
        internal Button? SaveCopyButton;
        internal Button? CancelButton;

        internal bool IsVisible => _root != null && _root.gameObject.activeSelf;
        internal string? MessageText => _message != null ? _message.text : null;

        private void Awake()
        {
            Instance = this;
            DemoModeGuard.Prompt = ShowIfAvailable;
        }

        private void OnDestroy()
        {
            if (!ReferenceEquals(Instance, this)) return;
            Instance = null;
            DemoModeGuard.Prompt = null;
        }

        public static void ShowIfAvailable()
        {
            var dialog = Instance;
            if (dialog == null || dialog.IsVisible) return;
            dialog.Show();
        }

        public void Build(Transform parent)
        {
            var backdrop = UIFactory.CreatePanel("DemoModeDialogBackdrop", parent,
                Vector2.zero, Vector2.one, UIStyle.ModalBackdrop);
            _root = backdrop.rectTransform;
            _root.anchorMin = Vector2.zero;
            _root.anchorMax = Vector2.one;
            _root.offsetMin = _root.offsetMax = Vector2.zero;

            var panel = UIFactory.CreatePanel("DemoModeDialogPanel", _root,
                Vector2.zero, new Vector2(W, H), UIStyle.Panel);
            var pr = panel.rectTransform;
            UIFactory.AnchorCenter(pr);
            pr.anchoredPosition = Vector2.zero;

            _title = UIFactory.CreateLabel("DemoModeDialogTitle", pr, DemoModeStrings.Title,
                UIStyle.FontWindowTitle, new Vector2(0, H / 2f - 30f),
                new Vector2(W - 2f * UIStyle.WindowPad, 30f), TextAnchor.UpperLeft);
            _title.fontStyle = FontStyles.Bold;

            _message = UIFactory.CreateLabel("DemoModeDialogMessage", pr, DemoModeStrings.Message,
                UIStyle.FontBody, new Vector2(0, 24f),
                new Vector2(W - 2f * UIStyle.WindowPad, H - 120f), TextAnchor.UpperLeft);
            _message.enableWordWrapping = true;

            CancelButton = UIFactory.CreateButton("DemoModeDialogCancel", pr,
                DemoModeStrings.CancelButton, new Vector2(-W / 2f + 100f, -H / 2f + 28f),
                new Vector2(170f, 40f), Hide);

            SaveCopyButton = UIFactory.CreateButton("DemoModeDialogSaveCopy", pr,
                DemoModeStrings.SaveCopyButton, new Vector2(W / 2f - 130f, -H / 2f + 28f),
                new Vector2(230f, 40f), SaveCopy);
            var image = SaveCopyButton.GetComponent<Image>();
            if (image != null) image.color = UIStyle.Accent;

            _root.gameObject.SetActive(false);
        }

        public void Show()
        {
            if (_root == null) return;
            _root.gameObject.SetActive(true);
            _root.SetAsLastSibling();
        }

        public void Hide()
        {
            if (_root != null) _root.gameObject.SetActive(false);
        }

        internal void SaveCopy()
        {
            Hide();
            new ProjectFileActions().SaveAs();
        }
    }
}
