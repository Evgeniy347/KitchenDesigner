using NUnit.Framework;
using KitchenDesigner.Core;

/// <summary>
/// L5a (план LEVELS): настройка «Соседние этажи» — показывать/приглушать/скрывать,
/// дефолт «показывать» (docs/todo_evolution.md §3.4). MCP-поверхность (SettingKeys,
/// ParamsSetSetting) и строка панели намеренно НЕ заведены в этом коммите — обе требуют
/// подсказки «i» (HintText.cs, HintCoverageGuardTests), а этот каталог принадлежит
/// другому агенту в этой кампании; довести до конца сможет он или отдельный проход.
/// </summary>
public class KitchenSettingsLevelsTests
{
    private KitchenSettingsData _savedData = null!;

    [SetUp]
    public void SetUp()
    {
        _savedData = KitchenSettings.Instance.ToData();
    }

    [TearDown]
    public void TearDown()
    {
        KitchenSettings.Instance.ApplyFrom(_savedData);
    }

    [Test]
    public void NeighbourLevels_DefaultsToShow()
    {
        KitchenSettings.Instance.ResetToDefaults();

        Assert.AreEqual(NeighbourLevelsMode.Show, KitchenSettings.Instance.NeighbourLevels);
    }

    [Test]
    public void NeighbourLevels_Setter_ClampsOutOfRangeValues()
    {
        KitchenSettings.Instance.NeighbourLevels = (NeighbourLevelsMode)99;
        Assert.AreEqual(NeighbourLevelsMode.Hide, KitchenSettings.Instance.NeighbourLevels,
            "значение выше диапазона обязано прижаться к последнему варианту (Hide), "
            + "а не остаться мусорным числом");

        KitchenSettings.Instance.NeighbourLevels = (NeighbourLevelsMode)(-5);
        Assert.AreEqual(NeighbourLevelsMode.Show, KitchenSettings.Instance.NeighbourLevels,
            "значение ниже диапазона обязано прижаться к первому варианту (Show)");
    }

    [Test]
    public void NeighbourLevels_SurvivesToDataAndApplyFrom()
    {
        KitchenSettings.Instance.NeighbourLevels = NeighbourLevelsMode.Dim;
        var data = KitchenSettings.Instance.ToData();

        var fresh = new KitchenSettings();
        fresh.ApplyFrom(data);

        Assert.AreEqual(NeighbourLevelsMode.Dim, fresh.NeighbourLevels);
    }

    [Test]
    public void NeighbourLevelsModeTitles_HasOneTitlePerEnumValue()
    {
        Assert.AreEqual(3, NeighbourLevelsModeTitles.All.Length);
        Assert.AreEqual("Показывать", NeighbourLevelsModeTitles.Of(NeighbourLevelsMode.Show));
        Assert.AreEqual("Приглушать", NeighbourLevelsModeTitles.Of(NeighbourLevelsMode.Dim));
        Assert.AreEqual("Скрывать", NeighbourLevelsModeTitles.Of(NeighbourLevelsMode.Hide));
    }
}
