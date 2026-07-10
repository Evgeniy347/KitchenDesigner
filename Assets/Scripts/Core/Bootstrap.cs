using UnityEngine;

namespace KitchenDesigner.Core
{
    public class Bootstrap : MonoBehaviour
    {
        private void Awake()
        {
            // Без этого плеер замирает при потере фокуса окна: перестают работать
            // автосохранение по таймеру и TCP-мост отладки (запрос принимается
            // фоновым потоком, а ответ шлётся из главного цикла — он на паузе).
            Application.runInBackground = true;

            if (KitchenSettings.Instance != null)
                KitchenSettings.Instance.Load();
            DisplaySettings.ApplyWindowMode();
        }

        private void Start()
        {
            if (FindAnyObjectByType<BasePlate>() == null)
                BasePlate.Create();

            if (FindAnyObjectByType<CameraController>() == null)
                gameObject.AddComponent<CameraController>();

            if (FindAnyObjectByType<SelectionManager>() == null)
                gameObject.AddComponent<SelectionManager>();

            if (FindAnyObjectByType<ElementMover>() == null)
                gameObject.AddComponent<ElementMover>();

            // InputCapture — диагностический логгер ввода, не подключаем по умолчанию
            // (создаётся вручную при отладке). Спамит консоль каждым нажатием.

            if (FindAnyObjectByType<ElementHighlighter>() == null)
                gameObject.AddComponent<ElementHighlighter>();

            if (FindAnyObjectByType<UI.UIManager>() == null)
                gameObject.AddComponent<UI.UIManager>();

            if (FindAnyObjectByType<AutoSaveManager>() == null)
                gameObject.AddComponent<AutoSaveManager>();

            if (FindAnyObjectByType<SpatialGridRenderer>() == null)
                gameObject.AddComponent<SpatialGridRenderer>();

            if (FindAnyObjectByType<EdgeOutlineRenderer>() == null)
                gameObject.AddComponent<EdgeOutlineRenderer>();

            if (FindAnyObjectByType<WallManager>() == null)
                gameObject.AddComponent<WallManager>();

            if (FindAnyObjectByType<ResizeHandleManager>() == null)
                gameObject.AddComponent<ResizeHandleManager>();

            if (FindAnyObjectByType<UI.ConsoleOverlay>() == null)
                gameObject.AddComponent<UI.ConsoleOverlay>();

            if (FindAnyObjectByType<UndoHandler>() == null)
                gameObject.AddComponent<UndoHandler>();

            // Подгружаем последнее сохранение при старте (последний файл или автосейв).
            SaveLoadManager.LoadLastSession();

            if (FindAnyObjectByType<MCP.UnityTcpBridge>() == null)
                gameObject.AddComponent<MCP.UnityTcpBridge>();

            MCP.ConsoleLogCapture.Initialize();
        }
    }
}
