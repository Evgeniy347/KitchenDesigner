using System.Linq;
using NUnit.Framework;
using KitchenDesigner.Core.Keybinding;
using KitchenDesigner.Core.UI;

/// <summary>
/// АРИФМЕТИКА раскладки строки привязок — и ничего больше. Имя файла сменилось нарочно:
/// прежнее («…CaptionFit…») обещало то, чего этот набор проверить не может.
///
/// Четыре итерации подряд кадр приезжал с обрезанной подписью, а быстрый набор оставался
/// зелёным. Причина оказалась не в проверках, а в МЕРЕ под ними: ширину текста здесь
/// считали долей кегля на знак, а рисует TMP настоящими метриками шрифта, и для кириллицы
/// домашняя оценка занижена. Тест мерил модель, которая расходится с экраном, — то есть
/// не мог упасть на том дефекте, которому был адресован (`agents/TEST-DESIGN.md`).
///
/// Поэтому обязанности разделены. ПРАВИЛА живут здесь и в `KeybindingCellLayout`: что из
/// чего вычитается, что во что вложено, что ничему не даёт схлопнуться. НАСТОЯЩИЕ ЧИСЛА
/// приходят от TMP там, где панель строится, а вопрос «подпись не обрезана» задают
/// диаграммные тесты — единственное место, где текст меряет тот, кто его рисует
/// (`SettingsPanelTabDiagramTests.AssertNoCaptionIsClipped`).
/// </summary>
public class KeybindingRowArithmeticTests
{
    private const float Row = KeybindingCellLayout.DefaultRowWidth;
    private const float TypicalCaptionWidth = 92f;

    private static KeybindingRowRuler TypicalRuler() =>
        KeybindingRowRuler.For(Row, TypicalCaptionWidth);

    [Test]
    public void TheRowLanes_FollowOneAnother_WithoutOverlapOrRemainder()
    {
        var ruler = TypicalRuler();

        Assert.That(ruler.CellLeft(primary: true) + ruler.CellWidth,
            Is.LessThanOrEqualTo(ruler.ClearLeft(primary: true) + 0.01f),
            "ячейка налезает на крестик очистки");
        Assert.That(ruler.ClearLeft(primary: true) + ruler.ClearWidth,
            Is.LessThanOrEqualTo(ruler.MarkerLeft(primary: false) + 0.01f),
            "первая пара налезает на вторую");
        Assert.AreEqual(Row * 0.5f, ruler.RightEdge, 0.01f,
            "строка обязана кончаться ровно на правом краю: остаток или перелёт означают, "
            + "что панель и линейка считают разметку по-разному");
    }

    [Test]
    public void TheMarkerLane_SitsOutsideTheCell_SoTheCaptionNeverPaysForIt()
    {
        var ruler = TypicalRuler();

        Assert.AreEqual(ruler.MarkerLeft(primary: true) + ruler.MarkerWidth,
            ruler.CellLeft(primary: true), 0.01f,
            "дорожка маркера обязана кончаться ровно там, где начинается ячейка: заедет "
            + "внутрь — отнимет место у подписи, оставит зазор — оторвётся от своей ячейки");
        Assert.AreEqual(ruler.MarkerLeft(primary: false) + ruler.MarkerWidth,
            ruler.CellLeft(primary: false), 0.01f, "то же у альтернативной ячейки");
        Assert.That(ruler.MarkerLeft(primary: true),
            Is.GreaterThanOrEqualTo(ruler.LabelLeft + ruler.LabelWidth - 0.01f),
            "и начинаться правее колонки названий, а не поверх неё");
    }

    [Test]
    public void ACellSizedForACaption_HoldsThatCaptionWithItsPadding()
    {
        float measured = TypicalCaptionWidth;
        float cell = KeybindingCellLayout.CellWidth(Row, measured);

        Assert.That(cell, Is.GreaterThanOrEqualTo(measured),
            "ячейка, посчитанная под измеренную подпись, обязана её вмещать — иначе "
            + "многоточие гарантировано ещё до всякого шрифта");
        Assert.IsTrue(KeybindingCellLayout.CaptionFits(measured, cell));
    }

    [Test]
    public void AWiderCaption_WidensTheCell_UntilTheNameColumnHitsItsFloor()
    {
        float narrow = KeybindingCellLayout.CellWidth(Row, 60f);
        float wide = KeybindingCellLayout.CellWidth(Row, 120f);
        float absurd = KeybindingCellLayout.CellWidth(Row, 10_000f);

        Assert.That(wide, Is.GreaterThan(narrow), "ячейка следует за измеренной подписью");
        Assert.That(KeybindingCellLayout.LabelWidth(Row, absurd),
            Is.GreaterThanOrEqualTo(KeybindingCellLayout.MinLabelWidth),
            "какой бы длинной ни оказалась привязка, колонке названий остаётся её минимум: "
            + "дальше подпись сжимается кеглем, а название не трогается");
    }

    [Test]
    public void TheCellNeverCollapses_EvenWithTheShortestCaption()
    {
        Assert.That(KeybindingCellLayout.CellWidth(Row, 1f),
            Is.GreaterThanOrEqualTo(KeybindingCellLayout.MinCellWidth),
            "одна короткая привязка не имеет права схлопнуть ячейку в ничто — в неё ещё нажимают");
    }

    [Test]
    public void CaptionFits_CanSayNo()
    {
        Assert.IsTrue(KeybindingCellLayout.CaptionFits(90f, 90f), "ровно по ширине — влезло");
        Assert.IsFalse(KeybindingCellLayout.CaptionFits(91f, 90f),
            "шире ячейки — не влезло; без этой половины проверки выше пусты");
    }

    [Test]
    public void TheFontSize_ShrinksInProportionToTheRoom_AndNeverPastTheFloor()
    {
        Assert.AreEqual(KeybindingCellLayout.MaxCaptionFontSize,
            KeybindingCellLayout.FontSizeFor(80f, 90f),
            "подпись влезает целиком — мельчить незачем");
        Assert.AreEqual(9, KeybindingCellLayout.FontSizeFor(140f, 90f),
            "подпись в полтора раза шире места — кегль во столько же раз мельче "
            + "(14 × 90 / 140), пока не упёрся в предел");
        Assert.AreEqual(KeybindingCellLayout.MinCaptionFontSize,
            KeybindingCellLayout.FontSizeFor(10_000f, 90f),
            "но не мельче предела читаемости: дальше за неё отвечает многоточие");
    }

    /// <summary>Запасная оценка работает только там, где шрифта нет вовсе (сборка без
    /// TMP-ассетов), и обязана ЗАВЫШАТЬ: занижение — это и есть обрезка, а завышение
    /// безопасно, потому что рисовать в этот момент всё равно нечем. Число взято с запасом
    /// над самой широкой буквой, которая может встретиться в наших подписях: у кириллической
    /// «М» в LiberationSans ширина около 0,83 кегля, у остальных меньше.</summary>
    [Test]
    public void TheFallbackEstimate_OverstatesRatherThanUndercuts()
    {
        Assert.That(KeybindingCellLayout.FallbackCharWidthPerPoint, Is.GreaterThanOrEqualTo(0.83f),
            "запасная оценка обязана быть не уже самой широкой буквы наших подписей");

        float halfEmEstimate = 12 * 0.5f * KeybindingCellLayout.MaxCaptionFontSize;
        Assert.That(KeybindingCellLayout.FallbackWidthFor(12, KeybindingCellLayout.MaxCaptionFontSize),
            Is.GreaterThan(halfEmEstimate),
            "прежняя мера «полкегля на знак» занижала ширину кириллицы — именно она и "
            + "пропустила четыре обрезки подряд");
    }

    [Test]
    public void TheConflictMarker_IsNotPartOfTheCaptionText()
    {
        var bindings = new KeyBindings();

        foreach (var action in InputActionCatalog.All)
        {
            string text = KeybindingCaption.CellText(bindings.PrimaryBinding(action));
            StringAssert.DoesNotContain(KeybindingCaption.MarkerText, text,
                $"подпись «{text}» несёт маркер внутри себя. Тогда его срежет вместе с "
                + "текстом, как только подпись перестанет влезать, — а это ровно то "
                + "состояние, в котором маркер и нужен");
        }
    }

    [Test]
    public void TheHintBadge_GetsItsOwnLane_OutsideTheNameText()
    {
        float label = TypicalRuler().LabelWidth;
        float text = KeybindingCellLayout.LabelTextWidth(label, hasHint: true);

        Assert.AreEqual(label - KeybindingCellLayout.HintLaneWidth, text, 0.01f,
            "строка со значком «i» обязана отдать ему дорожку из ширины названия");

        float badgeLeft = KeybindingCellLayout.HintBadgeCentreX(text)
            - KeybindingCellLayout.HintLaneWidth * 0.5f;
        Assert.That(badgeLeft, Is.GreaterThanOrEqualTo(text * 0.5f - 0.01f),
            "значок обязан стоять ПРАВЕЕ текста названия, а не поверх последней строки");
        Assert.That(badgeLeft + KeybindingCellLayout.HintLaneWidth,
            Is.LessThanOrEqualTo(text * 0.5f + KeybindingCellLayout.HintLaneWidth + 0.01f),
            "и не вылезать за колонку названия");
    }

    [Test]
    public void WithoutAHint_TheNameUsesTheWholeColumn()
    {
        float label = TypicalRuler().LabelWidth;

        Assert.AreEqual(label, KeybindingCellLayout.LabelTextWidth(label, hasHint: false), 0.01f,
            "строка без подсказки не обязана оставлять пустую дорожку");
    }

    [Test]
    public void EveryMouseActionDefault_ShowsSomething()
    {
        var mouse = InputActionCatalog.All
            .Where(a => InputActionCatalog.GroupOf(a) == InputActionGroup.Mouse)
            .ToList();

        Assert.That(mouse.Count, Is.GreaterThan(0), "в каталоге нет действий мыши");
        foreach (var action in mouse)
            Assert.IsNotEmpty(KeybindingCaption.CellText(KeyBindingDefaults.PrimaryOf(action)),
                $"пустая подпись у {action}");
    }
}
