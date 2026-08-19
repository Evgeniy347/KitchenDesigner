#if !UNITY_WEBGL
using System;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

namespace KitchenDesigner.Core.Update
{
    /// <summary>Скачивание установщика в целевой файл с помощью
    /// <see cref="DownloadHandlerFile"/> (не держим ~40 МБ в памяти). Прогресс —
    /// из <c>downloadProgress</c>. <see cref="Cancel"/> прерывает запрос; на
    /// прерывании недокачанный файл удаляется.</summary>
    public sealed class UnityWebRequestDownloader : MonoBehaviour, IInstallerDownloader
    {
        [SerializeField] private int _timeoutSeconds = 300;

        private UnityWebRequest? _active;
        private bool _cancelRequested;

        public void Start(string url, string targetPath,
            Action<float> onProgress, Action onComplete, Action<string, bool> onFailure)
        {
            _cancelRequested = false;
            StartCoroutine(Run(url, targetPath, onProgress, onComplete, onFailure));
        }

        public void Cancel()
        {
            _cancelRequested = true;
            try { _active?.Abort(); } catch { /* уже завершён */ }
        }

        private System.Collections.IEnumerator Run(string url, string targetPath,
            Action<float> onProgress, Action onComplete, Action<string, bool> onFailure)
        {
            try
            {
                var dir = Path.GetDirectoryName(targetPath);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            }
            catch (Exception e)
            {
                onFailure?.Invoke("Не удалось подготовить папку: " + e.Message, false);
                yield break;
            }

            var req = new UnityWebRequest(url, UnityWebRequest.kHttpVerbGET);
            var file = new DownloadHandlerFile(targetPath);
            req.downloadHandler = file;
            req.SetRequestHeader("User-Agent", "KitchenDesigner-Updater");
            req.timeout = _timeoutSeconds;
            req.chunkedTransfer = false;
            _active = req;

            UnityWebRequest.Result result;
            string err;
            long code;
            bool cancelled;

            while (!req.isDone)
            {
                onProgress?.Invoke(req.downloadProgress);
                yield return null;
            }

            result = req.result;
            err = req.error;
            code = req.responseCode;
            // Abort() приводит к ConnectionError; настоящее решение — по нашему флагу.
            cancelled = _cancelRequested;
            _active = null;
            req.Dispose();

            if (cancelled)
            {
                TryDelete(targetPath);
                onFailure?.Invoke("Загрузка отменена", true);
                yield break;
            }
            if (result != UnityWebRequest.Result.Success)
            {
                TryDelete(targetPath);
                onFailure?.Invoke($"HTTP {code}: {err}", false);
                yield break;
            }
            onProgress?.Invoke(1f);
            onComplete?.Invoke();
        }

        private static void TryDelete(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); } catch { }
        }
    }
}
#endif
