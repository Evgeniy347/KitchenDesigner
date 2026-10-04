using System.Linq;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;
using KitchenDesigner.Core.Keybinding;
using KitchenDesigner.Core.UI;

/// <summary>
/// Ширина строки настроек живёт в `SettingsPage.ContentW` — там её владелец, и
/// панель кладёт строки по ней. Чистый слой посчитать её сам не может: `SettingsPage`
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
        Assert.AreEqual(SettingsPage.ContentW, KeybindingCellLayout.DefaultRowWidth, 0.01f,
            "ширина строки в панели и ширина, по которой быстрый набор считает бюджет ячеек, "
            + "разошлись. Поправьте KeybindingCellLayout.DefaultRowWidth — иначе "
            + "KeybindingCaptionFitTests стережёт бюджет, которого в панели больше нет.");
    }

    [Test]
    public void TheDerivedColumns_FillTheRow_WithoutOverflowingIt()
    {
        var ruler = KeybindingRowRuler.For(SettingsPage.ContentW,
            KeybindingCellLayout.FallbackWidthFor(
                KeybindingCaption.LongestBoundLength(new KeyBindings()),
                KeybindingCellLayout.MaxCaptionFontSize));

        Assert.AreEqual(SettingsPage.ContentW * 0.5f, ruler.RightEdge, 0.01f,
            "дорожки строки обязаны кончаться ровно на правом краю той ширины, которую "
            + "панель реально раскладывает: остаток или перелёт означают, что линейка и "
            + "панель считают разметку по-разному");
        Assert.That(ruler.LabelWidth, Is.GreaterThanOrEqualTo(KeybindingCellLayout.MinLabelWidth),
            $"подписи действия осталось {ruler.LabelWidth:F0} px");
    }

    /// <summary>Ширина колонок считается от того, что РЕАЛЬНО назначено, поэтому список
    /// привязок меняет высоту прямо во время работы. Таблица стоит ПОСЛЕДНЕЙ на странице,
    /// и её рамка (`KbTable`) обязана расти вместе со строками: иначе нижние строки
    /// окажутся вне рамки, а область прокрутки страницы — короче содержимого, и до них
    /// не доскроллить. Проверяется на настоящей панели: в чистом слое прямоугольников нет.</summary>
    [Test]
    public void ReboundToALongGesture_TheTableFrameStillHoldsEveryRow()
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
            float frameBottom = BottomOf(canvas.transform, "KbTable");

            Assert.That(frameBottom, Is.LessThanOrEqualTo(lastRowBottom + 0.01f),
                $"рамка таблицы кончается на y={frameBottom:F0}, а последняя строка привязки — на "
                + $"y={lastRowBottom:F0}: список вырос вместе с шириной колонок, а рамка осталась прежней.");
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

        Assert.IsNotEmpty(rows, "на странице нет ни одной строки привязки — проверять нечего");
        return rows.Min(WorldBottom);
    }

    private static float BottomOf(Transform root, string name)
    {
        var rect = root.GetComponentsInChildren<RectTransform>(true)
            .FirstOrDefault(r => r.name == name);
        Assert.IsNotNull(rect, $"в панели нет узла «{name}»");
        return WorldBottom(rect!);
    }

    private static float WorldBottom(RectTransform rect)
    {
        var corners = new Vector3[4];
        rect.GetWorldCorners(corners);
        return corners[0].y;
    }
}
