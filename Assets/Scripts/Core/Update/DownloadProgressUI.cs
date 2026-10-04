using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using KitchenDesigner.Core.UI;

namespace KitchenDesigner.Core.Update
{
    public sealed class DownloadProgressUI : MonoBehaviour, IDownloadDialog
    {
        private ModalDialog? _dialog;
        private ProgressBar? _progress;
        private Action? _onCancel;
        private string _message = "";

        internal Button? CancelButton => _dialog?.SecondaryButton;

        internal bool IsVisible => _dialog != null && _dialog.IsVisible;
        internal string? MessageText => _dialog?.Body.text;
        internal string? RetryText => _dialog?.Note.text;
        internal float Progress => _progress != null ? _progress.Value : -1f;
        internal bool ProgressBarIsInteractive =>
            _progress != null && _progress.Root.GetComponentInChildren<Selectable>(true) != null;
        internal RectTransform? BackdropRect => _dialog?.Root;
        internal ModalDialog? Dialog => _dialog;

        public void Build(Transform parent)
        {
            _dialog = ModalDialog.Build(parent, "DownloadDialog");
            _progress = ProgressBar.Create(_dialog.Extra, "DownloadProgressBar");
        }

        public void ShowDownloading(string version, Action onCancel)
        {
            _onCancel = onCancel;
            _message = string.Format(UpdateStrings.DownloadMessage, version);
            SetProgress(0f);
            _dialog?.Show(new ModalDialogContent
            {
                Title = UpdateStrings.DownloadTitle,
                Body = _message,
                ExtraHeight = ProgressBar.Height,
                SecondaryCaption = UpdateStrings.DownloadCancelButton,
                OnSecondary = InvokeCancel,
            });
        }

        public void SetProgress(float t01) => _progress?.SetValue(t01);

        public void ShowRetry(int attemptNumber, int totalAttempts)
        {
            _dialog?.SetNote(string.Format(UpdateStrings.RetryAttempt, attemptNumber, totalAttempts));
            SetProgress(0f);
        }

        public void Hide() => _dialog?.Hide();

        private void InvokeCancel() => _onCancel?.Invoke();
    }
}
