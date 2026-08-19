#if !UNITY_WEBGL
using System.Collections;
using UnityEngine;
using KitchenDesigner.Core.UI;

namespace KitchenDesigner.Core.Update
{
    /// <summary>
    /// Точка входа автообновления: собирает координатор из реальных адаптеров и
    /// запускает проверку через пару секунд после старта, чтобы не конкурировать
    /// с загрузкой сцены/текстур. Создаётся <see cref="Bootstrap"/> только в
    /// собранном Windows-плеере; <see cref="StartupCheckEnabled"/> выключают тесты.
    /// </summary>
    public sealed class UpdateService : MonoBehaviour
    {
        /// <summary>Тесты/PlayModeTestConfig ставят false, чтобы даже случайно
        /// созданный сервис не полез в сеть.</summary>
        public static bool StartupCheckEnabled = true;

        [SerializeField] private float _startupDelaySeconds = 2f;

        private UpdateCoordinator? _coordinator;

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

            var checker = gameObject.AddComponent<GitHubReleaseChecker>();
            var downloader = gameObject.AddComponent<UnityWebRequestDownloader>();
            var applier = new InnoUpdateApplier();
            var status = new StatusBarSink();

            var updateGo = new GameObject("UpdateDialog");
            updateGo.transform.SetParent(canvas.transform, false);
            var updateDialog = updateGo.AddComponent<UpdateDialogUI>();
            updateDialog.Build(canvas.transform);

            var downloadGo = new GameObject("DownloadProgress");
            downloadGo.transform.SetParent(canvas.transform, false);
            var downloadDialog = downloadGo.AddComponent<DownloadProgressUI>();
            downloadDialog.Build(canvas.transform);

            _coordinator = new UpdateCoordinator(
                checker, downloader, applier, status, updateDialog, downloadDialog,
                BuildInfo.Version, () => Application.temporaryCachePath);

            StartCoroutine(CheckAfterDelay());
        }
#endif

        private IEnumerator CheckAfterDelay()
        {
            yield return new WaitForSecondsRealtime(_startupDelaySeconds);
            if (_coordinator != null) _coordinator.CheckForUpdates();
        }
    }
}
#endif
