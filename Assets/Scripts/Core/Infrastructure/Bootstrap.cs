using UnityEngine;

namespace KitchenDesigner.Core
{
    public class Bootstrap : MonoBehaviour
    {
        private void Awake()
        {
            Application.runInBackground = true;
            GameContext.InitializeWithDefaults();
            if (KitchenSettings.Instance != null)
                KitchenSettings.Instance.Load();
            // Внешние текстуры грузим ДО загрузки сцены (Start → LoadLastSession),
            // иначе сохранённые materialId не найдут свой декор в каталоге.
            ExternalTextureCatalog.LoadAll();
            DisplaySettings.ApplyWindowMode();
        }

        private void Start()
        {
            if (FindAnyObjectByType<BasePlate>() == null) BasePlate.Create();
            if (FindAnyObjectByType<CameraController>() == null) gameObject.AddComponent<CameraController>();
            if (FindAnyObjectByType<SelectionManager>() == null) gameObject.AddComponent<SelectionManager>();
            if (FindAnyObjectByType<ElementMover>() == null) gameObject.AddComponent<ElementMover>();
            if (FindAnyObjectByType<ElementHighlighter>() == null) gameObject.AddComponent<ElementHighlighter>();
            if (FindAnyObjectByType<UI.UIManager>() == null) gameObject.AddComponent<UI.UIManager>();
            if (FindAnyObjectByType<AutoSaveManager>() == null) gameObject.AddComponent<AutoSaveManager>();
            if (FindAnyObjectByType<SpatialGridRenderer>() == null) gameObject.AddComponent<SpatialGridRenderer>();
            if (FindAnyObjectByType<EdgeOutlineRenderer>() == null) gameObject.AddComponent<EdgeOutlineRenderer>();
            if (FindAnyObjectByType<WallManager>() == null) gameObject.AddComponent<WallManager>();
            if (FindAnyObjectByType<ResizeHandleManager>() == null) gameObject.AddComponent<ResizeHandleManager>();
            if (FindAnyObjectByType<UI.ConsoleOverlay>() == null) gameObject.AddComponent<UI.ConsoleOverlay>();
            if (FindAnyObjectByType<UndoHandler>() == null) gameObject.AddComponent<UndoHandler>();
            GameContext.Services.SaveLoadManager.LoadLastSession();
#if !UNITY_WEBGL
            if (FindAnyObjectByType<MCP.UnityTcpBridge>() == null) gameObject.AddComponent<MCP.UnityTcpBridge>();
#endif
            MCP.ConsoleLogCapture.Initialize();
        }

        private void OnDestroy()
        {
            GameContext.Clear();
        }
    }
}
