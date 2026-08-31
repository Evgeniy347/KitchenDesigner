using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

/// <summary>Детенты вдоль грани: на что деталь встаёт, когда её подводят к
/// соседу — на его кромку, на его центр или на паз в стене. `EdgeDetents` не
/// имел ни одного собственного теста: он исполнялся насквозь через прилипание,
/// и мутационный прогон это показал прямо — ВСЕ ПЯТЬ меток детентов заменяются
/// на пустую строку, и ни один тест не краснеет.
///
/// Метка не украшение: она уходит в лог прилипания и отвечает на вопрос «почему
/// деталь встала СЮДА». Перепутанные «кромка-» и «кромка+» или молча пустая
/// метка означают, что объяснение врёт, а сдвиг при этом верный — то есть
/// разбираться с жалобой придётся вслепую.
///
/// Второе, что стерегут эти тесты, — граница. Порог сравнивается через `&lt;=`,
/// и мутация в `&lt;` выживала: ни один тест не подавал детент РОВНО на пороге.</summary>
public class EdgeDetentsTests
{
    private const float Threshold = 0.05f;

    private static float Delta(float aMin, float aMax, float bMin, float bMax,
        out string label, List<float>? grooves = null) =>
        EdgeDetents.NearestDetentDelta(aMin, aMax, bMin, bMax, Threshold, grooves, out label);

    [Test]
    public void NearestDetentDelta_NeighbourLowerEdgeIsNearest_ShiftsToItAndSaysSo()
    {
        float d = Delta(0f, 1f, 0.01f, 3f, out string label);

        Assert.AreEqual(0.01f, d, 1e-6f, "сдвиг до нижней кромки соседа");
        Assert.AreEqual("кромка-", label,
            "метка обязана назвать ИМЕННО нижнюю кромку: по ней в логе прилипания "
            + "отличают «встал по кромке» от «встал по центру»");
    }

    [Test]
    public void NearestDetentDelta_NeighbourUpperEdgeIsNearest_ShiftsToItAndSaysSo()
    {
        float d = Delta(0f, 1f, -3f, 1.01f, out string label);

        Assert.AreEqual(0.01f, d, 1e-6f, "сдвиг до верхней кромки соседа");
        Assert.AreEqual("кромка+", label,
            "верхняя и нижняя кромки — разные детенты, и путать их нельзя: "
            + "деталь встанет на другой край соседа");
    }

    [Test]
    public void NearestDetentDelta_CentresAreNearest_ShiftsToTheCentreAndSaysSo()
    {
        float d = Delta(0f, 1f, -1f, 2.02f, out string label);

        Assert.AreEqual(0.01f, d, 1e-6f, "сдвиг до совмещения центров");
        Assert.AreEqual("центр", label,
            "выравнивание по центру — отдельный детент, и в логе оно должно "
            + "называться иначе, чем выравнивание по кромке");
    }

    [Test]
    public void NearestDetentDelta_GrooveCoordinate_IsADetentOfItsOwn_ForBothEdges()
    {
        float toLower = Delta(0f, 1f, 10f, 11f, out string lowerLabel,
            new List<float> { 0.01f });
        Assert.AreEqual(0.01f, toLower, 1e-6f, "нижняя кромка встаёт в паз");
        Assert.AreEqual("паз-", lowerLabel,
            "паз — не кромка соседа: деталь заводится в стену, и лог обязан "
            + "различать эти два случая");

        float toUpper = Delta(0f, 1f, 10f, 11f, out string upperLabel,
            new List<float> { 1.01f });
        Assert.AreEqual(0.01f, toUpper, 1e-6f, "верхняя кромка встаёт в паз");
        Assert.AreEqual("паз+", upperLabel,
            "в паз можно завести любой из двух краёв, и это разные детенты");
    }

    [Test]
    public void NearestDetentDelta_NothingWithinTheThreshold_LeavesThePartFree()
    {
        float d = Delta(0f, 1f, 5f, 6f, out string label);

        Assert.AreEqual(0f, d, 1e-6f, "не за что зацепиться — сдвига нет");
        Assert.AreEqual(EdgeDetents.FreeLabel, label,
            "«свободно» — это ОТВЕТ, а не отсутствие ответа: без него в логе "
            + "нельзя отличить «детента не нашлось» от «метку забыли проставить»");
    }

    [Test]
    public void NearestDetentDelta_DetentExactlyAtTheThreshold_StillCatches()
    {
        float d = Delta(0f, 1f, Threshold, 3f, out string label);

        Assert.AreEqual(Threshold, d, 1e-6f,
            "порог включающий: детент РОВНО на пороге ловится. Значение специально "
            + "взято на границе — проверка «строго меньше» выживала именно потому, "
            + "что все прочие случаи лежали заведомо внутри");
        Assert.AreEqual("кромка-", label, "и метка при этом настоящая");
    }

    [Test]
    public void NearestDetentDelta_TwoDetentsEquallyClose_KeepsTheFirstOne()
    {
        float d = Delta(0f, 1f, 0.03125f, 1.03125f, out string label);

        Assert.AreEqual(0.03125f, d, 1e-6f,
            "0,03125 — степень двойки, поэтому все три детента совпадают ТОЧНО: "
            + "на «круглом» 0,01 разность 1,01f-1f не равна 0,01f, и ничья превращается "
            + "в случайный порядок с шумом float. НЕ заменяй эти значения на более "
            + "читаемые — ничья при этом перестанет быть ничьёй, и тест продолжит "
            + "зеленеть, проверяя уже не порядок предпочтения, а случайность");
        Assert.AreEqual("кромка-", label,
            "при равном расстоянии выигрывает ПЕРВЫЙ детент в списке, а не последний: "
            + "сравнение строгое (`< bestAbs`), и замена его на `<=` перевернула бы "
            + "порядок предпочтения, оставив сдвиг тем же — то есть незаметно");
    }

    [Test]
    public void GrooveWallCoordsAlong_KeepsOnlyWallsFacingTheAxis_AndDropsDuplicates()
    {
        var walls = new[]
        {
            new Face(new Vector3(0.3f, 0, 0), Vector3.right, Vector2.one, Vector3.up, Vector3.forward),
            new Face(new Vector3(0.3f, 1, 2), Vector3.left, Vector2.one, Vector3.up, Vector3.forward),
            new Face(new Vector3(0.7f, 0, 0), Vector3.forward, Vector2.one, Vector3.up, Vector3.right),
        };

        var coords = EdgeDetents.GrooveWallCoordsAlong(walls, Vector3.right);

        CollectionAssert.AreEqual(new[] { 0.3f }, coords,
            "вдоль оси считается только стенка, ЧЬЯ НОРМАЛЬ этой оси параллельна — "
            + "перпендикулярная не задаёт координаты на ней; а две грани одного паза "
            + "(лицевая и тыльная) дают одну координату, и дубликат превратил бы "
            + "один паз в два одинаковых детента");
    }
}
