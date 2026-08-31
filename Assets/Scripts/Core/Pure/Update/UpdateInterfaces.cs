using System;

namespace KitchenDesigner.Core.Update
{
    /// <summary>Опрос GitHub на предмет последнего релиза. Реализация-адаптер делает
    /// сетевой запрос; координатор зависит только от интерфейса, поэтому тестируется
    /// без сети.</summary>
    public interface IUpdateChecker
    {
        void Check(Action<ReleaseManifest> onSuccess, Action<string> onFailure);
    }

    /// <summary>Скачивание установщика во временный файл. Реализация работает в
    /// фоне (корутина) и зовёт колбэки. <see cref="Cancel"/> должна привести к
    /// <c>onFailure(_, true)</c>.</summary>
    public interface IInstallerDownloader
    {
        void Start(string url, string targetPath,
            Action<float> onProgress, Action onComplete, Action<string, bool> onFailure);
        void Cancel();
    }

    /// <summary>Финальный шаг: закрыть текущий процесс, тихо запустить установщик,
    /// чтобы после установки поднялась новая версия. В тестах — фейк, который лишь
    /// запоминает вызов, ничего не запуская.</summary>
    public interface IUpdateApplier
    {
        void ApplyAndRelaunch(string installerPath);
    }

    /// <summary>Одноразовое сообщение в статус-полосе. Реализация — обёртка над
    /// <c>StatusBarUI</c>.</summary>
    public interface IStatusSink
    {
        void Show(string message, StatusLevel level, float seconds);
    }

    /// <summary>Модалка «есть новая версия».</summary>
    public interface IUpdateDialog
    {
        void ShowUpdateAvailable(string version, Action onUpdate, Action onCancel);
        void Hide();
    }

    /// <summary>Окно процесса загрузки с кнопкой «Отмена».</summary>
    public interface IDownloadDialog
    {
        void ShowDownloading(string version, Action onCancel);
        void SetProgress(float t01);
        void Hide();
    }
}
