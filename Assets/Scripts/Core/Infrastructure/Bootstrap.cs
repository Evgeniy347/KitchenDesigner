using System;
using System.Collections;
using UnityEngine;

namespace KitchenDesigner.Core
{
    public class Bootstrap : MonoBehaviour
    {
        private void Awake()
        {
            Application.runInBackground = true;
            // Инфо-логи (Debug.Log, в т.ч. каждый «[MCP] …») не должны тащить
            // полный стек-трейс в Player.log/консоль — это замусоривало лог
            // (~10 строк стека на каждый информационный лог). Стек оставляем
            // только для предупреждений и ошибок, где он реально нужен.
            Application.SetStackTraceLogType(LogType.Log, StackTraceLogType.None);
            // Ограничение FPS теперь динамическое — им управляет FrameRateManager
            // (активный FPS при активности, «почти ноль» в простое).
            GameContext.InitializeWithDefaults();
            // Настройки загружаются вместе с проектом через SaveLoadManager.LoadLastSession().
            DisplaySettings.ApplyWindowMode();
        }

        // Каталог декоров должен быть прочитан ДО восстановления сцены, иначе
        // сохранённые materialId не найдут свой декор. На десктопе это обычное
        // чтение файла, в WebGL — запрос, поэтому старт живёт в корутине.
        private IEnumerator Start()
        {
            if (!TextureLibrary.TryLoadIndexSync())
                yield return TextureLibrary.LoadIndexAsync();

            SetupScene();
        }

        private void SetupScene()
        {
            if (FindAnyObjectByType<BasePlate>() == null) BasePlate.Create();
            if (FindAnyObjectByType<CameraController>() == null) gameObject.AddComponent<CameraController>();
            if (FindAnyObjectByType<SelectionManager>() == null) gameObject.AddComponent<SelectionManager>();
            if (FindAnyObjectByType<ElementMover>() == null) gameObject.AddComponent<ElementMover>();
            if (FindAnyObjectByType<ElementHighlighter>() == null) gameObject.AddComponent<ElementHighlighter>();
            if (FindAnyObjectByType<AttachRider>() == null) gameObject.AddComponent<AttachRider>();
            if (FindAnyObjectByType<UI.UIManager>() == null) gameObject.AddComponent<UI.UIManager>();
            if (FindAnyObjectByType<AutoSaveManager>() == null) gameObject.AddComponent<AutoSaveManager>();
            if (FindAnyObjectByType<FrameRateManager>() == null)
                new GameObject("FrameRateManager").AddComponent<FrameRateManager>();
            if (FindAnyObjectByType<SpatialGridRenderer>() == null) gameObject.AddComponent<SpatialGridRenderer>();
            if (FindAnyObjectByType<EdgeOutlineRenderer>() == null) gameObject.AddComponent<EdgeOutlineRenderer>();
            if (FindAnyObjectByType<Measure.MeasureController>() == null)
                gameObject.AddComponent<Measure.MeasureController>();
            if (FindAnyObjectByType<Measure.MeasureRenderer>() == null)
                gameObject.AddComponent<Measure.MeasureRenderer>();
            if (FindAnyObjectByType<Tools.EyedropperController>() == null)
                gameObject.AddComponent<Tools.EyedropperController>();
            if (FindAnyObjectByType<SceneChangeTracker>() == null) gameObject.AddComponent<SceneChangeTracker>();
            if (FindAnyObjectByType<WallManager>() == null) gameObject.AddComponent<WallManager>();
            if (FindAnyObjectByType<SceneVisibilityManager>() == null) gameObject.AddComponent<SceneVisibilityManager>();
            if (FindAnyObjectByType<ResizeHandleManager>() == null) gameObject.AddComponent<ResizeHandleManager>();
            if (FindAnyObjectByType<TextureOverlayRenderer>() == null) gameObject.AddComponent<TextureOverlayRenderer>();
            if (FindAnyObjectByType<TextureOverlayHandles>() == null) gameObject.AddComponent<TextureOverlayHandles>();
            if (FindAnyObjectByType<UI.ConsoleOverlay>() == null) gameObject.AddComponent<UI.ConsoleOverlay>();
            if (FindAnyObjectByType<UndoHandler>() == null) gameObject.AddComponent<UndoHandler>();

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (FindAnyObjectByType<PerfMonitor>() == null) gameObject.AddComponent<PerfMonitor>();
#endif

#if UNITY_WEBGL
            if (FindAnyObjectByType<Networking.ProjectApiClient>() == null)
                gameObject.AddComponent<Networking.ProjectApiClient>();

            var urlProjectId = ParseProjectIdFromUrl();
            if (!string.IsNullOrEmpty(urlProjectId))
                Networking.ProjectApiClient.Instance!.CurrentProjectId = urlProjectId;

            var urlLockGuid = GetQueryParam("lockGuid");
            if (!string.IsNullOrEmpty(urlLockGuid))
                Networking.ProjectApiClient.Instance!.LockGuid = urlLockGuid;

            Networking.ProjectApiClient.Instance!.FetchConfig(enabled =>
            {
                if (enabled)
                {
                    var api = Networking.ProjectApiClient.Instance;
                    if (api.HasCurrentProject)
                    {
                        api.LoadProject(api.CurrentProjectId,
                            (name, jsonData) =>
                            {
                                var projectData = SaveLoadManager.Deserialize(jsonData);
                                if (projectData == null) return;
                                SaveLoadManager.LastPath = api.CurrentProjectId;
                                SaveLoadManager.ClearBoards(PartRegistry.Instance.GetAll());
                                SaveLoadManager.RestoreScene(projectData);
                                if (ElementHighlighter.Instance != null)
                                    ElementHighlighter.Instance.RefreshHighlights();
                                TextureLibrary.PrefetchScene();
                            },
                            _ =>
                            {
            GameContext.Services!.SaveLoadManager.LoadLastSession();
                                TextureLibrary.PrefetchScene();
                            });
                        return;
                    }
                }
                // Server save disabled or no current project — fall back to local.
            GameContext.Services!.SaveLoadManager.LoadLastSession();
                TextureLibrary.PrefetchScene();
            });
#else
            GameContext.Services!.SaveLoadManager.LoadLastSession();
            TextureLibrary.PrefetchScene();
#endif

#if UNITY_WEBGL
            if (FindAnyObjectByType<MCP.WebSocketBridge>() == null)
            {
                var wsBridge = gameObject.AddComponent<MCP.WebSocketBridge>();
                wsBridge.SetAutoConnect(false);

                var mcpKey = GetQueryParam("mcpKey");
                if (!string.IsNullOrEmpty(mcpKey))
                {
                    var wsUrl = BuildWebSocketUrl("/api/mcp/ws");
                    var projectId = GetQueryParam("projectId");
                    if (!string.IsNullOrEmpty(projectId))
                        wsUrl += "?projectId=" + Uri.EscapeDataString(projectId);
                    wsBridge.Connect(wsUrl!, mcpKey);
                }
            }
#else
            if (FindAnyObjectByType<MCP.UnityTcpBridge>() == null) gameObject.AddComponent<MCP.UnityTcpBridge>();
#endif
            MCP.ConsoleLogCapture.Initialize();
        }

        private void OnDestroy()
        {
            GameContext.Clear();
        }

#if UNITY_WEBGL
        private static string? GetQueryParam(string name)
        {
            var url = Application.absoluteURL;
            if (string.IsNullOrEmpty(url)) return null;

            var queryIndex = url.IndexOf('?');
            if (queryIndex < 0) return null;

            var query = url.Substring(queryIndex + 1);
            foreach (var pair in query.Split('&'))
            {
                var eqIndex = pair.IndexOf('=');
                if (eqIndex < 0) continue;

                var key = pair.Substring(0, eqIndex);
                if (!string.Equals(key, name, StringComparison.OrdinalIgnoreCase))
                    continue;

                return Uri.UnescapeDataString(pair.Substring(eqIndex + 1));
            }

            return null;
        }

        private static string? ParseProjectIdFromUrl() => GetQueryParam("projectId");

        private static string? BuildWebSocketUrl(string path)
        {
            var url = Application.absoluteURL;
            if (string.IsNullOrEmpty(url)) return null;

            var uri = new Uri(url);
            var scheme = uri.Scheme == Uri.UriSchemeHttps ? "wss" : "ws";
            return $"{scheme}://{uri.Authority}{path}";
        }
#endif
    }
}
