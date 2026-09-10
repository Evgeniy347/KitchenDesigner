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
/// глубина промерзания — ТОЛЬКО ЧТЕНИЕ и до появления таблицы СП 131.13330
/// показывает прочерк, а не выдуманное число, и дропдауны перечисляют ровно те
/// варианты, которые знает модель.</summary>
public class SettingsConstructionTabTests
{
    private const string PagePath =
        "SettingsPanel/SettingsPanelBody/SettingsPanelBodyContent/Tab_Construction/";

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
    }

    [TearDown]
    public void TearDown()
    {
        if (_saved != null) KitchenSettings.Instance.ApplyFrom(_saved.ToData());
        EditModeManager.Reset();
        if (_canvas != null) Object.DestroyImmediate(_canvas!.gameObject);
        var es = Object.FindAnyObjectByType<UnityEngine.EventSystems.EventSystem>();
        if (es != null) Object.DestroyImmediate(es.gameObject);
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
        Assert.IsNotNull(Row("RowDd_" + SettingsConstructionTab.RegionId));
        Assert.IsNotNull(Row("RowRo_" + SettingsConstructionTab.FrostDepthId));
        Assert.IsNotNull(Row("RowDd_" + SettingsConstructionTab.SoilId));
        Assert.IsNotNull(Row("RowFld_Высота этажа"));
        Assert.IsNotNull(Row("RowDd_" + SettingsConstructionTab.MasonryId));
        Assert.IsNotNull(Row("RowFld_Шов"));
        Assert.IsNotNull(Row("RowFld_Запас"));
        Assert.IsNotNull(Row("RowDd_" + SettingsConstructionTab.ConcreteId));
        Assert.IsNotNull(Row("RowFld_Подушка: песок"));
        Assert.IsNotNull(Row("RowFld_Подушка: щебень"));
        Assert.IsNotNull(Row("RowTgl_Трамбовка"));
    }

    [Test]
    public void EveryRowOfTheTab_CarriesItsHintBadge()
    {
        var badges = Page().GetComponentsInChildren<HintBadge>(true);

        Assert.AreEqual(11, badges.Length,
            "у каждого контрола вкладки — своя «i»: одиннадцать строк, одиннадцать значков "
            + "(docs/UI-GUIDELINES.md §13). Найдено: " + badges.Length);
    }

    /// <summary>Глубина промерзания выводится из региона по таблице СП 131.13330,
    /// которой ещё нет. Пока её нет — прочерк: подставленное «примерно 1 800»
    /// пошло бы в расчёт ленты и выглядело бы как настоящее число.</summary>
    [Test]
    public void TheFrostDepth_IsReadOnly_AndShowsADashUntilTheNormativeTableArrives()
    {
        var row = Row("RowRo_" + SettingsConstructionTab.FrostDepthId);

        Assert.IsEmpty(row.GetComponentsInChildren<TMP_InputField>(true),
            "глубина промерзания не вводится руками — она выводится из региона");
        Assert.IsEmpty(row.GetComponentsInChildren<TMP_Dropdown>(true),
            "и не выбирается из списка");

        var value = row.Find("Val_" + SettingsConstructionTab.FrostDepthId);
        Assert.IsNotNull(value, "значение обязано быть чем-то показано");
        Assert.AreEqual(SettingsConstructionTab.FrostDepthUnknown,
            value!.GetComponent<TextMeshProUGUI>().text.Replace("​", ""),
            "до появления таблицы — прочерк, а не выдуманное число");
    }

    [Test]
    public void TheDropdowns_OfferExactlyWhatTheModelKnows()
    {
        var region = Row("RowDd_" + SettingsConstructionTab.RegionId)
            .GetComponentInChildren<TMP_Dropdown>(true);
        var masonry = Row("RowDd_" + SettingsConstructionTab.MasonryId)
            .GetComponentInChildren<TMP_Dropdown>(true);
        var soil = Row("RowDd_" + SettingsConstructionTab.SoilId)
            .GetComponentInChildren<TMP_Dropdown>(true);
        var concrete = Row("RowDd_" + SettingsConstructionTab.ConcreteId)
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

        var region = Row("RowDd_" + SettingsConstructionTab.RegionId)
            .GetComponentInChildren<TMP_Dropdown>(true);
        var masonry = Row("RowDd_" + SettingsConstructionTab.MasonryId)
            .GetComponentInChildren<TMP_Dropdown>(true);
        var soil = Row("RowDd_" + SettingsConstructionTab.SoilId)
            .GetComponentInChildren<TMP_Dropdown>(true);
        var concrete = Row("RowDd_" + SettingsConstructionTab.ConcreteId)
            .GetComponentInChildren<TMP_Dropdown>(true);

        Assert.AreEqual("Урал", region.options[region.value].text, "регион по умолчанию — Урал");
        Assert.AreEqual("Кирпич 250×120×65", masonry.options[masonry.value].text);
        Assert.AreEqual("Неизвестно", soil.options[soil.value].text,
            "неизвестный грунт — худший случай, и он выбран намеренно");
        Assert.AreEqual("B20", concrete.options[concrete.value].text);

        Assert.AreEqual("3000", FieldText("RowFld_Высота этажа"));
        Assert.AreEqual("10", FieldText("RowFld_Шов"));
        Assert.AreEqual("5", FieldText("RowFld_Запас"));
        Assert.AreEqual("100", FieldText("RowFld_Подушка: песок"));
        Assert.AreEqual("100", FieldText("RowFld_Подушка: щебень"));
        Assert.IsTrue(Row("RowTgl_Трамбовка").GetComponentInChildren<Toggle>(true).isOn,
            "трамбовка включена по умолчанию");
    }

    private string FieldText(string rowName) =>
        Row(rowName).GetComponentInChildren<TMP_InputField>(true).text;
}
