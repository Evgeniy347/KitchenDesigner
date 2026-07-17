using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using KitchenDesigner.Core;
using KitchenDesigner.Core.UI;

public class SelectionContextMenuTests
{
    private GameObject? _bootstrap;
    private GameObject? _camera;

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
        if (System.IO.File.Exists(autoPath)) System.IO.File.Delete(autoPath);

        _bootstrap = new GameObject("Bootstrap");
        _bootstrap.AddComponent<Bootstrap>();

        yield return null;
        yield return null;
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        foreach (var e in Object.FindObjectsByType<KitchenElement>())
            if (e != null) Object.Destroy(e.gameObject);

        foreach (var c in Object.FindObjectsByType<Canvas>())
            if (c != null) Object.Destroy(c.gameObject);

        foreach (var es in Object.FindObjectsByType<UnityEngine.EventSystems.EventSystem>())
            if (es != null) Object.Destroy(es.gameObject);

        if (_bootstrap != null) Object.Destroy(_bootstrap);
        if (_camera != null) Object.Destroy(_camera);
        yield return null;
    }

    private static KitchenElement? SpawnAndGetBoard()
    {
        int before = 0;
        foreach (var e in Object.FindObjectsByType<KitchenElement>())
            if (e != null && e.GetComponent<BasePlate>() == null) before++;

        UIManager.Instance!.SpawnPreset(0);

        KitchenElement? board = null;
        foreach (var e in Object.FindObjectsByType<KitchenElement>())
            if (e != null && e.GetComponent<BasePlate>() == null && !IsAlreadyCounted(e, before))
                board = e;

        return board;
    }

    private static bool IsAlreadyCounted(KitchenElement e, int skipCount)
    {
        int idx = 0;
        foreach (var el in Object.FindObjectsByType<KitchenElement>())
        {
            if (el == null || el.GetComponent<BasePlate>() != null) continue;
            if (el == e) return idx < skipCount;
            idx++;
        }
        return false;
    }

    private static GameObject? ContextMenuRoot()
    {
        var go = GameObject.Find("ContextMenu");
        return go;
    }

    private static bool IsContextMenuVisible()
    {
        var root = ContextMenuRoot();
        return root != null && root.activeSelf;
    }

    private static bool IsContextMenuTitleVisible()
    {
        var titleGo = GameObject.Find("CtxTitle");
        return titleGo != null && titleGo.activeInHierarchy;
    }

    // ── Пункт 1: ЛКМ (Select) не открывает контекстное меню ─────────────
    [UnityTest]
    public IEnumerator Select_DoesNotOpen_ContextMenu()
    {
        var board = SpawnAndGetBoard();
        Assert.IsNotNull(board, "должен появиться spawn-элемент");
        yield return null;

        Assert.IsFalse(IsContextMenuVisible(),
            "контекстное меню должно быть закрыто до выделения");

        SelectionManager.Instance!.Select(board!);
        yield return null;

        Assert.AreEqual(board, SelectionManager.Instance.Selected,
            "элемент должен быть выделен");
        Assert.IsFalse(IsContextMenuVisible(),
            "ЛКМ (Select) НЕ должен открывать контекстное меню");
    }

    // ── Пункт 2: ПКМ (OpenContextMenu) открывает контекстное меню ────────
    [UnityTest]
    public IEnumerator OpenContextMenu_Opens_ContextMenu()
    {
        var board = SpawnAndGetBoard();
        Assert.IsNotNull(board);
        yield return null;

        Assert.IsFalse(IsContextMenuVisible());

        UIManager.Instance!.OpenContextMenu(board!);
        yield return null;

        Assert.IsTrue(IsContextMenuVisible(),
            "ПКМ (OpenContextMenu) должен открывать контекстное меню");
        Assert.AreEqual(board, SelectionManager.Instance!.Selected,
            "элемент должен быть выделен при открытии меню");
    }

    // ── Пункт 3: меню открыто → Select другого элемента обновляет меню ───
    [UnityTest]
    public IEnumerator ContextMenu_Open_ThenSelectAnother_UpdatesMenu()
    {
        var board1 = SpawnAndGetBoard();
        Assert.IsNotNull(board1);
        yield return null;

        // Создаём board2 без SpawnPreset (чтобы избежать автовыделения).
        var pos = board1!.transform.position + new Vector3(1f, 0f, 0f);
        var go = ElementFactory.CreatePart(
            AppConstants.PRESET_DIMENSIONS_MM[0], "TestBoard2", pos);
        var board2 = go.GetComponent<KitchenElement>();
        Assert.IsNotNull(board2);
        yield return null;

        UIManager.Instance!.OpenContextMenu(board1);
        yield return null;
        Assert.IsTrue(IsContextMenuVisible(), "меню должно быть открыто");

        SelectionManager.Instance!.Select(board2!);
        yield return null;

        Assert.IsTrue(IsContextMenuVisible(),
            "меню должно остаться открытым и обновиться на board2");
        Assert.AreEqual(board2, SelectionManager.Instance.Selected);
        Assert.IsTrue(IsContextMenuTitleVisible(),
            "заголовок меню должен быть виден после обновления");
    }

    // ── Пункт 3b: same-frame: Select() → DeselectAll() + Select() не закрывает меню ──
    [UnityTest]
    public IEnumerator ContextMenu_Select_SameFrame_KeepsMenu()
    {
        var board1 = SpawnAndGetBoard();
        Assert.IsNotNull(board1);
        yield return null;

        var pos = board1!.transform.position + new Vector3(1f, 0f, 0f);
        var go = ElementFactory.CreatePart(
            AppConstants.PRESET_DIMENSIONS_MM[0], "TestBoard2", pos);
        var board2 = go.GetComponent<KitchenElement>();
        Assert.IsNotNull(board2);
        yield return null;

        UIManager.Instance!.OpenContextMenu(board1);
        yield return null;
        Assert.IsTrue(IsContextMenuVisible());

        // Select() внутри вызывает DeselectAll() → OnSelectionChanged(null),
        // затем OnSelectionChanged(board2) — всё в одном кадре.
        // Меню НЕ должно закрыться.
        SelectionManager.Instance!.Select(board2!);
        // Без yield — проверяем в том же кадре, до отложенного закрытия.
        Assert.IsTrue(IsContextMenuVisible(),
            "меню должно быть видно в том же кадре после Select (отложенное закрытие)");

        yield return null;
        Assert.IsTrue(IsContextMenuVisible(),
            "меню должно остаться открытым и в следующем кадре");
        Assert.AreEqual(board2, SelectionManager.Instance.Selected);
    }

    // ── Пункт 4: меню открыто → Deselect (клик в пустоту) закрывает меню ─
    [UnityTest]
    public IEnumerator ContextMenu_Open_ThenDeselect_ClosesMenu()
    {
        var board = SpawnAndGetBoard();
        Assert.IsNotNull(board);
        yield return null;

        UIManager.Instance!.OpenContextMenu(board!);
        yield return null;
        Assert.IsTrue(IsContextMenuVisible());

        SelectionManager.Instance!.DeselectAll();
        yield return null;

        Assert.IsFalse(IsContextMenuVisible(),
            "клик в пустоту (DeselectAll) должен закрывать меню");
        Assert.IsNull(SelectionManager.Instance.Selected);
    }

    // ── Повторный ПКМ на том же элементе не ломает меню ──────────────────
    [UnityTest]
    public IEnumerator OpenContextMenu_Twice_SameElement_DoesNotThrow()
    {
        var board = SpawnAndGetBoard();
        Assert.IsNotNull(board);
        yield return null;

        UIManager.Instance!.OpenContextMenu(board!);
        yield return null;
        Assert.IsTrue(IsContextMenuVisible());

        UIManager.Instance.OpenContextMenu(board!);
        yield return null;
        Assert.IsTrue(IsContextMenuVisible());
    }

    // ── ЛКМ по уже выделенному элементу не открывает меню ────────────────
    [UnityTest]
    public IEnumerator Select_AlreadySelected_DoesNotOpen_ContextMenu()
    {
        var board = SpawnAndGetBoard();
        Assert.IsNotNull(board);
        yield return null;

        SelectionManager.Instance!.Select(board!);
        yield return null;
        Assert.IsFalse(IsContextMenuVisible(), "меню закрыто после первого Select");

        SelectionManager.Instance.Select(board!);
        yield return null;

        Assert.IsFalse(IsContextMenuVisible(),
            "повторный Select не должен открывать меню");
    }
}
