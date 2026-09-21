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

    private static float CellForDefaults() => RulerForDefaults().CellWidth;

    private static KeybindingRowRuler RulerForDefaults() =>
        KeybindingRowRuler.For(Row, KeybindingCaption.LongestBoundLength(new KeyBindings()));

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

    /// <summary>ТРЕБОВАНИЕ целиком, а не сегодняшний симптом: в конфликтующей строке
    /// человек обязан увидеть И маркер целиком, И подпись целиком.
    ///
    /// Эту проверку пришлось переписывать трижды, и каждый раз она стерегла ровно ту
    /// половину, которую только что починили. Сперва маркер был префиксом внутри подписи
    /// — его срезало вместе с текстом, а тест смотрел на суммарную ширину. Потом маркер
    /// получил дорожку ВНУТРИ ячейки — маркер стало видно, но дорожка отняла место у
    /// подписи, и обрезалась уже она, а тест смотрел только на маркер. Поэтому теперь
    /// проверка спрашивает обе половины СРАЗУ и по одной линейке
    /// (<see cref="KeybindingRowRuler"/>), которой размечает строку сама панель.</summary>
    [Test]
    public void InAConflict_BothTheMarkerAndTheWholeCaption_AreVisible()
    {
        var bindings = new KeyBindings();
        var ruler = RulerForDefaults();

        Assert.IsTrue(KeybindingCellLayout.MarkerFitsItsLane(),
            $"маркер «{KeybindingCaption.MarkerText}» не помещается в свою дорожку "
            + $"{ruler.MarkerWidth:F0} px — его срежет, а это единственный носитель смысла "
            + "для тех, кто не различает красный");

        var clipped = InputActionCatalog.All
            .SelectMany(a => new[] { bindings.PrimaryBinding(a), bindings.AltBinding(a) })
            .Where(b => !b.IsEmpty)
            .Select(KeybindingCaption.CellText)
            .Where(text => !KeybindingCellLayout.FitsComfortably(text, ruler.CellWidth))
            .Distinct()
            .ToList();

        CollectionAssert.IsEmpty(clipped,
            $"подпись не помещается в ячейку {ruler.CellWidth:F0} px на полном кегле — "
            + "её срежет многоточием, и человек увидит, ЧТО конфликт есть, но не увидит, "
            + "С ЧЕМ: " + string.Join(" | ", clipped));
    }

    /// <summary>Вторая половина того же требования: за дорожку маркера не платит подпись.
    /// Дорожка живёт в зазоре СЛЕВА от ячейки, поэтому ширина подписи одна и та же в
    /// конфликте и без него — таблица не переезжает, когда конфликт появляется и уходит.</summary>
    [Test]
    public void TheMarkerLane_SitsOutsideTheCell_SoTheCaptionNeverPaysForIt()
    {
        var ruler = RulerForDefaults();

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
    public void TheRowLanes_FollowOneAnother_WithoutOverlapOrGapAtTheEnd()
    {
        var ruler = RulerForDefaults();

        Assert.That(ruler.CellLeft(primary: true) + ruler.CellWidth,
            Is.LessThanOrEqualTo(ruler.ClearLeft(primary: true) + 0.01f),
            "ячейка налезает на крестик очистки");
        Assert.That(ruler.ClearLeft(primary: true) + ruler.ClearWidth,
            Is.LessThanOrEqualTo(ruler.MarkerLeft(primary: false) + 0.01f),
            "первая пара налезает на вторую");
        Assert.AreEqual(ruler.RowWidth * 0.5f, ruler.RightEdge, 0.01f,
            "строка обязана кончаться ровно на правом краю: остаток или перелёт означают, "
            + "что панель и линейка считают разметку по-разному");
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
    public void TheFontSize_ShrinksWithTheRoom_AndNeverPastTheFloor()
    {
        Assert.AreEqual(KeybindingCellLayout.MaxCaptionFontSize,
            KeybindingCellLayout.FontSizeFor(4, 200f),
            "короткой подписи в просторной ячейке незачем мельчить");

        Assert.That(KeybindingCellLayout.FontSizeFor(40, 90f),
            Is.LessThan(KeybindingCellLayout.MaxCaptionFontSize),
            "длинная подпись обязана сжиматься — иначе она вылезет за ячейку, как вылез маркер");

        Assert.AreEqual(KeybindingCellLayout.MinCaptionFontSize,
            KeybindingCellLayout.FontSizeFor(400, 90f),
            "но не мельче предела читаемости: дальше за неё отвечает многоточие");
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
