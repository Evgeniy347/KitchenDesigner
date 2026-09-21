using System.Linq;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core.Keybinding;
using KitchenDesigner.Core.UI;

/// <summary>
/// Как делится ширина строки. Порядок приоритетов здесь не вкусовой — он оплачен двумя
/// заходами.
///
/// Сначала ячейка была зашита числом 96 px, подобранным под клавиатурные подписи, и
/// «! СКМ+движение» из неё вылезало. Потом ширину вывели из ТЕОРЕТИЧЕСКОГО максимума
/// каталога («! Ctrl+Alt+Shift+Кнопка 4+движение»), которого нет ни у кого, — и тридцать
/// реальных строк заплатили за несуществующую комбинацию: колонка названий сжалась, почти
/// каждое название стало двухэтажным, вкладка выросла вдвое.
///
/// Отсюда правило: **ячейке — по факту, остальное — названию**. Ширина считается от того,
/// что РЕАЛЬНО назначено сейчас, и пересчитывается при каждой правке; названия получают
/// весь остаток; когда остатка мало, сжимается кегль ПОДПИСИ (авторазмер), а не название.
/// Маркер конфликта в ширину не закладывается нарочно: он появляется в редком состоянии, и
/// оплачивать его постоянным расширением колонки — та же ошибка в миниатюре.
///
/// Сенсором остаётся БЮДЖЕТ: сойдётся ли строка, если в ячейку попадёт самая длинная
/// подпись, какую каталог вообще может дать.
/// </summary>
public class KeybindingCaptionFitTests
{
    private const float Row = KeybindingCellLayout.DefaultRowWidth;

    private static float CellForDefaults()
    {
        var bindings = new KeyBindings();
        return KeybindingCellLayout.CellWidth(Row, KeybindingCaption.LongestBoundLength(bindings));
    }

    [Test]
    public void OutOfTheBox_EveryBoundCaptionFitsItsCell_AtFullSize()
    {
        var bindings = new KeyBindings();
        float cell = CellForDefaults();

        var cramped = InputActionCatalog.All
            .SelectMany(a => new[] { bindings.PrimaryBinding(a), bindings.AltBinding(a) })
            .Where(b => !b.IsEmpty)
            .Select(InputBindingDisplay.Of)
            .Where(caption => !KeybindingCellLayout.FitsComfortably(caption, cell))
            .Distinct()
            .ToList();

        CollectionAssert.IsEmpty(cramped,
            $"заводская привязка не помещается в ячейку {cell:F0} px на полном кегле "
            + $"{KeybindingCellLayout.MaxCaptionFontSize} — человек увидит сжатый текст там, "
            + "где ничего не переназначал: " + string.Join(" | ", cramped));
    }

    [Test]
    public void OutOfTheBox_TheNameColumnKeepsMostOfTheRow()
    {
        float cell = CellForDefaults();
        float label = KeybindingCellLayout.LabelWidth(Row, cell);

        Assert.That(label, Is.GreaterThan(2f * cell),
            $"колонка названий {label:F0} px против ячейки {cell:F0} px. Названия — самая "
            + "длинная часть строки, и именно они обязаны получать остаток: ширина, "
            + "выведенная из редкого длинного аккорда, делает почти каждую строку "
            + "двухэтажной, а вкладку вдвое выше");
        Assert.That(label, Is.GreaterThanOrEqualTo(220f),
            $"колонка названий ужалась до {label:F0} px — меньше, чем было до правки, "
            + "а значит названия снова поедут на вторую строку");
    }

    [Test]
    public void ALongerBinding_WidensTheCell_OnlyWhenItIsActuallyBound()
    {
        var bindings = new KeyBindings();
        float before = CellForDefaults();

        bindings.SetPrimaryBinding(InputAction.SelectClick, InputBinding.FromGesture(
            new MouseGesture(MouseButtonKind.XButton1, withMotion: true,
                ctrl: true, alt: true, shift: true)));
        float after = KeybindingCellLayout.CellWidth(Row,
            KeybindingCaption.LongestBoundLength(bindings));

        Assert.That(after, Is.GreaterThan(before),
            "назначили длинную привязку — колонка обязана вырасти именно в этот момент, "
            + "а не заранее для всех");
    }

    [Test]
    public void EvenTheLongestCaptionTheCatalogCanGive_StillLeavesTheNameItsMinimum()
    {
        float cell = KeybindingCellLayout.CellWidth(Row, KeybindingCaption.LongestLength());
        float label = KeybindingCellLayout.LabelWidth(Row, cell);

        Assert.That(label, Is.GreaterThanOrEqualTo(KeybindingCellLayout.MinLabelWidth),
            $"строка перестала сходиться: ячейкам {cell:F0} px, названию {label:F0} px. "
            + "Сужать ячейку обратно нельзя — либо формат привязки становится короче, либо "
            + "название переезжает на свою строку. Самая длинная подпись каталога сейчас: "
            + KeybindingCaption.Longest());
    }

    [Test]
    public void TheWorstCaseCaption_StillFitsTheCell_AtTheSmallestFontSize()
    {
        float cell = KeybindingCellLayout.CellWidth(Row, KeybindingCaption.LongestLength());

        Assert.IsTrue(KeybindingCellLayout.Fits(KeybindingCaption.Longest(), cell),
            "на минимальном кегле в ячейку обязана влезать и самая длинная подпись каталога: "
            + KeybindingCaption.Longest());
    }

    [Test]
    public void TheHintBadge_GetsItsOwnLane_OutsideTheNameText()
    {
        float label = KeybindingCellLayout.LabelWidth(Row, CellForDefaults());
        float text = KeybindingCellLayout.LabelTextWidth(label, hasHint: true);

        Assert.AreEqual(label - KeybindingCellLayout.HintLaneWidth, text, 0.01f,
            "строка со значком «i» обязана отдать ему дорожку из ширины названия");

        float badgeLeft = KeybindingCellLayout.HintBadgeCentreX(text)
            - KeybindingCellLayout.HintLaneWidth * 0.5f;
        Assert.That(badgeLeft, Is.GreaterThanOrEqualTo(text * 0.5f - 0.01f),
            "значок обязан стоять ПРАВЕЕ текста названия. Он накладывался на подпись "
            + "(«производительности:i»), потому что его сажали по ширине неперенесённого "
            + "текста, а не в отведённую дорожку");
        Assert.That(badgeLeft + KeybindingCellLayout.HintLaneWidth,
            Is.LessThanOrEqualTo(text * 0.5f + KeybindingCellLayout.HintLaneWidth + 0.01f),
            "и не вылезать за колонку названия: значок живёт в координатах ПРЯМОУГОЛЬНИКА "
            + "ТЕКСТА, а колонка шире него ровно на дорожку");
    }

    [Test]
    public void WithoutAHint_TheNameUsesTheWholeColumn()
    {
        float label = KeybindingCellLayout.LabelWidth(Row, CellForDefaults());

        Assert.AreEqual(label, KeybindingCellLayout.LabelTextWidth(label, hasHint: false), 0.01f,
            "строка без подсказки не обязана оставлять пустую дорожку");
    }

    /// <summary>Противоположный вход: модель обязана уметь сказать «не влезает» — иначе
    /// проверки выше зеленели бы при `Fits`, всегда отвечающем «да».</summary>
    [Test]
    public void ACaptionLongerThanTheCell_DoesNotFit()
    {
        float cell = CellForDefaults();
        int charsThatFit = (int)(cell / (KeybindingCellLayout.CharWidthPerPoint
            * KeybindingCellLayout.MinCaptionFontSize));

        Assert.IsTrue(KeybindingCellLayout.Fits(new string('X', charsThatFit), cell));
        Assert.IsFalse(KeybindingCellLayout.Fits(new string('X', charsThatFit + 2), cell));
        Assert.IsFalse(KeybindingCellLayout.FitsComfortably(new string('X', charsThatFit), cell),
            "на полном кегле та же подпись обязана НЕ влезать — иначе проверка про "
            + "заводские привязки ничего не стережёт");
    }

    [Test]
    public void TheCellNeverCollapses_EvenWithTheShortestCaptions()
    {
        Assert.That(KeybindingCellLayout.CellWidth(Row, 1),
            Is.GreaterThanOrEqualTo(KeybindingCellLayout.MinCellWidth),
            "одна короткая привязка не имеет права схлопнуть ячейку в ничто — в неё ещё "
            + "нажимают");
    }
}
