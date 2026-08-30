using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using KitchenDesigner.Core.UI;

namespace KitchenDesigner.Core.Update
{
    /// <summary>
    /// Окно процесса загрузки установщика: заголовок, текст (с обещанием
    /// авто-перезапуска), полоса прогресса и кнопка «Отмена». Как и диалог
    /// обновления — без корутин, <see cref="SetProgress"/>/Show/Hide обращаются
    /// к состоянию напрямую, поэтому тестируется в EditMode.
    /// </summary>
    public sealed class DownloadProgressUI : MonoBehaviour, IDownloadDialog
    {
        private RectTransform? _root;
        private TMP_Text? _title;
        private TMP_Text? _message;
        private Slider? _progress;
        internal Button? CancelButton;

        private Action? _onCancel;

        // Внутренние геттеры для EditMode-тестов (internalsVisibleTo).
        internal bool IsVisible => _root != null && _root.gameObject.activeSelf;
        internal string? MessageText => _message != null ? _message.text : null;
        internal float Progress => _progress != null ? _progress.value : -1f;
        internal RectTransform? TitleRect => _title != null ? _title.rectTransform : null;
        internal RectTransform? MessageRect => _message != null ? _message.rectTransform : null;


        private const float W = 460f;
        private const float H = 210f;

        public void Build(Transform parent)
        {
            var backdrop = UIFactory.CreatePanel("DownloadBackdrop", parent,
                Vector2.zero, Vector2.one, new Color(0f, 0f, 0f, 0.55f));
            _root = backdrop.rectTransform;
            _root.anchorMin = Vector2.zero;
            _root.anchorMax = Vector2.one;
            _root.offsetMin = _root.offsetMax = Vector2.zero;

            var panel = UIFactory.CreatePanel("DownloadPanel", _root,
                Vector2.zero, new Vector2(W, H), UIStyle.Panel);
            var pr = panel.rectTransform;
            UIFactory.AnchorCenter(pr);
            pr.anchoredPosition = Vector2.zero;

            var title = UIFactory.CreateLabel("DownloadTitle", pr, UpdateStrings.DownloadTitle,
                22, new Vector2(0, H / 2f - 30f), new Vector2(W - 40f, 30f), TextAnchor.UpperLeft);
            title.fontStyle = FontStyles.Bold;
            _title = title;

            _message = UIFactory.CreateLabel("DownloadMessage", pr, "",
                16, new Vector2(0, 15f), new Vector2(W - 40f, 90f), TextAnchor.UpperLeft);
            _message.enableWordWrapping = true;

            _progress = UIFactory.CreateSlider("DownloadProgressBar", pr, 0f, 1f, 0f,
                new Vector2(0, -30f), new Vector2(W - 40f, 24f), null!);
            _progress.interactable = false;   // только показывает ход
            _progress.fillRect.GetComponent<Image>().color = UIStyle.Accent;

            CancelButton = UIFactory.CreateButton("DownloadCancel", pr,
                UpdateStrings.DownloadCancelButton, new Vector2(0, -H / 2f + 30f),
                new Vector2(170f, 40f), InvokeCancel);

            _root.gameObject.SetActive(false);
        }

        public void ShowDownloading(string version, Action onCancel)
        {
            _onCancel = onCancel;
            if (_message != null) _message.text = string.Format(UpdateStrings.DownloadMessage, version);
            SetProgress(0f);
            if (_root != null)
            {
                _root.gameObject.SetActive(true);
                _root.SetAsLastSibling();
            }
        }

        public void SetProgress(float t01)
        {
            if (_progress != null) _progress.value = Mathf.Clamp01(t01);
        }

        public void Hide()
        {
            if (_root != null) _root.gameObject.SetActive(false);
        }

        private void InvokeCancel() => _onCancel?.Invoke();
    }
}
