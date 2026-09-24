using NUnit.Framework;
using KitchenDesigner.Core.Construction;

/// <summary>Сечение стропильной ноги в СП 64.13330.2017 — результат расчёта по нагрузке
/// (снеговой район, шаг, порода древесины), а не готовая таблица «пролёт → сечение»; текст
/// норматива исполнителю недоступен, точный пункт не подтверждён. Таблица ниже — практический
/// ряд сечений по пролёту стропильной ноги, который встречается в справочниках малоэтажного
/// строительства. По правилу docs/todo_evolution.md §3.6 («если исполнителю недоступен текст
/// СП — он пишет таблицу с пунктом, который нашёл, и помечает тест
/// [Category("NormativeUnverified")]») весь класс помечен этой категорией: менеджер сверяет
/// цифры сам, пользователя не спрашивать.</summary>
[Category("NormativeUnverified")]
public class RafterSectionTableTests
{
    [Test]
    public void RafterSectionTable_ForSpan_GrowsWithSpan_NeverShrinks()
    {
        var short3000 = RafterSectionTable.ForSpan(3000f);
        var mid4500 = RafterSectionTable.ForSpan(4500f);
        var long6000 = RafterSectionTable.ForSpan(6000f);

        Assert.AreEqual(50f, short3000.WidthMm, 1e-3f);
        Assert.AreEqual(150f, short3000.HeightMm, 1e-3f,
            "пролёт 3 000 мм — первая строка таблицы, 50×150 мм");
        Assert.AreEqual(200f, mid4500.HeightMm, 1e-3f, "пролёт 4 500 мм — вторая строка, 50×200 мм");
        Assert.AreEqual(250f, long6000.HeightMm, 1e-3f, "пролёт 6 000 мм — третья строка, 50×250 мм");

        Assert.LessOrEqual(short3000.HeightMm, mid4500.HeightMm,
            "больше пролёт — не меньшее сечение, монотонность ряда");
        Assert.LessOrEqual(mid4500.HeightMm, long6000.HeightMm);
    }

    [Test]
    public void RafterSectionTable_ForSpan_AtTheBoundary_TakesTheSmallerSection()
    {
        var atBoundary = RafterSectionTable.ForSpan(3000f);
        var justOver = RafterSectionTable.ForSpan(3001f);

        Assert.AreEqual(150f, atBoundary.HeightMm, 1e-3f,
            "ровно 3 000 мм ещё укладывается в первую строку — <= включает границу");
        Assert.AreEqual(200f, justOver.HeightMm, 1e-3f,
            "3 001 мм — уже следующая строка: 1 мм не должен быть прощён");
    }

    [Test]
    public void RafterSectionTable_ForSpan_BeyondTheLastRow_TakesTheLargestSectionInstead()
    {
        var farBeyond = RafterSectionTable.ForSpan(20000f);

        Assert.AreEqual(250f, farBeyond.HeightMm, 1e-3f,
            "таблица не растёт бесконечно — пролёт за её пределами получает самое крупное "
            + "из известных сечений, а не отказ и не наименьшее сечение, которое бы недосчитало");
    }
}
