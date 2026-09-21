using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using KitchenDesigner.Core;
using KitchenDesigner.Core.Keybinding;
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

    /// <summary>Ширина колонок считается от того, что РЕАЛЬНО назначено, поэтому список
    /// привязок меняет высоту прямо во время работы — а блок справки стоит под ним. Если
    /// он останется на месте, он наложится на последние строки списка, и человек увидит
    /// текст поверх текста. Проверяется на настоящей панели: в чистом слое прямоугольников
    /// нет.</summary>
    [Test]
    public void ReboundToALongGesture_TheReferenceBlockStaysBelowTheList()
    {
        var canvas = UIFactory.CreateCanvas("ControlTabReflowCanvas");
        var settings = KitchenSettings.Instance;
        var saved = settings.ToData();
        try
        {
            settings.ResetToDefaults();
            var panel = canvas.gameObject.AddComponent<SettingsPanelUI>();
            panel.Build(canvas.transform);
            panel.OpenControlsTab();

            settings.KeyBindings.SetPrimaryBinding(InputAction.SelectClick,
                InputBinding.FromGesture(new MouseGesture(MouseButtonKind.XButton1,
                    withMotion: true, ctrl: true, alt: true, shift: true)));
            panel.SyncFromSettings();

            float lastRowBottom = BottomOfTheLastBindingRow(canvas.transform);
            float referenceTop = TopOf(canvas.transform, "KbRefSection");

            Assert.That(referenceTop, Is.LessThanOrEqualTo(lastRowBottom + 0.01f),
                $"блок справки начинается на y={referenceTop:F0}, а список привязок кончается "
                + $"на y={lastRowBottom:F0} — справка наехала на последние строки. Список "
                + "меняет высоту вместе с шириной колонок, и всё, что стоит ниже, обязано "
                + "уезжать вместе с ним.");
        }
        finally
        {
            settings.ApplyFrom(saved);
            EditModeManager.Reset();
            Object.DestroyImmediate(canvas.gameObject);
        }
    }

    private static float BottomOfTheLastBindingRow(Transform root)
    {
        var rows = root.GetComponentsInChildren<RectTransform>(true)
            .Where(r => r.name.StartsWith("KbRow_", System.StringComparison.Ordinal))
            .ToList();

        Assert.IsNotEmpty(rows, "во вкладке нет ни одной строки привязки — проверять нечего");
        return rows.Min(r => r.anchoredPosition.y - r.rect.height * 0.5f);
    }

    private static float TopOf(Transform root, string name)
    {
        var rect = root.GetComponentsInChildren<RectTransform>(true)
            .FirstOrDefault(r => r.name == name);
        Assert.IsNotNull(rect, $"в панели нет узла «{name}»");
        return rect!.anchoredPosition.y + rect.rect.height * 0.5f;
    }
}
