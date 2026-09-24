using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;
using KitchenDesigner.Core.Construction;

/// <summary>Лента идёт по осевым несущих стен (docs/todo_evolution.md §3.6, «Осевая как
/// источник длины ленты»). Наивный способ посчитать общую длину — сложить длины всех стен
/// по отдельности — переоценивает контур ровно там, где обход графа доходит до угла и не
/// останавливается: круг из четырёх стен обошёл бы себя дважды, если бы траектория не
/// замечала возврат в стартовую точку. Поэтому три теста ниже — не про то, что сумма
/// «выглядит похожей», а про три топологии, которые ломают наивный обход по-разному:
/// открытая ломаная (Г-образный дом), закрытый контур (обход обязан остановиться, дойдя
/// до старта) и развилка (три стены в одной точке — это не пара «конец-в-конец», значит
/// не может лечь в одну простую полилинию).</summary>
public class FoundationLayoutTests
{
    // Quaternion.Euler is an ECall (conventions/STRUCTURE.md → "A class without a scene lives
    // on the fast path") and does not compile under CoreCLR; the equivalent 180° turn around Y
    // written as raw components (x,y,z,w) is plain data and does.
    private static readonly Quaternion Rotate180AroundY = new Quaternion(0f, 1f, 0f, 0f);

    private static WallCentreline Wall(Vector3 centerUnits, Quaternion rotation,
        int lengthAxisXmm, int lengthAxisZmm) =>
        WallCentreline.Of(centerUnits, rotation, new Vector3Int(lengthAxisXmm, 2700, lengthAxisZmm));

    [Test]
    public void TotalLengthMm_LShape_4000Plus3000_Is7000mm()
    {
        var a = Wall(new Vector3(2f, 0f, 0f), Quaternion.identity, 4000, 250);
        var b = Wall(new Vector3(4f, 0f, 1.5f), Quaternion.identity, 250, 3000);

        float total = FoundationLayout.TotalLengthMm(new[] { a, b });

        Assert.AreEqual(7000f, total, 0.5f,
            "Г-образный дом: два прогона ленты 4000 и 3000 мм встречаются в одной точке "
            + "(4000, 0) — 4000 + 3000 = 7000 мм, посчитано руками");
    }

    [Test]
    public void MergeIntoPolylines_ClosedBox_2x4000Plus2x3000_Is14000mm_NotDoubledAtTheCorners()
    {
        var a = Wall(new Vector3(2f, 0f, 0f), Quaternion.identity, 4000, 250);
        var b = Wall(new Vector3(4f, 0f, 1.5f), Quaternion.identity, 250, 3000);
        var c = Wall(new Vector3(2f, 0f, 3f), Rotate180AroundY, 4000, 250);
        var d = Wall(new Vector3(0f, 0f, 1.5f), Rotate180AroundY, 250, 3000);
        var walls = new[] { a, b, c, d };

        float total = FoundationLayout.TotalLengthMm(walls);

        Assert.AreEqual(14000f, total, 0.5f,
            "замкнутый прямоугольник 4000×3000 мм: периметр 2×(4000+3000) = 14000 мм, "
            + "посчитано руками. Каждый из четырёх углов делят ровно две стены — обход, "
            + "который не замечает возврат в стартовый узел, обошёл бы контур второй раз "
            + "и досчитал бы все четыре стены дважды (28000 мм)");

        var polylines = FoundationLayout.MergeIntoPolylines(walls);
        Assert.AreEqual(1, polylines.Count,
            "замкнутый контур без ответвлений — одна полилиния, а не четыре разрозненных "
            + "отрезка");
        Assert.AreEqual(5, polylines[0].Points.Count,
            "четыре угла плюс возврат в стартовую точку, которым обход закрывает петлю, — "
            + "пять точек одной замкнутой полилинии");
    }

    [Test]
    public void MergeIntoPolylines_TJunction_4000Plus3000Plus2000_Is9000mm_AsThreeRays()
    {
        var a = Wall(new Vector3(2f, 0f, 0f), Quaternion.identity, 4000, 250);
        var b = Wall(new Vector3(4f, 0f, 1.5f), Quaternion.identity, 250, 3000);
        var e = Wall(new Vector3(4f, 0f, -1f), Rotate180AroundY, 250, 2000);
        var walls = new[] { a, b, e };

        float total = FoundationLayout.TotalLengthMm(walls);

        Assert.AreEqual(9000f, total, 0.5f,
            "три прогона сходятся в одной точке (4000, 0): 4000 + 3000 + 2000 = 9000 мм, "
            + "посчитано руками");

        var polylines = FoundationLayout.MergeIntoPolylines(walls);
        Assert.AreEqual(3, polylines.Count,
            "узел, где сходятся три стены, — не пара «конец-в-конец»: три ребра не могут "
            + "лечь в одну простую полилинию без повторного прохода через узел, поэтому "
            + "правильный результат — три луча, каждый из одной стены");
    }

    [Test]
    public void TotalLengthMm_EmptyList_IsZero()
    {
        Assert.AreEqual(0f, FoundationLayout.TotalLengthMm(System.Array.Empty<WallCentreline>()));
    }

    [Test]
    public void TotalLengthMm_IgnoresAnUndefinedCentreline()
    {
        var a = Wall(new Vector3(2f, 0f, 0f), Quaternion.identity, 4000, 250);
        var undefined = default(WallCentreline);

        float total = FoundationLayout.TotalLengthMm(new[] { a, undefined });

        Assert.AreEqual(4000f, total, 0.5f,
            "WallCentreline.IsDefined=false — вырожденная стена (нулевая длина или ось, "
            + "которая не разворачивается в плане); она не обязана портить сумму соседних "
            + "стен, поэтому вклад в длину — ноль, а не NaN и не исключение");
    }
}
