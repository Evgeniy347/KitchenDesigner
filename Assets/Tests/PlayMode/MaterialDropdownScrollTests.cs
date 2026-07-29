using System.Collections;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;
using KitchenDesigner.Core;
using KitchenDesigner.Core.UI;

/// <summary>Раскрытый список декоров: в окно помещается 7 пунктов из трёх
/// десятков, поэтому всё остальное доступно только прокруткой. Проверка живёт
/// в PlayMode: раскрытие списка — рантайм TMP (Awake, твины, корутины), в
/// EditMode оно не работает.</summary>
public class MaterialDropdownScrollTests
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
        foreach (var es in Object.FindObjectsByType<EventSystem>())
            if (es != null) Object.Destroy(es.gameObject);
        if (_bootstrap != null) Object.Destroy(_bootstrap);
        if (_camera != null) Object.Destroy(_camera);
        yield return null;
    }

    [UnityTest]
    public IEnumerator MaterialList_ScrollsWithWheel()
    {
        TMP_Dropdown dd = null!;
        yield return OpenMaterialList(d => dd = d);

        var list = dd.transform.Find("Dropdown List");
        Assert.IsNotNull(list, "список не раскрылся");

        var scroll = list!.GetComponent<ScrollRect>();
        Assert.IsNotNull(scroll, "у раскрытого списка нет ScrollRect");
        Assert.IsNotNull(scroll!.content, "ScrollRect не знает своего контента");
        Assert.IsNotNull(scroll!.viewport, "ScrollRect не знает своего окна");

        float viewportH = scroll!.viewport.rect.height;
        float contentH = scroll!.content.rect.height;
        Assert.Greater(contentH, viewportH,
            $"прокручивать нечего: контент {contentH:F0} px при окне {viewportH:F0} px");

        float before = scroll!.content.anchoredPosition.y;
        scroll!.OnScroll(new PointerEventData(EventSystem.current)
        {
            scrollDelta = new Vector2(0f, -3f),
        });
        yield return null;

        Assert.Greater(scroll!.content.anchoredPosition.y, before,
            "BUG: колесо мыши не прокручивает список декоров");
    }

    /// <summary>Прокрутка бесполезна, если колесо до списка не доходит: событие
    /// уходит тому, на что показывает курсор, и ScrollRect обязан быть первым
    /// обработчиком прокрутки вверх по иерархии от пункта.</summary>
    [UnityTest]
    public IEnumerator WheelOverItem_ReachesTheScrollRect()
    {
        TMP_Dropdown dd = null!;
        yield return OpenMaterialList(d => dd = d);

        var list = dd.transform.Find("Dropdown List");
        Assert.IsNotNull(list);
        var item = list!.GetComponentInChildren<Toggle>(includeInactive: false);
        Assert.IsNotNull(item, "в раскрытом списке нет ни одного пункта");

        var handler = ExecuteEvents.GetEventHandler<IScrollHandler>(item!.gameObject);
        Assert.AreSame(list!.gameObject, handler,
            "BUG: колесо над пунктом уходит мимо списка");
    }

    [UnityTest]
    public IEnumerator OpenList_ShowsSevenItems_AndHasMoreBelow()
    {
        TMP_Dropdown dd = null!;
        yield return OpenMaterialList(d => dd = d);

        var list = dd.transform.Find("Dropdown List");
        var scroll = list!.GetComponent<ScrollRect>();
        float itemH = scroll!.content.GetComponentInChildren<Toggle>()
            .GetComponent<RectTransform>().rect.height;

        Assert.AreEqual(UIStyle.DropdownItemH, itemH, 0.5f,
            "пункт каталога декоров должен быть однострочным");
        Assert.AreEqual(7, Mathf.RoundToInt(scroll!.viewport.rect.height / itemH),
            "без прокрутки видно семь декоров");
        Assert.Greater(dd.options.Count, 7, "остальные декоры доступны прокруткой");
    }

    /// <summary>Открыть меню свойств детали и раскрыть в нём список «Текстура».
    /// Между открытием меню и раскрытием списка нужен кадр: TMP_Dropdown
    /// заводит свои твины в Awake, а он у только что включённой панели
    /// отрабатывает не раньше следующего кадра.</summary>
    private static IEnumerator OpenMaterialList(System.Action<TMP_Dropdown> onReady)
    {
        UIManager.Instance!.SpawnPreset(0);
        KitchenElement? board = null;
        foreach (var e in Object.FindObjectsByType<KitchenElement>())
            if (e != null && e.GetComponent<BasePlate>() == null) board = e;
        Assert.IsNotNull(board, "деталь не создалась");

        ContextMenuUI.Instance!.Open(board!);
        yield return null;

        TMP_Dropdown? material = null;
        foreach (var d in Object.FindObjectsByType<TMP_Dropdown>(
                     FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (d.name == "CtxMaterial") material = d;
        Assert.IsNotNull(material, "список «Текстура» не найден");
        Assert.IsTrue(material!.gameObject.activeInHierarchy,
            "строка «Текстура» должна быть видна у обычной детали");

        material!.Show();
        yield return null;
        onReady(material!);
    }
}
