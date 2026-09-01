using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using KitchenDesigner.Core.UI;

namespace KitchenDesigner.Core.Update
{
    public sealed class UpdateDialogUI : MonoBehaviour, IUpdateDialog
    {
        private RectTransform? _root;
        private TMP_Text? _title;
        private TMP_Text? _message;
        internal Button? UpdateButton;
        internal Button? CancelButton;

        private Action? _onUpdate;
        private Action? _onCancel;

        internal bool IsVisible => _root != null && _root.gameObject.activeSelf;
        internal string? MessageText => _message != null ? _message.text : null;
        internal RectTransform? BackdropRect => _root;


        private const float W = 460f;
        private const float H = 210f;

        public void Build(Transform parent)
        {
            var backdrop = UIFactory.CreatePanel("UpdateDialogBackdrop", parent,
                Vector2.zero, Vector2.one, new Color(0f, 0f, 0f, 0.55f));
            _root = backdrop.rectTransform;
            _root.anchorMin = Vector2.zero;
            _root.anchorMax = Vector2.one;
            _root.offsetMin = _root.offsetMax = Vector2.zero;

            var panel = UIFactory.CreatePanel("UpdateDialogPanel", _root,
                Vector2.zero, new Vector2(W, H), UIStyle.Panel);
            var pr = panel.rectTransform;
            UIFactory.AnchorCenter(pr);
            pr.anchoredPosition = Vector2.zero;

            _title = UIFactory.CreateLabel("UpdateDialogTitle", pr, UpdateStrings.UpdateTitle,
                22, new Vector2(0, H / 2f - 30f), new Vector2(W - 40f, 30f), TextAnchor.UpperLeft);
            _title.fontStyle = FontStyles.Bold;

            _message = UIFactory.CreateLabel("UpdateDialogMessage", pr, "",
                16, new Vector2(0, 20f), new Vector2(W - 40f, H - 120f), TextAnchor.UpperLeft);
            _message.enableWordWrapping = true;

            CancelButton = UIFactory.CreateButton("UpdateDialogCancel", pr,
                UpdateStrings.UpdateCancelButton, new Vector2(-W / 2f + 100f, -H / 2f + 28f),
                new Vector2(170f, 40f), InvokeCancel);

            UpdateButton = UIFactory.CreateButton("UpdateDialogUpdate", pr,
                UpdateStrings.UpdateAcceptButton, new Vector2(W / 2f - 130f, -H / 2f + 28f),
                new Vector2(250f, 40f), InvokeUpdate);
            var img = UpdateButton.GetComponent<Image>();
            if (img != null) img.color = UIStyle.Accent;

            _root.gameObject.SetActive(false);
        }

        public void ShowUpdateAvailable(string version, Action onUpdate, Action onCancel)
        {
            _onUpdate = onUpdate;
            _onCancel = onCancel;
            if (_message != null) _message.text = string.Format(UpdateStrings.UpdateMessage, version);
            if (_root != null)
            {
                _root.gameObject.SetActive(true);
                _root.SetAsLastSibling();
            }
        }

        public void Hide()
        {
            if (_root != null) _root.gameObject.SetActive(false);
        }

        private void InvokeUpdate() => _onUpdate?.Invoke();
        private void InvokeCancel() => _onCancel?.Invoke();
    }
}
