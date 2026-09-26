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

    /// <summary>L9 (review-ui-mcp): ApplyLevels писал сырое число из данных напрямую в
    /// поле, минуя клампающий сеттер NeighbourLevels — значение вне диапазона (повреждённый
    /// файл, файл от будущей версии с большим числом режимов) доходило до выпадающего
    /// списка настроек мусорным числом вместо того, чтобы прижаться к границе.</summary>
    [Test]
    public void ApplyFrom_ClampsAnOutOfRangeNeighbourLevelsMode_InsteadOfStoringItRaw()
    {
        var data = KitchenSettings.Instance.ToData();
        data.neighbourLevelsMode = 99;

        var fresh = new KitchenSettings();
        fresh.ApplyFrom(data);

        Assert.AreEqual(NeighbourLevelsMode.Hide, fresh.NeighbourLevels,
            "значение выше диапазона обязано прижаться к последнему варианту через тот же " +
            "клампающий сеттер, каким пользуется NeighbourLevels напрямую");
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
