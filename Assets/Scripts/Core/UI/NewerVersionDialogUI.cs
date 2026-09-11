using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KitchenDesigner.Core.UI
{
    public sealed class NewerVersionDialogUI : MonoBehaviour
    {
        public static NewerVersionDialogUI? Instance { get; private set; }

        private const float W = 520f;
        private const float H = 260f;

        private RectTransform? _root;
        private TMP_Text? _message;
        private System.Action? _open;
        internal Button? OpenButton;
        internal Button? CancelButton;

        internal bool IsVisible => _root != null && _root.gameObject.activeSelf;
        internal string? MessageText => _message != null ? _message.text : null;

        private void Awake()
        {
            Instance = this;
            NewerVersionPrompt.Show = Ask;
        }

        private void OnDestroy()
        {
            if (!ReferenceEquals(Instance, this)) return;
            Instance = null;
            NewerVersionPrompt.Show = null;
        }

        public void Build(Transform parent)
        {
            var backdrop = UIFactory.CreatePanel("NewerVersionDialogBackdrop", parent,
                Vector2.zero, Vector2.one, UIStyle.ModalBackdrop);
            _root = backdrop.rectTransform;
            _root.anchorMin = Vector2.zero;
            _root.anchorMax = Vector2.one;
            _root.offsetMin = _root.offsetMax = Vector2.zero;

            var panel = UIFactory.CreatePanel("NewerVersionDialogPanel", _root,
                Vector2.zero, new Vector2(W, H), UIStyle.Panel);
            var pr = panel.rectTransform;
            UIFactory.AnchorCenter(pr);
            pr.anchoredPosition = Vector2.zero;

            var title = UIFactory.CreateLabel("NewerVersionDialogTitle", pr,
                NewerVersionStrings.Title, UIStyle.FontWindowTitle,
                new Vector2(0, H / 2f - 30f),
                new Vector2(W - 2f * UIStyle.WindowPad, 30f), TextAnchor.UpperLeft);
            title.fontStyle = FontStyles.Bold;

            _message = UIFactory.CreateLabel("NewerVersionDialogMessage", pr, "",
                UIStyle.FontBody, new Vector2(0, 24f),
                new Vector2(W - 2f * UIStyle.WindowPad, H - 120f), TextAnchor.UpperLeft);
            _message.enableWordWrapping = true;

            CancelButton = UIFactory.CreateButton("NewerVersionDialogCancel", pr,
                NewerVersionStrings.CancelButton, new Vector2(-W / 2f + 100f, -H / 2f + 28f),
                new Vector2(170f, 40f), Cancel);

            OpenButton = UIFactory.CreateButton("NewerVersionDialogOpen", pr,
                NewerVersionStrings.OpenButton, new Vector2(W / 2f - 130f, -H / 2f + 28f),
                new Vector2(170f, 40f), OpenAnyway);
            var image = OpenButton.GetComponent<Image>();
            if (image != null) image.color = UIStyle.Accent;

            _root.gameObject.SetActive(false);
        }

        public void Ask(string message, System.Action open)
        {
            if (_root == null)
            {
                open();
                return;
            }
            _open = open;
            if (_message != null) _message.text = message;
            _root.gameObject.SetActive(true);
            _root.SetAsLastSibling();
        }

        internal void Cancel()
        {
            _open = null;
            Hide();
        }

        internal void OpenAnyway()
        {
            var open = _open;
            _open = null;
            Hide();
            open?.Invoke();
        }

        private void Hide()
        {
            if (_root != null) _root.gameObject.SetActive(false);
        }
    }
}
