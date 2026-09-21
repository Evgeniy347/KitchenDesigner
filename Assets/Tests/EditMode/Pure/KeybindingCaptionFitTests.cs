using System.Linq;
using NUnit.Framework;
using KitchenDesigner.Core.Keybinding;
using KitchenDesigner.Core.UI;

/// <summary>
/// Подпись обязана помещаться в свою ячейку — и не на сегодняшних данных, а на самой
/// длинной подписи, которая вообще может там оказаться.
///
/// Поймано это было глазами: «! СКМ с движением» вылезало за ячейку, потому что её
/// ширина была зашита числом 96, подобранным под клавиатурные подписи вроде «Ctrl+D».
/// Мышиные подписи длиннее, а с маркером конфликта ещё длиннее — и «подогнать 96 под
/// сегодняшний максимум» вернуло бы ту же поломку с первым же новым действием.
///
/// Поэтому ширина ВЫВОДИТСЯ из каталога (`KeybindingCaption.Longest`), ровно как ширина
/// вкладки выводится из её подписи в `SettingsTabStrip.LayOut`, и оттуда же взята
/// оценка ширины текста без шрифта — «полкегля на знак» (`CharWidthPerPoint`,
/// `SettingsTabStrip.CaptionWidth` считает так же). Проверка быстрая: она про ЧИСЛА
/// раскладки, а не про кадр.
///
/// Сенсором её делает последняя пара проверок: добавят завтра действие с длинной
/// подписью или удлинят формат жеста — красной станет не «ячейка», а БЮДЖЕТ СТРОКИ,
/// с именем виновной подписи.
/// </summary>
public class KeybindingCaptionFitTests
{
    private static float CellWidth() =>
        KeybindingCellLayout.CellWidth(KeybindingCaption.LongestLength());

    [Test]
    public void EveryWorstCaseCaption_FitsTheCell()
    {
        float cell = CellWidth();

        var tooWide = KeybindingCaption.WorstCases()
            .Where(caption => !KeybindingCellLayout.Fits(caption, cell))
            .Distinct()
            .ToList();

        CollectionAssert.IsEmpty(tooWide,
            $"ячейка шириной {cell:F0} px не вмещает подпись даже на минимальном кегле "
            + $"{KeybindingCellLayout.MinCaptionFontSize}: " + string.Join(" | ", tooWide));
    }

    [Test]
    public void TheWorstCase_IsAGestureWithEveryModifierAndTheConflictMarker()
    {
        string longest = KeybindingCaption.Longest();

        StringAssert.StartsWith(KeybindingCaption.ConflictMarker, longest,
            "самая длинная подпись обязана учитывать маркер конфликта — он тоже занимает место");
        StringAssert.Contains("Ctrl+", longest);
        StringAssert.Contains("Alt+", longest);
        StringAssert.Contains("Shift+", longest);
        Assert.That(longest.Length, Is.GreaterThan(KeybindingCaption.ConflictMarker.Length + 10),
            "подозрительно короткий «худший случай» — значит перебор подписей что-то не увидел");
    }

    [Test]
    public void TheRowStillBalances_LabelKeepsItsMinimumWidth()
    {
        float cell = CellWidth();
        float label = KeybindingCellLayout.LabelWidth(KeybindingCellLayout.DefaultRowWidth, cell);

        Assert.That(label, Is.GreaterThanOrEqualTo(KeybindingCellLayout.MinLabelWidth),
            $"ячейки разрослись до {cell:F0} px и оставили подписи действия {label:F0} px — "
            + "строка перестала помещаться в ширину вкладки. Это не повод сузить ячейку "
            + "обратно: либо подпись действия переезжает на свою строку, либо формат "
            + "привязки становится короче. Самая длинная подпись сейчас: "
            + KeybindingCaption.Longest());
    }

    [Test]
    public void EveryGestureShape_IsAmongTheWorstCases()
    {
        var worstCases = KeybindingCaption.WorstCases().ToList();

        foreach (var gesture in KeybindingCaption.EveryGestureShape())
        {
            var loaded = InputBinding.FromGesture(gesture);
            string expected = KeybindingCaption.WorstCaseOf(loaded);
            CollectionAssert.Contains(worstCases, expected,
                "перебор обязан видеть каждую форму жеста — иначе он меряет не худший случай");
        }
    }

    /// <summary>Противоположный вход: модель обязана уметь сказать «не влезает». Без этой
    /// пары проверка выше зеленела бы и при `Fits`, возвращающем `true` всегда.</summary>
    [Test]
    public void ACaptionLongerThanTheCell_DoesNotFit()
    {
        float cell = CellWidth();
        int charsThatFit = (int)(cell / (KeybindingCellLayout.CharWidthPerPoint
            * KeybindingCellLayout.MinCaptionFontSize));

        Assert.IsTrue(KeybindingCellLayout.Fits(new string('X', charsThatFit), cell),
            "подпись ровно по ширине ячейки обязана считаться влезшей");
        Assert.IsFalse(KeybindingCellLayout.Fits(new string('X', charsThatFit + 2), cell),
            "подпись шире ячейки обязана считаться НЕ влезшей — иначе проверка выше пуста");
    }

    [Test]
    public void ALongerActionCaption_WouldWidenTheCell()
    {
        int today = KeybindingCaption.LongestLength();

        Assert.That(KeybindingCellLayout.CellWidth(today + 8),
            Is.GreaterThan(KeybindingCellLayout.CellWidth(today)),
            "ширина ячейки ВЫВОДИТСЯ из самой длинной подписи: зашитое число вернуло бы "
            + "ту же поломку на первом же новом действии");
    }
}
