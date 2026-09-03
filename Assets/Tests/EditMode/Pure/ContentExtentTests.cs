using System.Collections.Generic;
using KitchenDesigner.Core.UI;
using NUnit.Framework;

/// <summary>Единственное описание контура «сколько места занимает содержимое».
///
/// Дважды одна и та же болезнь: каталог сайдбара перерос корень, потому что
/// позиции строк считал <c>Place</c>, а высоту — зашитая константа 960; окно
/// настроек уехало за нижний край, потому что панель фиксированной высоты 900,
/// а вкладка «Фото режим» набрала 27 строк по 38 px. В обоих случаях врало НЕ
/// размещение, а ВТОРОЕ описание того же контура — высота, которую никто не
/// пересчитывал.
///
/// Поэтому <see cref="ContentExtent.Measure"/> — одна функция, и она отдаёт
/// сразу всё: высоту содержимого, видимую высоту, переполнение и ИМЯ самого
/// нижнего узла. Ею же меряет и <c>WindowBody</c>, когда подгоняет область
/// прокрутки, и сторож <c>WindowOverflowGuardTests</c>, когда ищет окна без
/// прокрутки. Разойтись им негде — считалка одна.
///
/// Отсюда форма проверок: ожидаемую высоту здесь ВСЕГДА пересчитывают
/// независимо от Measure — суммой строк, — и сравнивают с тем, что Measure
/// вернул. Вернуть константу вместо суммы значит покраснеть.</summary>
public class ContentExtentTests
{
    private const float RowH = 38f;

    private static ContentSpan Row(string name, float top, float height) =>
        new ContentSpan(name, top, top + height);

    private static List<ContentSpan> Stack(int rows)
    {
        var spans = new List<ContentSpan>(rows);
        for (int i = 0; i < rows; i++) spans.Add(Row("Row" + i, i * RowH, RowH));
        return spans;
    }

    private static float LowestBottom(IReadOnlyList<ContentSpan> spans)
    {
        float lowest = 0f;
        foreach (var span in spans)
            if (span.Bottom > lowest) lowest = span.Bottom;
        return lowest;
    }

    [Test]
    public void Measure_EmptyRegion_HasNoContentAndNoOverflow()
    {
        var fit = ContentExtent.Measure(new List<ContentSpan>(), 12f, 100f);

        Assert.AreEqual(0f, fit.ContentHeight,
            "пустая область не имеет высоты — нижний отступ не должен браться из воздуха, "
            + "иначе пустое окно объявит себя переполненным");
        Assert.IsFalse(fit.Overflows, "пустой области нечем переполниться");
    }

    [Test]
    public void Measure_ManyRows_MatchesTheSumOfTheRowsAndNotAConstant()
    {
        var spans = Stack(27);

        var fit = ContentExtent.Measure(spans, 0f, 696f);

        Assert.AreEqual(LowestBottom(spans), fit.ContentHeight, 0.001f,
            "высота содержимого обязана быть пересчитана по строкам, а не взята из константы: "
            + "именно вторая, независимая копия высоты дважды похоронила нижние строки");
        Assert.AreEqual(27f * RowH, fit.ContentHeight, 0.001f,
            "27 строк по 38 px это 1026 px — столько набрала вкладка «Фото режим»");
    }

    [Test]
    public void Measure_LowestRowIsNotTheLastOne_StillDecidesTheHeight()
    {
        var spans = new List<ContentSpan> { Row("Deep", 0f, 500f), Row("Shallow", 10f, 20f) };

        var fit = ContentExtent.Measure(spans, 0f, 400f);

        Assert.AreEqual(500f, fit.ContentHeight, 0.001f,
            "контур задаёт самый нижний узел, а не последний добавленный: строки кладут "
            + "не только сверху вниз — тулбар вкладки и подпись внизу идут вперемешку");
        Assert.AreEqual("Deep", fit.Lowest.Name,
            "сторож обязан назвать виновника по имени, иначе красный тест придётся "
            + "расследовать вручную по всему дереву панели");
        Assert.AreEqual(0f, fit.Lowest.Top, 0.001f,
            "вместе с именем нужен и участок узла — по нему видно, это одна длинная строка "
            + "или строка, уехавшая вниз");
    }

    [Test]
    public void Measure_BottomPadding_LandsBelowTheLowestRow()
    {
        var spans = Stack(3);

        var fit = ContentExtent.Measure(spans, 12f, 1000f);

        Assert.AreEqual(3f * RowH + 12f, fit.ContentHeight, 0.001f,
            "нижний паддинг 12 px по п. 7 UI-GUIDELINES («ничего не прилипает») входит "
            + "в высоту содержимого, иначе последняя строка упрётся в край области");
    }

    [Test]
    public void Measure_ContentShorterThanTheViewport_DoesNotOverflow()
    {
        var fit = ContentExtent.Measure(Stack(5), 0f, 696f);

        Assert.IsFalse(fit.Overflows,
            "5 строк по 38 px влезают в 696 px видимой области настроек");
        Assert.AreEqual(0f, fit.Overflow, 0.001f,
            "переполнение непереполненного равно нулю, а не отрицательному запасу: "
            + "иначе сторож напечатает «ниже края на -500 px»");
    }

    [Test]
    public void Measure_ContentTallerThanTheViewport_OverflowsByTheDifference()
    {
        var fit = ContentExtent.Measure(Stack(27), 0f, 696f);

        Assert.IsTrue(fit.Overflows,
            "27 строк это 1026 px — вкладка «Фото режим» в окне 900 px не помещается, "
            + "и это ровно та жалоба, ради которой писан сторож");
        Assert.AreEqual(1026f - 696f, fit.Overflow, 0.001f,
            "сторож сообщает НАСКОЛЬКО вылезли: «не помещается» без числа не даёт понять, "
            + "спрятана одна строка или десять");
    }

    [Test]
    public void Measure_ContentTallerByLessThanTheSlack_DoesNotOverflow()
    {
        var spans = new List<ContentSpan> { Row("Hair", 0f, 100f + ContentExtent.SlackPx) };

        var fit = ContentExtent.Measure(spans, 0f, 100f);

        Assert.IsFalse(fit.Overflows,
            "полпикселя — это округление RectTransform, а не спрятанная строка: без допуска "
            + "сторож краснел бы на каждом нечётном размере панели");
    }

    [Test]
    public void Measure_JustOverTheSlack_Overflows()
    {
        var spans = new List<ContentSpan> { Row("Row", 0f, 100f + ContentExtent.SlackPx + 0.01f) };

        var fit = ContentExtent.Measure(spans, 0f, 100f);

        Assert.IsTrue(fit.Overflows,
            "допуск обязан быть узкой щелью, а не дырой: сразу за SlackPx сторож краснеет. "
            + "Без этой пары предыдущий тест разрешал бы любой допуск, хоть 1000 px");
    }

    [Test]
    public void Measure_RowsAboveTheTopEdge_DoNotShrinkTheContent()
    {
        var spans = new List<ContentSpan> { Row("Above", -80f, 40f), Row("Body", 0f, 100f) };

        var fit = ContentExtent.Measure(spans, 0f, 200f);

        Assert.AreEqual(100f, fit.ContentHeight, 0.001f,
            "заголовок, привязанный выше верхнего края области, не укорачивает содержимое: "
            + "иначе шапка окна вычла бы из высоты списка ровно свою высоту");
    }

    [Test]
    public void ToString_OnOverflow_NamesTheLowestNodeAndTheGap()
    {
        var fit = ContentExtent.Measure(Stack(27), 0f, 696f);

        string text = fit.ToString();

        Assert.IsTrue(text.Contains("Row26"),
            "текст диагноза обязан содержать имя нижнего узла — сторож печатает именно его, "
            + "и другого способа найти виновника в дереве из сотен RectTransform нет: " + text);
        Assert.IsTrue(text.Contains("330"),
            "и величину переполнения 1026-696=330 px: " + text);
    }

    [Test]
    public void ToString_WhenItFits_SaysNothingAboutOverflow()
    {
        string text = ContentExtent.Measure(Stack(5), 0f, 696f).ToString();

        Assert.IsFalse(text.Contains("ниже края"),
            "зелёный случай не должен печатать слова про переполнение — иначе они попадут "
            + "в лог рядом с зелёным тестом и обесценятся: " + text);
    }
}
