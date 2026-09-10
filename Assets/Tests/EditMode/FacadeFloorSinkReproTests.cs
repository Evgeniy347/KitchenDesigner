using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;
using KitchenDesigner.Core.UI;

/// <summary>Каждый созданный фасад оказывался на 2 мм В ПОЛУ, и коробка эта
/// не декоративная: ValidationCore судит перекрытие ровно по ней, поэтому
/// свежесозданный фасад немедленно получал Overlap с опорной плитой и красился
/// розовым.
///
/// Механизм. Обе постановки — ElementSpawner при рождении и PlacementController
/// каждый кадр, пока элемент висит на курсоре, — считали центр как половину
/// ФИЗИЧЕСКОЙ высоты. А GappedBox.CornerUnits не сжимает коробку зазорами, а
/// РАСШИРЯЕТ её: minY = -h/2 - gapBottom. Меш при этом не двигается. Значит низ
/// коробки уходил ниже физического низа ровно на нижний притвор — 2 мм у фасада
/// и сборного фасада, 1 мм у ХДФ-панели.
///
/// Тот же дефект уже ловили с другой стороны: изометрические кадры фасадов
/// месяцами показывали не декор, а тинт нарушения, и в фикстуре его закрыли
/// подъёмом по коробке валидации (IsoScreenshotTests.StandOnFloor). Продакшен
/// остался считать по-старому — два описания одной величины, и это второй тест
/// про одно и то же число. Поэтому здесь проверка не формулы, а РЕЗУЛЬТАТА:
/// низ коробки, которую читает валидатор, обязан лечь на ноль. Ровно это
/// утверждает и фикстура, поэтому сойтись они могут теперь только вместе.
///
/// И каждая проверка идёт в паре с элементом БЕЗ зазоров. Без этой пары
/// починку нельзя отличить от сдвига всего на 2 мм вверх: доска, поднятая на
/// притвор фасада, повисла бы в воздухе, а тесты выше остались бы зелёными.</summary>
public class FacadeFloorSinkReproTests
{
    private const float ToleranceMm = 1e-3f;

    private readonly List<GameObject> _spawned = new List<GameObject>();
    private PlacementController _placement = null!;
    private bool _gridEnabledBefore;

    [SetUp]
    public void SetUp()
    {
        _gridEnabledBefore = KitchenSettings.Instance.GridEnabled;
        KitchenSettings.Instance.GridEnabled = false;

        PartRegistry.Clear();
        CommandStack.Clear();

        var camGo = new GameObject("Main Camera");
        camGo.tag = "MainCamera";
        camGo.AddComponent<Camera>();
        camGo.transform.position = new Vector3(0f, 3f, -5f);
        camGo.transform.LookAt(Vector3.zero);
        _spawned.Add(camGo);

        var host = new GameObject("Placement");
        _spawned.Add(host);
        _placement = host.AddComponent<PlacementController>();
    }

    [TearDown]
    public void TearDown()
    {
        if (KitchenSettings.Instance != null)
            KitchenSettings.Instance.GridEnabled = _gridEnabledBefore;

        foreach (var go in _spawned) if (go != null) Object.DestroyImmediate(go);
        _spawned.Clear();
        foreach (var el in new List<KitchenElement>(PartRegistry.GetAll()))
            if (el != null) Object.DestroyImmediate(el.gameObject);
        PartRegistry.Clear();
        CommandStack.Clear();
        ElementFactory.ClearPools();
    }

    // ── На курсоре: PlacementController перебивает Y каждый кадр ──

    [Test]
    public void AFacadeOnTheCursor_StandsOnTheFloor_NotInIt()
    {
        var go = ElementFactory.CreateFacade(new Vector3Int(350, 716, 18), "Фасад",
            new Vector3(0f, 2f, 0f));
        _spawned.Add(go);
        var facade = go.GetComponent<FacadeElement>();
        Assert.IsNotNull(facade, "фабрика фасада обязана вернуть фасад");
        Assert.AreEqual(FacadeElement.DEFAULT_GAP_MM, facade!.GapBottom,
            "проверять утопание бессмысленно на фасаде без нижнего притвора: "
            + "с нулевым зазором коробка совпадает с мешем и сломанный код зелен");

        _placement.Begin(facade!);

        AssertSitsOnFloor(facade!, "фасад на курсоре");
    }

    [Test]
    public void AnAssembledFacadeOnTheCursor_StandsOnTheFloor_NotInIt()
    {
        var go = ElementFactory.CreateAssembledFacade(new Vector3Int(450, 716, 22),
            "Сборный", new Vector3(0f, 2f, 0f), AssembledFill.Blind);
        _spawned.Add(go);
        var facade = go.GetComponent<AssembledFacadeElement>();
        Assert.IsNotNull(facade, "фабрика сборного фасада обязана вернуть сборный фасад");
        Assert.AreEqual(FacadeElement.DEFAULT_GAP_MM, facade!.GapBottom,
            "сборный фасад получил стандартные притворы вместе с обычным — до этого его "
            + "коробка совпадала с физической, и тонуть ему было нечем");

        _placement.Begin(facade!);

        AssertSitsOnFloor(facade!, "сборный фасад на курсоре");
    }

    [Test]
    public void APanelOnTheCursor_StandsOnTheFloor_NotInIt()
    {
        var go = ElementFactory.CreatePanel(new Vector3Int(500, 716, 4), "Задник",
            new Vector3(0f, 2f, 0f));
        _spawned.Add(go);
        var panel = go.GetComponent<PanelElement>();
        Assert.IsNotNull(panel, "фабрика панели обязана вернуть панель");
        Assert.AreEqual(PanelElement.DEFAULT_GAP_MM, panel!.GapBottom,
            "у панели зазор СВОЙ — 1 мм против 2 у фасада. Проверка на двух разных числах "
            + "не пройдёт на подъёме, зашитом константой фасада");

        _placement.Begin(panel!);

        AssertSitsOnFloor(panel!, "ХДФ-панель на курсоре");
    }

    [Test]
    public void APlainBoardOnTheCursor_StandsExactlyOnZero_NotLiftedByAFacadeGap()
    {
        var go = ElementFactory.CreatePart(new Vector3Int(600, 18, 400), "Доска",
            new Vector3(0f, 2f, 0f));
        _spawned.Add(go);
        var board = go.GetComponent<KitchenElement>();
        Assert.AreEqual(0, board.Gaps.Bottom,
            "у обычной доски зазоров нет — иначе это не парный контроль, а третий случай "
            + "того же самого");

        _placement.Begin(board);

        AssertSitsOnFloor(board, "доска на курсоре");
        Assert.AreEqual(AppConstants.HalfHeightUnits(board.DimensionsMM.y),
            go.transform.position.y, Tolerance.EpsilonUnits,
            "и центр доски остался на половине высоты: подъём на притвор фасада поднял бы "
            + "ВСЮ мебель, а проверки выше этого не заметили бы");
    }

    // ── При рождении: ElementSpawner, когда постановки на курсор нет ──

    [Test]
    public void AFacadeFromTheSidebar_StandsOnTheFloor_NotInIt()
    {
        var facade = SpawnFromSidebar(
            new SidebarCatalog.Item("Фасад", new Vector3Int(350, 716, 18), SidebarItemKind.Facade));

        Assert.IsInstanceOf<FacadeElement>(facade, "кнопка фасада обязана родить фасад");
        Assert.AreEqual(FacadeElement.DEFAULT_GAP_MM, ((FacadeElement)facade).GapBottom,
            "зазоры сайдбар больше не передаёт: их единственный источник — "
            + "FacadeElement.DEFAULT_GAP_MM через умолчание фабрики. Ноль здесь значит, что "
            + "по дороге зазор потеряли, и проверка утопания ниже стала бы зелёной на пустом");
        AssertSitsOnFloor(facade, "фасад из сайдбара");
    }

    [Test]
    public void APlainBoardFromTheSidebar_StandsExactlyOnZero_NotLiftedByAFacadeGap()
    {
        var board = SpawnFromSidebar(
            new SidebarCatalog.Item("Доска", new Vector3Int(600, 18, 400), SidebarItemKind.Board));

        Assert.AreEqual(0, board.Gaps.Bottom, "у обычной доски зазоров нет");
        AssertSitsOnFloor(board, "доска из сайдбара");
        Assert.AreEqual(AppConstants.HalfHeightUnits(board.DimensionsMM.y),
            board.transform.position.y, Tolerance.EpsilonUnits,
            "центр доски — ровно половина высоты, как и до починки");
    }

    /// <summary>Постановка без PlacementController: так рождается элемент в
    /// прогоне без сцены и так же он ложится, если постановка на курсор
    /// отключена. Сетка выключена в SetUp: с включённой центр округляется к её
    /// шагу и утопание тонет в этом округлении.</summary>
    private KitchenElement SpawnFromSidebar(SidebarCatalog.Item item)
    {
        var spawner = new ElementSpawner(() => Vector3.zero, () => null);
        spawner.Spawn(item);

        var all = PartRegistry.GetAll();
        Assert.AreEqual(1, all.Count,
            "спаунер обязан завести ровно один элемент — иначе меряем не тот");
        var element = all[0];
        Assert.IsNotNull(element, "спаунер обязан завести элемент, а не пустоту");
        _spawned.Add(element.gameObject);
        return element;
    }

    /// <summary>Мера — низ ТОЙ коробки, по которой судит валидатор
    /// (GetVertices), а не половина габарита: выписанное отдельно число
    /// разошлось бы с зазорами при первой же их правке.</summary>
    private static void AssertSitsOnFloor(KitchenElement element, string who)
    {
        float minY = float.MaxValue;
        foreach (var v in element.GetVertices()) minY = Mathf.Min(minY, v.y);
        float minMm = minY / AppConstants.MM_TO_UNITS;

        Assert.AreEqual(0f, minMm, ToleranceMm,
            $"{who}: низ коробки ВАЛИДАЦИИ ушёл на {-minMm:0.###} мм ниже пола. "
            + "Ровно по этой коробке ValidationCore считает перекрытие, поэтому элемент "
            + "получает Overlap с опорной плитой в момент создания");
    }
}
