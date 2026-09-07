using System;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

namespace KitchenDesigner.Core.Update
{
    public sealed class UnityWebRequestDownloader : MonoBehaviour, IInstallerDownloader
    {
        [SerializeField] private float _idleSeconds = DownloadStallWatchdog.DefaultIdleSeconds;

        private UnityWebRequest? _active;
        private bool _cancelRequested;

        public UpdateRetryPolicy RetryPolicy { get; set; } = UpdateRetryPolicy.ForInstallerDownload();

        public void BeginDownload(string url, string targetPath,
            Action<float> onProgress, Action<int, int> onAttemptStarted,
            Action onComplete, Action<string, bool> onFailure)
        {
            _cancelRequested = false;
            StartCoroutine(Run(url, targetPath, onProgress, onAttemptStarted, onComplete, onFailure));
        }

        public void Cancel()
        {
            _cancelRequested = true;
            AbortEvenIfTheRequestAlreadyFinished();
        }

        private void AbortEvenIfTheRequestAlreadyFinished()
        {
            _active?.Abort();
        }

        private System.Collections.IEnumerator Run(string url, string targetPath,
            Action<float> onProgress, Action<int, int> onAttemptStarted,
            Action onComplete, Action<string, bool> onFailure)
        {
            string? folderError = PrepareFolder(targetPath);
            if (folderError != null)
            {
                onFailure?.Invoke(folderError, false);
                yield break;
            }

            var policy = RetryPolicy;
            var outcome = new UpdateAttemptOutcome();

            for (int attempt = 1; attempt <= policy.MaxAttempts; attempt++)
            {
                float pause = policy.PauseBeforeAttemptSeconds(attempt);
                if (pause > 0f)
                {
                    TryDelete(targetPath);
                    yield return new WaitForSecondsRealtime(pause);
                    if (_cancelRequested) break;
                }

                onAttemptStarted?.Invoke(attempt, policy.MaxAttempts);
                yield return SendOnce(url, targetPath, onProgress, outcome);

                if (outcome.Succeeded)
                {
                    onProgress?.Invoke(1f);
                    onComplete?.Invoke();
                    yield break;
                }
                if (!policy.ShouldRetryAfter(attempt, outcome.Failure)) break;
            }

            TryDelete(targetPath);
            if (_cancelRequested) onFailure?.Invoke(UpdateStrings.DownloadCancelled, true);
            else onFailure?.Invoke(outcome.Reason, false);
        }

        private System.Collections.IEnumerator SendOnce(string url, string targetPath,
            Action<float> onProgress, UpdateAttemptOutcome outcome)
        {
            var req = new UnityWebRequest(url, UnityWebRequest.kHttpVerbGET);
            req.downloadHandler = new DownloadHandlerFile(targetPath);
            req.SetRequestHeader("User-Agent", "KitchenDesigner-Updater");
            req.chunkedTransfer = false;
            _active = req;
            req.SendWebRequest();

            var watchdog = new DownloadStallWatchdog(_idleSeconds);
            bool stalled = false;

            while (!req.isDone && !stalled)
            {
                onProgress?.Invoke(req.downloadProgress);
                watchdog.Observe((long)req.downloadedBytes, Time.realtimeSinceStartup);
                stalled = watchdog.IsStalled(Time.realtimeSinceStartup);
                yield return null;
            }

            var result = req.result;
            long code = req.responseCode;
            string error = req.error;
            _active = null;
            req.Dispose();

            if (_cancelRequested)
                outcome.Fail(UpdateStrings.DownloadCancelled, UpdateAttemptFailure.Cancelled());
            else if (stalled)
                outcome.Fail($"загрузка встала: нет новых байтов {watchdog.IdleSeconds:0} с",
                    UpdateAttemptFailure.NoAnswer());
            else if (result != UnityWebRequest.Result.Success)
                outcome.Fail($"HTTP {code}: {error}", UpdateAttemptFailure.FromResponse(code));
            else
                outcome.Succeed();
        }

        private static string? PrepareFolder(string targetPath)
        {
            try
            {
                var dir = Path.GetDirectoryName(targetPath);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                return null;
            }
            catch (Exception e)
            {
                return "Не удалось подготовить папку: " + e.Message;
            }
        }

        internal static void TryDelete(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }
}
