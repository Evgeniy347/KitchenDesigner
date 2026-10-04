using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using KitchenDesigner.Core;
using KitchenDesigner.Core.UI;

/// <summary>Вкладка «Строительство» — место, где живут дефолты дома: регион,
/// высота этажа, кладка со швом и запасом, грунт, бетон, подушка. Проверяется
/// три решения: у каждой строки есть подсказка «i» (docs/UI-GUIDELINES.md §13),
/// глубина промерзания — ТОЛЬКО ЧТЕНИЕ и показывает то, что даёт норматив, а там,
/// где норматив числа не даёт, — прочерк, и дропдауны перечисляют ровно те
/// варианты, которые знает модель.</summary>
public class SettingsConstructionTabTests
{
    private static readonly string PagePath = KitchenDesigner.Tests.SettingsWindowPaths.Page("construction") + "/";

    private Canvas? _canvas;
    private SettingsPanelUI? _ui;
    private KitchenSettings? _saved;

    [SetUp]
    public void Setup()
    {
        _saved = new KitchenSettings();
        _saved.ApplyFrom(KitchenSettings.Instance.ToData());

        var go = new GameObject("TestCanvas");
        _canvas = go.AddComponent<Canvas>();
        _canvas!.renderMode = RenderMode.ScreenSpaceOverlay;
        go.AddComponent<CanvasScaler>();
        go.AddComponent<GraphicRaycaster>();

        if (Object.FindAnyObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
        {
            var es = new GameObject("EventSystem");
            es.AddComponent<UnityEngine.EventSystems.EventSystem>();
            es.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
        }

        _ui = _canvas!.gameObject.AddComponent<SettingsPanelUI>();
        _ui!.Build(_canvas!.transform);
        _ui.SetVisible(true);
    }

    [TearDown]
    public void TearDown()
    {
        if (_saved != null) KitchenSettings.Instance.ApplyFrom(_saved.ToData());
        EditModeManager.Reset();
        if (_canvas != null) Object.DestroyImmediate(_canvas!.gameObject);
        var es = Object.FindAnyObjectByType<UnityEngine.EventSystems.EventSystem>();
        if (es != null) Object.DestroyImmediate(es.gameObject);
        foreach (var e in Object.FindObjectsByType<KitchenElement>(FindObjectsSortMode.None))
            if (e != null) Object.DestroyImmediate(e.gameObject);
        PartRegistry.Clear();
        LevelRegistry.Reset();
    }

    private Transform Page()
    {
        var page = _canvas!.transform.Find(PagePath.TrimEnd('/'));
        Assert.IsNotNull(page, "страница вкладки «Строительство» обязана существовать");
        return page!;
    }

    private Transform Row(string name)
    {
        var row = Page().Find(name);
        Assert.IsNotNull(row, "строка «" + name + "» обязана быть на вкладке");
        return row!;
    }

    [Test]
    public void TheTab_CarriesEveryProjectDefaultOfTheHouse()
    {
        Assert.IsNotNull(Row("Row_" + SettingsConstructionTab.RegionId));
        Assert.IsNotNull(Row("Row_" + SettingsConstructionTab.FrostDepthId));
        Assert.IsNotNull(Row("Row_" + SettingsConstructionTab.SoilId));
        Assert.IsNotNull(Row("Row_Высота этажа"));
        Assert.IsNotNull(Row("Row_" + SettingsConstructionTab.NeighbourLevelsId));
        Assert.IsNotNull(Row("Row_" + SettingsConstructionTab.MasonryId));
        Assert.IsNotNull(Row("Row_Шов"));
        Assert.IsNotNull(Row("Row_Запас"));
        Assert.IsNotNull(Row("Row_" + SettingsConstructionTab.ConcreteId));
        Assert.IsNotNull(Row("Row_Подушка: песок"));
        Assert.IsNotNull(Row("Row_Подушка: щебень"));
        Assert.IsNotNull(Row("Row_Трамбовка"));
    }

    [Test]
    public void EveryRowOfTheTab_CarriesItsHintBadge()
    {
        var badges = Page().GetComponentsInChildren<HintBadge>(true);

        Assert.AreEqual(13, badges.Length,
            "у каждого контрола вкладки — своя «i»: тринадцать строк (S2 добавил «Толщина "
            + "плиты» перекрытия), тринадцать значков (docs/UI-GUIDELINES.md §13). Найдено: "
            + badges.Length);
    }

    /// <summary>Глубина промерзания не вводится руками: она выводится из региона И
    /// грунта по СП 22.13330.2016 (5.5.3) — вывод формулы разобран в
    /// FrostDepthTests.</summary>
    [Test]
    public void TheFrostDepth_IsReadOnly_BecauseItIsDerivedNotEntered()
    {
        var row = Row("Row_" + SettingsConstructionTab.FrostDepthId);

        Assert.IsEmpty(row.GetComponentsInChildren<TMP_InputField>(true),
            "глубина промерзания не вводится руками — она выводится из региона и грунта");
        Assert.IsEmpty(row.GetComponentsInChildren<TMP_Dropdown>(true),
            "и не выбирается из списка");
    }

    /// <summary>Сторож читает ПОСТРОЕННОЕ поле, а не функцию: таблица и формула
    /// могут быть верными, а поле — так и не подключённым к ним (agents/TEST-DESIGN.md,
    /// «У сторожа, который читает ИСХОДНИК, обязана быть пара, читающая ПОСТРОЕННЫЙ
    /// продукт»).</summary>
    [Test]
    public void TheFrostDepth_ShowsTheNormativeDepthOfTheChosenRegionAndSoil()
    {
        KitchenSettings.Instance.ConstructionRegion = ConstructionRegion.Centre;
        KitchenSettings.Instance.ConstructionSoil = SoilKind.Loam;
        _ui!.SetVisible(true);

        Assert.AreEqual("1079 мм", FrostDepthText(),
            "Москва, суглинок: d_fn = 0,23·√22,0 = 1,079 м. Миллиметры — единица приложения "
            + "(conventions/UNITS-AND-FILES.md)");
    }

    [Test]
    public void TheFrostDepth_FollowsTheSoilDropdown_NotOnlyTheRegion()
    {
        KitchenSettings.Instance.ConstructionRegion = ConstructionRegion.Centre;
        KitchenSettings.Instance.ConstructionSoil = SoilKind.Loam;
        _ui!.SetVisible(true);
        string onLoam = FrostDepthText();

        Row("Row_" + SettingsConstructionTab.SoilId)
            .GetComponentInChildren<TMP_Dropdown>(true).value = (int)SoilKind.Sand;

        Assert.AreEqual("1407 мм", FrostDepthText(),
            "тот же регион на песке промерзает глубже: 0,30·√22,0 = 1,407 м. Было: " + onLoam
            + ". Поле, которое слушает только регион, показало бы прежнее число");
    }

    [Test]
    public void TheFrostDepth_FollowsTheRegionDropdown()
    {
        KitchenSettings.Instance.ConstructionRegion = ConstructionRegion.Centre;
        KitchenSettings.Instance.ConstructionSoil = SoilKind.Loam;
        _ui!.SetVisible(true);

        Row("Row_" + SettingsConstructionTab.RegionId)
            .GetComponentInChildren<TMP_Dropdown>(true).value = (int)ConstructionRegion.Siberia;

        Assert.AreEqual("1827 мм", FrostDepthText(),
            "Новосибирск, суглинок: 0,23·√63,1 = 1,827 м. Поле обязано обновляться сразу, "
            + "а не при следующем открытии окна");
    }

    /// <summary>Прочерк остался, но теперь он означает названную вещь: норматив
    /// не даёт d0 для торфа, и выдумывать его нельзя.</summary>
    [Test]
    public void TheFrostDepth_ShowsADash_WhereTheNormGivesNoNumber()
    {
        KitchenSettings.Instance.ConstructionRegion = ConstructionRegion.Centre;
        KitchenSettings.Instance.ConstructionSoil = SoilKind.Peat;
        _ui!.SetVisible(true);

        Assert.AreEqual(SettingsConstructionTab.FrostDepthUnknown, FrostDepthText(),
            "для торфа d0 в СП 22.13330.2016 (5.5.3) не назван — прочерк, а не чужое число");
    }

    private string FrostDepthText()
    {
        var value = Row("Row_" + SettingsConstructionTab.FrostDepthId)
            .Find("Val_" + SettingsConstructionTab.FrostDepthId);
        Assert.IsNotNull(value, "значение обязано быть чем-то показано");
        return value!.GetComponent<TextMeshProUGUI>().text.Replace("​", "");
    }

    [Test]
    public void TheDropdowns_OfferExactlyWhatTheModelKnows()
    {
        var region = Row("Row_" + SettingsConstructionTab.RegionId)
            .GetComponentInChildren<TMP_Dropdown>(true);
        var masonry = Row("Row_" + SettingsConstructionTab.MasonryId)
            .GetComponentInChildren<TMP_Dropdown>(true);
        var soil = Row("Row_" + SettingsConstructionTab.SoilId)
            .GetComponentInChildren<TMP_Dropdown>(true);
        var concrete = Row("Row_" + SettingsConstructionTab.ConcreteId)
            .GetComponentInChildren<TMP_Dropdown>(true);

        CollectionAssert.AreEqual(ConstructionRegionTitles.All,
            region.options.Select(o => o.text).ToArray());
        CollectionAssert.AreEqual(SettingsConstructionTab.MasonryTitles(),
            masonry.options.Select(o => o.text).ToList(),
            "список форматов кладки берётся из MasonryUnit.Table, второго списка нет");
        CollectionAssert.AreEqual(SoilKindTitles.All, soil.options.Select(o => o.text).ToArray());
        CollectionAssert.AreEqual(ConcreteGradeTitles.All,
            concrete.options.Select(o => o.text).ToArray());
    }

    [Test]
    public void OutOfTheBox_TheTabShowsTheDefaultsDecidedForTheUser()
    {
        KitchenSettings.Instance.ResetConstruction();
        _ui!.SetVisible(true);

        var region = Row("Row_" + SettingsConstructionTab.RegionId)
            .GetComponentInChildren<TMP_Dropdown>(true);
        var masonry = Row("Row_" + SettingsConstructionTab.MasonryId)
            .GetComponentInChildren<TMP_Dropdown>(true);
        var soil = Row("Row_" + SettingsConstructionTab.SoilId)
            .GetComponentInChildren<TMP_Dropdown>(true);
        var concrete = Row("Row_" + SettingsConstructionTab.ConcreteId)
            .GetComponentInChildren<TMP_Dropdown>(true);

        Assert.AreEqual("Урал", region.options[region.value].text, "регион по умолчанию — Урал");
        Assert.AreEqual("Кирпич 250×120×65", masonry.options[masonry.value].text);
        Assert.AreEqual("Неизвестно", soil.options[soil.value].text,
            "неизвестный грунт — худший случай, и он выбран намеренно");
        Assert.AreEqual("B20", concrete.options[concrete.value].text);

        Assert.AreEqual("3000", FieldText("Row_Высота этажа"));
        Assert.AreEqual("10", FieldText("Row_Шов"));
        Assert.AreEqual("5", FieldText("Row_Запас"));
        Assert.AreEqual("100", FieldText("Row_Подушка: песок"));
        Assert.AreEqual("100", FieldText("Row_Подушка: щебень"));
        Assert.IsTrue(Row("Row_Трамбовка").GetComponentInChildren<Toggle>(true).isOn,
            "трамбовка включена по умолчанию");
    }

    private string FieldText(string rowName) =>
        Row(rowName).GetComponentInChildren<TMP_InputField>(true).text;

    /// <summary>M2: до фикса менявшая режим лямбда только писала в
    /// <see cref="KitchenSettings.NeighbourLevels"/> — приглушение соседнего этажа
    /// появлялось не сразу, а при следующем случайном <c>RefreshHighlights</c> (клик,
    /// правка). Дропдаун обязан перекрашивать сцену тем же кадром.</summary>
    [Test]
    public void NeighbourLevelsDropdown_SwitchingToDim_RepaintsWithoutAnExplicitRefresh()
    {
        LevelRegistry.Set(new[]
        {
            new Level("1", "1 этаж", 0, 3000),
            new Level("2", "2 этаж", 3000, 3000),
        });
        LevelRegistry.CurrentId = "2";

        var highlighterGo = new GameObject("Highlighter");
        var highlighter = highlighterGo.AddComponent<ElementHighlighter>();
        try
        {
            var go = ElementFactory.CreateWall(new Vector3Int(3000, 2500, 100), "WallBelow", Vector3.zero);
            var element = go.GetComponent<KitchenElement>();
            element.LevelId = "1";
            var own = go.GetComponent<MeshRenderer>().sharedMaterial;

            highlighter.RefreshHighlights();
            Assert.AreEqual(own, go.GetComponent<MeshRenderer>().sharedMaterial,
                "предпосылка: в режиме «Показывать» этаж ниже рисуется своим материалом");

            Row("Row_" + SettingsConstructionTab.NeighbourLevelsId)
                .GetComponentInChildren<TMP_Dropdown>(true).value = (int)NeighbourLevelsMode.Dim;

            Assert.AreNotEqual(own, go.GetComponent<MeshRenderer>().sharedMaterial,
                "смена режима «Соседние этажи» на «Приглушать» обязана перекрасить сцену "
                + "сразу, без отдельного вызова RefreshHighlights — иначе тонировка виснет "
                + "до следующей случайной правки сцены");
        }
        finally
        {
            Object.DestroyImmediate(highlighterGo);
        }
    }
}
