using NUnit.Framework;
using KitchenDesigner.Core.UI;

/// <summary>
/// Ширина строки настроек живёт в `SettingsRowFactory.ContentW` — там её владелец, и
/// панель кладёт строки по ней. Чистый слой посчитать её сам не может: `SettingsRowFactory`
/// — Unity-класс, и быстрый набор его не видит, поэтому у проверки бюджета ячеек
/// (`KeybindingCaptionFitTests`) есть своя константа `DefaultRowWidth`.
///
/// Две записи одного числа расходятся молча: поменяют ширину строки в панели — и быстрая
/// проверка продолжит стеречь старый бюджет, зеленея на подписях, которые в новую строку
/// уже не влезают. Этот тест — единственное место, где обе стороны встречаются.
/// </summary>
public class KeybindingRowWidthGuardTests
{
    [Test]
    public void ThePureCellBudget_MeasuresTheSameRowWidth_ThePanelActuallyLaysOut()
    {
        Assert.AreEqual(SettingsRowFactory.ContentW, KeybindingCellLayout.DefaultRowWidth, 0.01f,
            "ширина строки в панели и ширина, по которой быстрый набор считает бюджет ячеек, "
            + "разошлись. Поправьте KeybindingCellLayout.DefaultRowWidth — иначе "
            + "KeybindingCaptionFitTests стережёт бюджет, которого в панели больше нет.");
    }

    [Test]
    public void TheDerivedColumns_FillTheRow_WithoutOverflowingIt()
    {
        float cell = KeybindingCellLayout.CellWidth(KeybindingCaption.LongestLength());
        float label = KeybindingCellLayout.LabelWidth(SettingsRowFactory.ContentW, cell);

        float used = label + KeybindingCellLayout.GapAfterLabel
            + 2f * cell + 2f * (KeybindingCellLayout.GapBeforeClear + KeybindingCellLayout.ClearWidth)
            + KeybindingCellLayout.GapBetweenCells;

        Assert.AreEqual(SettingsRowFactory.ContentW, used, 0.01f,
            "колонки обязаны складываться ровно в ширину строки: остаток означает, что "
            + "подпись действия или ячейка считают ширину по-своему");
        Assert.That(label, Is.GreaterThanOrEqualTo(KeybindingCellLayout.MinLabelWidth),
            $"подписи действия осталось {label:F0} px");
    }
}
