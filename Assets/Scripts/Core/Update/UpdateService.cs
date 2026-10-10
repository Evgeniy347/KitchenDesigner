using System.Collections;
using UnityEngine;
using KitchenDesigner.Core.UI;
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
using System;
using System.IO;
using System.Net.Http;
using System.Threading;
#endif

namespace KitchenDesigner.Core.Update
{
    public sealed class UpdateService : MonoBehaviour
    {
        public static bool StartupCheckEnabled = true;
        public const string LegacyCacheCleanedKey = "UpdateLegacyCacheCleaned";

        [SerializeField] private float _startupDelaySeconds = 2f;

        private UpdateFlow? _flow;
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        private HttpClient? _http;
        private HttpInstallerDownloader? _downloader;
#endif

        private void Start()
        {
            if (!StartupCheckEnabled) return;
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            WireAndCheck();
#endif
        }

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        private void WireAndCheck()
        {
            var canvas = UIManager.Instance != null ? UIManager.Instance.Canvas : null;
            if (canvas == null)
            {
                Debug.Log("[Update] нет Canvas — проверка обновлений пропущена");
                return;
            }

            var console = new UnityUpdateConsole();
            CleanLegacyCacheOnce(console);
            var mainThread = gameObject.AddComponent<MainThreadQueue>();
            var checker = gameObject.AddComponent<GitHubReleaseChecker>();
            var folder = new FileSystemUpdateFolder(UpdateFolderLocation.RootUnder(Path.GetTempPath()));

            _http = new HttpClient { Timeout = Timeout.InfiniteTimeSpan };
            _downloader = new HttpInstallerDownloader(_http, folder, mainThread,
                UpdateRetryPolicy.ForInstallerDownload(),
                TimeSpan.FromSeconds(HttpInstallerDownloader.DefaultIdleSeconds));

            var updateGo = new GameObject("UpdateDialog");
            updateGo.transform.SetParent(canvas.transform, false);
            var updateDialog = updateGo.AddComponent<UpdateDialogUI>();
            updateDialog.Build(canvas.transform);

            _flow = new UpdateFlow(
                new UpdatePlanner(BuildInfo.Version),
                checker,
                folder,
                new Sha256FileInspector(folder, mainThread),
                _downloader,
                console,
                updateDialog,
                new InnoUpdateApplier(),
                () => Time.realtimeSinceStartup);

            StartCoroutine(CheckAfterDelay());
        }

        private static void CleanLegacyCacheOnce(IUpdateConsole console)
        {
            var preferences = PreferenceStore.Current;
            if (preferences.GetInt(LegacyCacheCleanedKey, 0) == 1) return;
            var legacy = new FileSystemUpdateFolder(Application.temporaryCachePath);
            if (!LegacyCacheCleanup.Run(legacy, console)) return;
            preferences.SetInt(LegacyCacheCleanedKey, 1);
        }

        private void OnDestroy()
        {
            _downloader?.Dispose();
            _http?.Dispose();
        }
#endif

        private IEnumerator CheckAfterDelay()
        {
            yield return new WaitForSecondsRealtime(_startupDelaySeconds);
            if (_flow != null) _flow.Start();
        }
    }
}
