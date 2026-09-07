using System;
using System.Linq;
using KitchenDesigner.Core.UI;
using NUnit.Framework;

/// <summary>
/// Лента консоли (открывается клавишей «ё»/BackQuote). До этой правки она
/// показывала только то, что пришло через Application.logMessageReceived, а
/// сообщения строки состояния жили 3–4 секунды и исчезали навсегда: пропустил
/// глазами — узнать, что именно сказала программа, было неоткуда.
///
/// Лента — единственный буфер на два источника (журнал Unity и строка
/// состояния), поэтому порядок строк получается сам собой и сливать два списка
/// с двумя часами не приходится. Всё, что здесь проверяется, — чистое: ни
/// сцены, ни MonoBehaviour, ни Time.unscaledTime, поэтому тест идёт под dotnet
/// за доли секунды.
/// </summary>
public class ConsoleLogTests
{
    private static readonly DateTime T0 = new DateTime(2026, 9, 8, 19, 4, 11);

    private static DateTime At(int seconds) => T0.AddSeconds(seconds);

    [Test]
    public void EveryMessage_BecomesItsOwnLine_InTheOrderItArrived()
    {
        var log = new ConsoleLog();

        log.Append(ConsoleLineKind.Log, "загружено 42 элемента", At(0));
        log.Append(ConsoleLineKind.Status, "Прилипание отключено (Ctrl)", At(1));
        log.Append(ConsoleLineKind.StatusSuccess, "Сохранено: kitchen.json", At(2));

        Assert.AreEqual(3, log.Count, "три разных сообщения — три строки");
        CollectionAssert.AreEqual(
            new[] { "загружено 42 элемента", "Прилипание отключено (Ctrl)", "Сохранено: kitchen.json" },
            log.Lines.Select(l => l.Text).ToArray(),
            "порядок строк в ленте — порядок событий; ради этого оба источника пишут "
            + "в ОДИН буфер, а не в два, которые пришлось бы сливать по часам");
    }

    [Test]
    public void TheSameMessageTwiceInARow_CollapsesIntoOneLineWithACount()
    {
        var log = new ConsoleLog();

        log.Append(ConsoleLineKind.Status, "Прилипание отключено (Ctrl)", At(0));
        log.Append(ConsoleLineKind.Status, "Прилипание отключено (Ctrl)", At(1));
        log.Append(ConsoleLineKind.Status, "Прилипание отключено (Ctrl)", At(5));

        Assert.AreEqual(1, log.Count, "подряд идущий повтор не заводит новую строку");
        Assert.AreEqual(3, log.Lines[0].Repeats);
        StringAssert.Contains("×3", ConsoleLog.Format(log.Lines[0]),
            "счётчик обязан быть ВИДЕН: без него свёрнутая строка врёт, будто сообщение "
            + "было одно");
        StringAssert.Contains("19:04:16", ConsoleLog.Format(log.Lines[0]),
            "у свёрнутой строки показывается время ПОСЛЕДНЕГО повтора: человек ищет "
            + "«когда это было в прошлый раз», а не когда началось");
    }

    [Test]
    public void TheSameMessageWithAnotherLineBetween_StaysTwoLines()
    {
        var log = new ConsoleLog();

        log.Append(ConsoleLineKind.Status, "Прилипание отключено (Ctrl)", At(0));
        log.Append(ConsoleLineKind.Status, "Прилипание включено (Ctrl)", At(1));
        log.Append(ConsoleLineKind.Status, "Прилипание отключено (Ctrl)", At(2));

        Assert.AreEqual(3, log.Count,
            "сворачиваются только ПОДРЯД идущие повторы. Иначе Ctrl, нажатый и отпущенный "
            + "десять раз, схлопнулся бы в две строки «×10» и порядок событий пропал бы");
    }

    [Test]
    public void TheSameTextFromTwoDifferentSources_StaysTwoLines()
    {
        var log = new ConsoleLog();

        log.Append(ConsoleLineKind.Log, "Сохранено", At(0));
        log.Append(ConsoleLineKind.StatusSuccess, "Сохранено", At(1));

        Assert.AreEqual(2, log.Count,
            "совпал текст, но не источник: строка состояния и журнал движка — разные события, "
            + "и сворачивание их в одну строку соврало бы про оба");
    }

    [Test]
    public void EmptyText_IsNotRecorded()
    {
        var log = new ConsoleLog();

        log.Append(ConsoleLineKind.Status, "", At(0));
        log.Append(ConsoleLineKind.Status, null!, At(1));

        Assert.AreEqual(0, log.Count,
            "StatusBarUI.ShowTransient(\"\") — это команда ПОГАСИТЬ строку состояния, а не "
            + "сообщение; попав в историю, она засорила бы её пустыми строками");
    }

    [Test]
    public void ALongSession_KeepsTheLastLinesAndDropsTheOldest()
    {
        var log = new ConsoleLog();

        for (int i = 0; i < ConsoleLog.Capacity * 3; i++)
            log.Append(ConsoleLineKind.Log, "строка " + i, At(i));

        Assert.AreEqual(ConsoleLog.Capacity, log.Count,
            "буфер кольцевой: без потолка многочасовая сессия — это утечка памяти");
        Assert.AreEqual("строка " + (ConsoleLog.Capacity * 3 - 1), log.Lines[log.Count - 1].Text,
            "последнее сообщение всегда на месте");
        Assert.AreEqual("строка " + (ConsoleLog.Capacity * 2), log.Lines[0].Text,
            "вытесняются САМЫЕ СТАРЫЕ — если бы отбрасывались новые, лента застыла бы "
            + "на первых 400 строках и перестала показывать происходящее");
    }

    [Test]
    public void ARepeatingMessage_DoesNotEatTheHistory()
    {
        var log = new ConsoleLog();

        log.Append(ConsoleLineKind.Status, "важное, случилось первым", At(0));
        for (int i = 0; i < 10_000; i++)
            log.Append(ConsoleLineKind.Warning, "шейдер не найден", At(i));

        Assert.AreEqual(2, log.Count,
            "ради этого и сделано сворачивание: одно сообщение, повторённое каждый кадр, "
            + "иначе вымело бы из ленты ровно то, что человек и пришёл искать");
        Assert.AreEqual("важное, случилось первым", log.Lines[0].Text);
    }

    [Test]
    public void Format_PutsTheTimeBeforeTheText()
    {
        var log = new ConsoleLog();
        log.Append(ConsoleLineKind.Status, "Прилипание включено (Ctrl)", At(0));

        Assert.AreEqual("[19:04:11] статус: Прилипание включено (Ctrl)",
            ConsoleLog.Format(log.Lines[0]),
            "без отметки времени непонятно, сообщение от текущего действия или "
            + "получасовой давности — а именно за этим в ленту и приходят");
    }

    [Test]
    public void EveryKind_CarriesItsLevelInTheTEXT_NotOnlyInColour()
    {
        var kinds = Enum.GetValues(typeof(ConsoleLineKind)).Cast<ConsoleLineKind>().ToArray();
        var prefixes = kinds.Select(ConsoleLog.Prefix).ToArray();

        CollectionAssert.AllItemsAreUnique(prefixes,
            "docs/UI-GUIDELINES.md §9: цвет не может быть единственным носителем смысла. "
            + "Два вида с одинаковым префиксом различались бы только краской — то есть ни "
            + "на монохромном скриншоте, ни у человека с дальтонизмом");

        foreach (var kind in kinds.Where(ConsoleLog.IsStatus))
            StringAssert.StartsWith("статус", ConsoleLog.Prefix(kind),
                "строку состояния должно быть видно среди строк журнала Unity и наоборот: "
                + "они соседи в одной ленте");
    }

    [Test]
    public void EveryKind_IsClassifiedAsStatusOrNot()
    {
        var kinds = Enum.GetValues(typeof(ConsoleLineKind)).Cast<ConsoleLineKind>().ToArray();

        Assert.AreEqual(3, kinds.Count(k => !ConsoleLog.IsStatus(k)),
            "журнал Unity: Log, Warning, Error");
        Assert.AreEqual(4, kinds.Count(ConsoleLog.IsStatus),
            "строка состояния: Info, Success, Warning, Error. Новый вид обязан попасть в "
            + "IsStatus осознанно — иначе он молча окажется «журналом Unity» и потеряет "
            + "свой префикс");
    }

    [Test]
    public void Tail_ReturnsTheLastLinesOnly_AndInOrder()
    {
        var log = new ConsoleLog();
        for (int i = 0; i < 10; i++) log.Append(ConsoleLineKind.Log, "l" + i, At(i));

        var tail = log.Tail(3);

        StringAssert.DoesNotContain("l6", tail, "запрошены три последние строки, не четыре");
        Assert.Less(tail.IndexOf("l7", StringComparison.Ordinal),
            tail.IndexOf("l9", StringComparison.Ordinal),
            "хвост отдаётся в хронологическом порядке, самое свежее — внизу");
    }

    [Test]
    public void Tail_OfAnEmptyLog_IsEmpty()
    {
        Assert.AreEqual(string.Empty, new ConsoleLog().Tail(32));
        Assert.AreEqual(string.Empty, new ConsoleLog().Tail(0));
    }

    [Test]
    public void Revision_MovesOnEveryAppend_IncludingACollapsedRepeat()
    {
        var log = new ConsoleLog();
        int atStart = log.Revision;

        log.Append(ConsoleLineKind.Status, "одно и то же", At(0));
        int afterFirst = log.Revision;
        log.Append(ConsoleLineKind.Status, "одно и то же", At(1));

        Assert.AreNotEqual(atStart, afterFirst);
        Assert.AreNotEqual(afterFirst, log.Revision,
            "свёрнутый повтор МЕНЯЕТ картинку (появляется «×2» и новое время), поэтому "
            + "ревизия обязана сдвинуться — иначе открытая консоль не перерисуется и "
            + "покажет вчерашний счётчик");
    }
}
