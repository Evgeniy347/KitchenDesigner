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
            KeepStackTracesOutOfInfoLogs();
            DefaultGameServices.Install();
            DisplaySettings.ApplyWindowMode();
        }

        internal static void KeepStackTracesOutOfInfoLogs() =>
            Application.SetStackTraceLogType(LogType.Log, StackTraceLogType.None);

        private IEnumerator Start()
        {
            yield return LoadDecorCatalogBeforeAnyProjectIsRestored();
            SetupScene();
        }

        private static IEnumerator LoadDecorCatalogBeforeAnyProjectIsRestored()
        {
            if (!TextureLibrary.TryLoadIndexSync())
                yield return TextureLibrary.LoadIndexAsync();
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

            GameContext.Services!.SaveLoadManager.LoadLastSession();
            TextureLibrary.PrefetchScene();

            if (FindAnyObjectByType<MCP.UnityTcpBridge>() == null) gameObject.AddComponent<MCP.UnityTcpBridge>();
            MCP.ConsoleLogCapture.Initialize();

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            if (FindAnyObjectByType<Update.UpdateService>() == null)
                gameObject.AddComponent<Update.UpdateService>();
#endif
        }

        private void OnDestroy()
        {
            GameContext.Clear();
        }

    }
}
