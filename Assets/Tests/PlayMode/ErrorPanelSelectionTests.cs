using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using KitchenDesigner.Core;
using KitchenDesigner.Core.Analysis;
using KitchenDesigner.Core.UI;

/// <summary>
/// PlayMode: тесты выделения строк и копирования в окне «Ошибки».
/// Проверяются только публичные API панели — никакого EventsSystem,
/// никакой симуляции клавиатуры. Это потому, что модификаторы читаются
/// через Input.GetKey в момент клика; в PlayMode-тесте эти клавиши
/// не нажаты, и так НЕ протестировать Shift/Ctrl-ветки. Поэтому
/// HandleRowClick публичный и принимает модификаторы параметрами.
/// </summary>
public class ErrorPanelSelectionTests
{
    private GameObject? _bootstrap;
    private GameObject? _camera;
    private readonly System.Collections.Generic.List<GameObject> _spawned =
        new System.Collections.Generic.List<GameObject>();

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        PlayModeTestConfig.ConfigureForTests();

        _camera = new GameObject("Main Camera");
        _camera.tag = "MainCamera";
        _camera.AddComponent<Camera>();
        _camera.transform.position = new Vector3(0f, 3f, -5f);
        _camera.transform.LookAt(Vector3.zero);

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
        foreach (var go in _spawned)
            if (go != null) Object.Destroy(go);
        _spawned.Clear();

        foreach (var e in Object.FindObjectsByType<KitchenElement>())
            if (e != null) Object.Destroy(e.gameObject);
        foreach (var c in Object.FindObjectsByType<Canvas>())
            if (c != null) Object.Destroy(c.gameObject);
        foreach (var es in Object.FindObjectsByType<EventSystem>())
            if (es != null) Object.Destroy(es.gameObject);

        if (_bootstrap != null) Object.Destroy(_bootstrap);
        if (_camera != null) Object.Destroy(_camera);
        yield return null;
    }

    /// <summary>Создать набор деталей, дающий ≥3 разных issues: DRW-01 (ящик
    /// без фасада), FAC-01 (нулевой зазор), GAP-01 (почти вплотную).</summary>
    private void SpawnMixedIssues()
    {
        // DRW-01: ящик без фасада.
        var drawer = ElementFactory.CreateDrawer(DrawerType.B, 450, DrawerColor.Anthracite, 400,
            "Ящик без фасада", new Vector3(0f, 0.55f, 0f));
        _spawned.Add(drawer);

        // FAC-01: фасад с нулевым зазором слева.
        var facade = ElementFactory.CreateFacade(new Vector3Int(560, 720, 18), "Фасад без зазора",
            new Vector3(1.2f, 0.55f, 0f), 0, 2, 2, 2);
        _spawned.Add(facade);

        // GAP-01: две полки почти вплотную (~5 мм зазор по Z).
        var shelf1 = ElementFactory.CreatePart(new Vector3Int(600, 400, 18), "Полка A",
            new Vector3(2.4f, 0.8f, 0f));
        _spawned.Add(shelf1);
        var shelf2 = ElementFactory.CreatePart(new Vector3Int(600, 400, 18), "Полка B",
            new Vector3(2.4f, 0.8f, 0.023f));
        _spawned.Add(shelf2);
    }

    /// <summary>Найти панель, открыть её, дождаться прогона Analyze + RebuildRows.</summary>
    private static ErrorPanelUI OpenAndWaitForRows()
    {
        var panel = Object.FindAnyObjectByType<ErrorPanelUI>();
        Assert.IsNotNull(panel, "ErrorPanelUI должен быть создан Bootstrap-ом");
        panel!.SetVisible(true);
        return panel;
    }

    [UnityTest]
    public IEnumerator SingleClick_SelectsOneRow_AndCopiesTabSeparatedLine()
    {
        SpawnMixedIssues();
        yield return null;

        var panel = OpenAndWaitForRows();
        // Один дополнительный кадр — на случай отложенной пересборки.
        yield return null;

        // Все issues видимы по умолчанию (фильтры «Все», поиск пуст).
        var issues = SceneAnalyzer.Analyze();
        Assert.GreaterOrEqual(issues.Count, 3, "нужно минимум 3 issues для теста");

        panel.HandleRowClick(issues[0], 0, ctrl: false, shift: false);

        Assert.AreEqual(1, panel.SelectedCount, "после клика должно быть 1 выделено");
        Assert.IsTrue(panel.IsSelected(issues[0]));
        Assert.AreEqual(0, panel.SelectedVisibleIdx);

        panel.CopySelectedToClipboard();
        string clip = GUIUtility.systemCopyBuffer;

        Assert.IsFalse(string.IsNullOrEmpty(clip), "clipboard не должен быть пустым");
        // Табуляция между колонками.
        string[] cols = clip.Split('\t');
        Assert.AreEqual(4, cols.Length,
            $"одна строка = 4 колонки (Level\\tCode\\tDetail\\tMessage), получено: '{clip}'");
        Assert.AreEqual(ErrorPanelUI.LevelName(issues[0].Level), cols[0]);
        Assert.AreEqual(issues[0].Code, cols[1]);
        Assert.AreEqual(issues[0].Detail, cols[2]);
        Assert.AreEqual(issues[0].Message, cols[3]);
    }

    [UnityTest]
    public IEnumerator CtrlClick_TogglesRow()
    {
        SpawnMixedIssues();
        yield return null;

        var panel = OpenAndWaitForRows();
        yield return null;

        var issues = SceneAnalyzer.Analyze();
        Assert.GreaterOrEqual(issues.Count, 2);

        // Простой клик → 1 выделено.
        panel.HandleRowClick(issues[0], 0, ctrl: false, shift: false);
        Assert.AreEqual(1, panel.SelectedCount);

        // Ctrl+Click по той же строке → 0 (toggle off).
        panel.HandleRowClick(issues[0], 0, ctrl: true, shift: false);
        Assert.AreEqual(0, panel.SelectedCount, "Ctrl+Click по выделенной строке снимает выделение");

        // Ctrl+Click по другой → 1, anchor НЕ двигается (= не сбрасывается до −1).
        panel.HandleRowClick(issues[1], 1, ctrl: true, shift: false);
        Assert.AreEqual(1, panel.SelectedCount);
        Assert.IsTrue(panel.IsSelected(issues[1]));
    }

    [UnityTest]
    public IEnumerator ShiftClick_ExtendsRangeFromAnchor()
    {
        SpawnMixedIssues();
        yield return null;

        var panel = OpenAndWaitForRows();
        yield return null;

        var issues = SceneAnalyzer.Analyze();
        Assert.GreaterOrEqual(issues.Count, 3);

        // Якорь — клик по строке 0.
        panel.HandleRowClick(issues[0], 0, ctrl: false, shift: false);
        Assert.AreEqual(1, panel.SelectedCount);

        // Shift+Click по строке 2 — диапазон [0..2], 3 строки.
        panel.HandleRowClick(issues[2], 2, ctrl: false, shift: true);
        Assert.AreEqual(3, panel.SelectedCount,
            "Shift+Click с anchor=0 и target=2 должен выделить 3 строки");
        Assert.IsTrue(panel.IsSelected(issues[0]));
        Assert.IsTrue(panel.IsSelected(issues[1]));
        Assert.IsTrue(panel.IsSelected(issues[2]));

        // Копирование — три строки в буфере, разделены '\n'.
        panel.CopySelectedToClipboard();
        string clip = GUIUtility.systemCopyBuffer;

        int newlines = clip.Count(c => c == '\n');
        Assert.AreEqual(2, newlines,
            $"3 строки должны быть разделены 2 '\\n', получено {newlines} в '{clip}'");
        Assert.IsTrue(clip.Contains(issues[0].Code));
        Assert.IsTrue(clip.Contains(issues[1].Code));
        Assert.IsTrue(clip.Contains(issues[2].Code));
    }

    [UnityTest]
    public IEnumerator CtrlA_SelectsAll_AndCopiesAllNewlineSeparated()
    {
        SpawnMixedIssues();
        yield return null;

        var panel = OpenAndWaitForRows();
        yield return null;

        var issues = SceneAnalyzer.Analyze();
        int total = panel.VisibleIssueCount;
        Assert.GreaterOrEqual(total, 3);

        panel.SelectAll();
        Assert.AreEqual(total, panel.SelectedCount, "Ctrl+A выделяет все видимые строки");

        panel.CopySelectedToClipboard();
        string clip = GUIUtility.systemCopyBuffer;

        int newlines = clip.Count(c => c == '\n');
        Assert.AreEqual(total - 1, newlines,
            $"{total} строк должны быть разделены {total - 1} '\\n', получено {newlines}");
        // Все коды issues попали в буфер.
        foreach (var iss in issues)
            Assert.IsTrue(clip.Contains(iss.Code),
                $"в clipboard должна быть строка с кодом {iss.Code}; clip='{clip}'");
    }

    [UnityTest]
    public IEnumerator RebuildRows_PreservesSelectionThroughSearchFilter()
    {
        SpawnMixedIssues();
        yield return null;

        var panel = OpenAndWaitForRows();
        yield return null;

        var issues = SceneAnalyzer.Analyze();
        Assert.GreaterOrEqual(issues.Count, 3);

        // Выделяем первую строку простым кликом.
        panel.HandleRowClick(issues[0], 0, ctrl: false, shift: false);
        Assert.IsTrue(panel.IsSelected(issues[0]), "после клика строка выделена");

        // Сужаем поиск до строки, которой НЕТ в сцене → все строки скрыты,
        // RebuildRows отработает и SelectedVisibleIdx сбросится в −1.
        // Само выделение по issue должно остаться (иначе фильтр «съел» бы
        // работу пользователя).
var sf = panel.SearchField;
        Assert.IsNotNull(sf, "поле поиска должно быть в сцене");
        sf.text = "ZZZ_no_such_issue_999";
        yield return null;
        Assert.AreEqual(0, panel.VisibleIssueCount, "после ввода мусорного запроса 0 строк");
        Assert.IsTrue(panel.IsSelected(issues[0]),
            "выделение по issue НЕ должно сбрасываться от смены фильтра");

        // Возвращаем пустой поиск — строка снова видна, всё выделение
        // восстанавливается автоматически (issue всё ещё в _selected).
        sf.text = "";
        yield return null;
        Assert.GreaterOrEqual(panel.VisibleIssueCount, 3);
        Assert.IsTrue(panel.IsSelected(issues[0]),
            "после возврата фильтра выделение должно быть на месте");
    }
}

