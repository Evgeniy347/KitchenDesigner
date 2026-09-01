using System;

namespace KitchenDesigner.Core.Update
{
    public interface IUpdateChecker
    {
        void Check(Action<ReleaseManifest> onSuccess, Action<string> onFailure);
    }

    public interface IInstallerDownloader
    {
        void Start(string url, string targetPath,
            Action<float> onProgress, Action onComplete, Action<string, bool> onFailureWithCancelledFlag);
        void Cancel();
    }

    public interface IUpdateApplier
    {
        void ApplyAndRelaunch(string installerPath);
    }

    public interface IStatusSink
    {
        void Show(string message, StatusLevel level, float seconds);
    }

    public interface IUpdateDialog
    {
        void ShowUpdateAvailable(string version, Action onUpdate, Action onCancel);
        void Hide();
    }

    public interface IDownloadDialog
    {
        void ShowDownloading(string version, Action onCancel);
        void SetProgress(float t01);
        void Hide();
    }
}
