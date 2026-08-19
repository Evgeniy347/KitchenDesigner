using System;
using UnityEngine;

namespace KitchenDesigner.Core.Update
{
    /// <summary>
    /// Конечный автомат автообновления — вся ветка решения «проверка → диалог →
    /// загрузка → установка» и все пользовательские сообщения. Класс сознательно
    /// НЕ трогает сеть, диск и процессы: всё внешнее приходит через интерфейсы,
    /// поэтому поведение полностью покрывается EditMode-тестами на фейках.
    /// </summary>
    public sealed class UpdateCoordinator
    {
        public enum State { Idle, Checking, UpdateAvailable, Downloading }

        public State CurrentState { get; private set; } = State.Idle;

        private readonly IUpdateChecker _checker;
        private readonly IInstallerDownloader _downloader;
        private readonly IUpdateApplier _applier;
        private readonly IStatusSink _status;
        private readonly IUpdateDialog _updateDialog;
        private readonly IDownloadDialog _downloadDialog;
        private readonly string _currentVersion;
        private readonly Func<string> _tempDirProvider;

        private ReleaseManifest? _pending;
        private string? _targetPath;

        public UpdateCoordinator(
            IUpdateChecker checker,
            IInstallerDownloader downloader,
            IUpdateApplier applier,
            IStatusSink status,
            IUpdateDialog updateDialog,
            IDownloadDialog downloadDialog,
            string currentVersion,
            Func<string> tempDirProvider)
        {
            _checker = checker ?? throw new ArgumentNullException(nameof(checker));
            _downloader = downloader ?? throw new ArgumentNullException(nameof(downloader));
            _applier = applier ?? throw new ArgumentNullException(nameof(applier));
            _status = status ?? throw new ArgumentNullException(nameof(status));
            _updateDialog = updateDialog ?? throw new ArgumentNullException(nameof(updateDialog));
            _downloadDialog = downloadDialog ?? throw new ArgumentNullException(nameof(downloadDialog));
            _currentVersion = currentVersion ?? string.Empty;
            _tempDirProvider = tempDirProvider ?? throw new ArgumentNullException(nameof(tempDirProvider));
        }

        /// <summary>Запускать после загрузки приложения. Повторный вызов во время
        /// активной проверки/загрузки игнорируется (не плодим параллельные потоки).</summary>
        public void CheckForUpdates()
        {
            if (CurrentState != State.Idle) return;
            CurrentState = State.Checking;
            _checker.Check(OnCheckSuccess, OnCheckFailure);
        }

        private void OnCheckSuccess(ReleaseManifest manifest)
        {
            // Пока пришёл ответ, мы ещё «Checking»; переходим дальше.
            if (manifest == null)
            {
                FinishAsIdle();
                _status.Show(UpdateStrings.CheckError, StatusLevel.Error, UpdateStrings.TransientSeconds);
                return;
            }

            int cmp = VersionUtil.Compare(manifest.Version, _currentVersion);
            if (cmp > 0)
            {
                _pending = manifest;
                CurrentState = State.UpdateAvailable;
                _updateDialog.ShowUpdateAvailable(manifest.Version, OnUserChoseUpdate, OnUserDismissed);
            }
            else
            {
                CurrentState = State.Idle;
                _status.Show(UpdateStrings.UpToDate, StatusLevel.Success, UpdateStrings.TransientSeconds);
            }
        }

        private void OnCheckFailure(string reason)
        {
            Debug.Log($"[Update] проверка не удалась: {reason}");
            FinishAsIdle();
            _status.Show(UpdateStrings.CheckError, StatusLevel.Error, UpdateStrings.TransientSeconds);
        }

        private void OnUserDismissed()
        {
            // «Отмена»: просто закрываем диалог, юзер продолжает работать.
            _updateDialog.Hide();
            _pending = null;
            CurrentState = State.Idle;
        }

        private void OnUserChoseUpdate()
        {
            if (_pending == null) { CurrentState = State.Idle; return; }

            _updateDialog.Hide();
            CurrentState = State.Downloading;

            _targetPath = System.IO.Path.Combine(_tempDirProvider(), _pending.FileName);
            _downloadDialog.ShowDownloading(_pending.Version, OnDownloadCancelRequested);
            _downloader.Start(_pending.DownloadUrl, _targetPath,
                OnDownloadProgress, OnDownloadComplete, OnDownloadFailure);
        }

        private void OnDownloadProgress(float t01)
        {
            _downloadDialog.SetProgress(Mathf.Clamp01(t01));
        }

        private void OnDownloadComplete()
        {
            _downloadDialog.Hide();
            CurrentState = State.Idle;
            _pending = null;
            // Закрытие процесса и перезапуск — внутри адаптера; здесь всё заканчивается.
            _applier.ApplyAndRelaunch(_targetPath!);
        }

        private void OnDownloadCancelRequested()
        {
            _downloader.Cancel(); // приведёт к OnDownloadFailure(_, cancelled: true)
        }

        private void OnDownloadFailure(string reason, bool cancelled)
        {
            _downloadDialog.Hide();
            CurrentState = State.Idle;
            _pending = null;
            if (cancelled)
                _status.Show(UpdateStrings.DownloadCancelled, StatusLevel.Info, UpdateStrings.TransientSeconds);
            else
            {
                Debug.Log($"[Update] загрузка не удалась: {reason}");
                _status.Show(UpdateStrings.DownloadError, StatusLevel.Error, UpdateStrings.TransientSeconds);
            }
        }

        private void FinishAsIdle() => CurrentState = State.Idle;
    }
}
