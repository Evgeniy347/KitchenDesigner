#if UNITY_EDITOR || DEVELOPMENT_BUILD

using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using KitchenDesigner.Core;
using KitchenDesigner.Tests;

/// <summary>
/// Профилировочный прогон, а не регрессионный тест: грузит тяжёлую сцену, крутит камеру
/// заданное число кадров и выгружает покадровые метрики в test-results/perf/.
/// Категория "Perf" — прицельный запуск через -testFilter PerfProfileTests.
/// Проверки здесь настоящие (сцена загрузилась, CSV записан), поэтому прогон не «зелёный
/// по построению»: сломанная загрузка или несобравшийся файл валят тест.
///
/// Цифры сняты в редакторе, поэтому абсолютные значения завышены (EditorLoop, отсутствие
/// оптимизаций плеера). Смысл имеет РАСПРЕДЕЛЕНИЕ: кто из маркеров съедает кадр, сколько
/// байт GC приходится на кадр и сколько раз за кадр перебирается реестр.
/// </summary>
/// <remarks>[Explicit] — гоняется только прицельно (`tools\artifacts.ps1 -Only perf`
/// или `-Filter PerfProfileTests`), в обычный прогон не попадает: 660 кадров
/// профилировщика по ~100 мс это 70 секунд, треть всего набора PlayMode.
///
/// Исключать регуляркой не получится: NUnit пускает ВСЕХ потомков узла, который
/// сам прошёл фильтр, поэтому `^(?!.*PerfProfileTests).*$` отсекает класс, но
/// узел сборки проходит — и класс всё равно едет. [Explicit] — единственный
/// механизм, который тут работает.</remarks>
[Explicit("замер, не тест: 70 с профилировщика — см. tools\\artifacts.ps1")]
public class PerfProfileTests
{
    private const int WarmupFrames = 30;
    private const int MeasureFrames = 300;

    private GameObject? _bootstrap;
    private GameObject? _mainCamera;

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        PlayModeTestConfig.ConfigureForTests();

        _mainCamera = new GameObject("Main Camera");
        _mainCamera.tag = "MainCamera";
        _mainCamera.AddComponent<Camera>();

        SaveLoadManager.LastPath = "";
        var autoPath = SaveLoadManager.PathForName(AutoSaveManager.AutoSaveName);
        if (File.Exists(autoPath)) File.Delete(autoPath);

        _bootstrap = new GameObject("Bootstrap");
        _bootstrap.AddComponent<Bootstrap>();

        yield return null;
        yield return null;
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        PerfMonitor.Enabled = false;

        foreach (var e in Object.FindObjectsByType<KitchenElement>(FindObjectsSortMode.None))
            if (e != null) Object.Destroy(e.gameObject);
        foreach (var c in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
            if (c != null) Object.Destroy(c.gameObject);
        if (_bootstrap != null) Object.Destroy(_bootstrap);
        if (_mainCamera != null) Object.Destroy(_mainCamera);
        yield return null;
    }

    /// <summary>Тяжёлая сцена из docs/example.save.json + непрерывное движение камеры.</summary>
    [UnityTest, Category("Perf")]
    public IEnumerator Profile_CameraMovement_OnExampleScene()
    {
        var path = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "docs", "example.save.json"));
        Assert.IsTrue(File.Exists(path), $"Нет файла сцены для профилирования: {path}");
        Assert.IsTrue(SaveLoadManager.LoadFromPath(path), "Сцена не загрузилась");

        yield return null;
        int elements = PartRegistry.GetAll().Count;
        Debug.Log($"[Perf] сцена загружена: {elements} элементов");

        var cam = CameraController.Instance;
        Assert.IsNotNull(cam, "CameraController не создан Bootstrap-ом");

        // Прогрев: первые кадры после загрузки заняты построением мешей и не характерны.
        for (int i = 0; i < WarmupFrames; i++) yield return null;

        PerfMonitor.Enabled = true;
        var monitor = PerfMonitor.Instance;
        Assert.IsNotNull(monitor, "PerfMonitor не создан Bootstrap-ом");
        monitor!.SetCsvRecording(true);

        for (int i = 0; i < MeasureFrames; i++)
        {
            // Тот же путь, что у клавиш и мыши: движение вперёд плюс небольшая орбита,
            // чтобы шевелилась и позиция, и направление взгляда.
            cam!.ApplyWASDMovement(new Vector2(0f, 1f), Time.deltaTime);
            cam.ApplyOrbit(0.35f, 0f);
            yield return null;
        }

        var csv = monitor.SetCsvRecording(false);
        PerfMonitor.DumpNow();
        Assert.IsNotNull(csv, "CSV не записан");
        Debug.Log($"[Perf] профиль движения камеры: {elements} элементов, {MeasureFrames} кадров -> {csv}");
    }

    /// <summary>Контроль: то же самое на пустой сцене. Если здесь ровно, а на тяжёлой рвано —
    /// стоимость кадра зависит от числа элементов, и виновата покадровая логика, не камера.</summary>
    [UnityTest, Category("Perf")]
    public IEnumerator Profile_CameraMovement_OnEmptyScene()
    {
        yield return null;
        var cam = CameraController.Instance;
        Assert.IsNotNull(cam, "CameraController не создан Bootstrap-ом");

        for (int i = 0; i < WarmupFrames; i++) yield return null;

        PerfMonitor.Enabled = true;
        var monitor = PerfMonitor.Instance;
        Assert.IsNotNull(monitor, "PerfMonitor не создан Bootstrap-ом");
        monitor!.SetCsvRecording(true);

        for (int i = 0; i < MeasureFrames; i++)
        {
            cam!.ApplyWASDMovement(new Vector2(0f, 1f), Time.deltaTime);
            cam.ApplyOrbit(0.35f, 0f);
            yield return null;
        }

        var csv = monitor.SetCsvRecording(false);
        PerfMonitor.DumpNow();
        Debug.Log($"[Perf] профиль движения камеры на пустой сцене, {MeasureFrames} кадров -> {csv}");
    }
}

#endif
