using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using KitchenDesigner.Core;
using KitchenDesigner.Core.UI;

public class IntegrationPlayModeTests
{
    private GameObject _bootstrap;
    private GameObject _camera;

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        _camera = new GameObject("Main Camera");
        _camera.tag = "MainCamera";
        _camera.AddComponent<Camera>();
        _camera.transform.position = new Vector3(0f, 3f, -5f);
        _camera.transform.LookAt(Vector3.zero);

        _bootstrap = new GameObject("Bootstrap");
        _bootstrap.AddComponent<Bootstrap>();

        // Awake + Start + кадр на инициализацию UI/менеджеров.
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

        foreach (var es in Object.FindObjectsByType<EventSystem>())
            if (es != null) Object.Destroy(es.gameObject);

        if (_bootstrap != null) Object.Destroy(_bootstrap);
        if (_camera != null) Object.Destroy(_camera);
        yield return null;
    }

    private static int BoardCount()
    {
        int count = 0;
        foreach (var e in Object.FindObjectsByType<KitchenElement>())
            if (e != null && e.GetComponent<BasePlate>() == null) count++;
        return count;
    }

    [UnityTest]
    public IEnumerator Bootstrap_CreatesCoreManagersAndUI()
    {
        Assert.IsNotNull(Object.FindAnyObjectByType<BasePlate>(), "BasePlate должен быть создан");
        Assert.IsNotNull(SelectionManager.Instance, "SelectionManager.Instance");
        Assert.IsNotNull(ElementHighlighter.Instance, "ElementHighlighter.Instance");
        Assert.IsNotNull(UIManager.Instance, "UIManager.Instance");
        Assert.IsNotNull(UIManager.Instance.Canvas, "Canvas должен быть создан");
        Assert.IsNotNull(Object.FindAnyObjectByType<EventSystem>(), "EventSystem");
        yield return null;
    }

    [UnityTest]
    public IEnumerator SpawnPreset_AddsBoardWithPresetDimensions()
    {
        int before = BoardCount();
        UIManager.Instance.SpawnPreset(0);
        yield return null;

        Assert.AreEqual(before + 1, BoardCount(), "должна добавиться одна доска");

        KitchenElement spawned = null;
        foreach (var e in Object.FindObjectsByType<KitchenElement>())
            if (e != null && e.GetComponent<BasePlate>() == null) spawned = e;

        Assert.IsNotNull(spawned);
        Assert.AreEqual(AppConstants.PRESET_DIMENSIONS_MM[0], spawned.DimensionsMM);
    }

    [UnityTest]
    public IEnumerator SaveProject_ThenLoadProject_RestoresBoards()
    {
        UIManager.Instance.SpawnPreset(0);
        UIManager.Instance.SpawnPreset(1);
        UIManager.Instance.SpawnPreset(2);
        yield return null;

        Assert.AreEqual(3, BoardCount());

        const string name = "pm_roundtrip";
        Assert.IsTrue(SaveLoadManager.SaveProject(name));

        Assert.IsTrue(SaveLoadManager.LoadProject(name));
        yield return null; // дождаться Destroy старых досок

        Assert.AreEqual(3, BoardCount(), "после загрузки должно быть 3 доски");

        File.Delete(SaveLoadManager.PathForName(name));
    }

    [UnityTest]
    public IEnumerator SelectionManager_SelectAndDeselect()
    {
        UIManager.Instance.SpawnPreset(0);
        yield return null;

        KitchenElement board = null;
        foreach (var e in Object.FindObjectsByType<KitchenElement>())
            if (e != null && e.GetComponent<BasePlate>() == null) board = e;

        SelectionManager.Instance.Select(board);
        Assert.AreEqual(board, SelectionManager.Instance.Selected);

        SelectionManager.Instance.Deselect();
        Assert.IsNull(SelectionManager.Instance.Selected);
    }
}
