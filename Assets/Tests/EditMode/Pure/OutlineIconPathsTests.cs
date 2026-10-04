using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using KitchenDesigner.Core.UI;
using NUnit.Framework;
using UnityEngine;

/// <summary>Один набор иконок тулбара и каталога (D11): контур 1,6 на сетке 20. Сторож на то,
/// что аудит нашёл руками: «гаечный ключ ~12 px, шестерёнка 24, корзина 16» — иконки разного
/// размера в одном ряду. Здесь размер каждой иконки меряется по её же путям.</summary>
public class OutlineIconPathsTests
{
    private static IEnumerable<string> Names => OutlineIconPaths.Table.Keys;

    private static List<Vector2> Points(string name) =>
        StrokePath.Parse(OutlineIconPaths.Table[name]).SelectMany(s => s).ToList();

    private static IEnumerable<string> ConstantNames() =>
        typeof(OutlineIconPaths).GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.IsLiteral && f.FieldType == typeof(string))
            .Select(f => (string)f.GetRawConstantValue()!);

    [Test]
    public void EveryNamedIcon_HasAPath_AndEveryPathHasAName()
    {
        var named = new HashSet<string>(ConstantNames());
        var tabled = new HashSet<string>(OutlineIconPaths.Table.Keys);

        Assert.IsNotEmpty(named, "сканирование констант ничего не нашло — проверять было бы нечего");
        CollectionAssert.IsEmpty(named.Except(tabled), "имя без пути: иконка просится на экране и падает");
        CollectionAssert.IsEmpty(tabled.Except(named), "путь без имени: иконку нечем заказать, строка лежит мёртвым грузом");
    }

    [Test]
    public void EveryToolbarIcon_IsInTheTable()
    {
        foreach (var name in OutlineIconPaths.ToolbarNames)
            Assert.IsTrue(OutlineIconPaths.Table.ContainsKey(name), name);
    }

    [TestCaseSource(nameof(Names))]
    public void EveryIcon_IsDrawnInsideTheGrid_WithRoomForItsStroke(string name)
    {
        float margin = OutlineIconPaths.StrokeWidth * 0.5f - 0.01f;
        foreach (var p in Points(name))
        {
            Assert.GreaterOrEqual(p.x, margin, name + ": штрих уходит за левый край сетки");
            Assert.GreaterOrEqual(p.y, margin, name + ": штрих уходит за верхний край сетки");
            Assert.LessOrEqual(p.x, OutlineIconPaths.GridSize - margin, name + ": штрих уходит за правый край сетки");
            Assert.LessOrEqual(p.y, OutlineIconPaths.GridSize - margin, name + ": штрих уходит за нижний край сетки");
        }
    }

    [TestCaseSource(nameof(Names))]
    public void EveryIcon_FillsItsGridLikeItsNeighbours_NotAMiteInsideIt(string name)
    {
        var pts = Points(name);
        float width = pts.Max(p => p.x) - pts.Min(p => p.x);
        float height = pts.Max(p => p.y) - pts.Min(p => p.y);

        Assert.GreaterOrEqual(System.Math.Max(width, height), 12f,
            name + ": иконка крупнее 12 единиц сетки по большей стороне — иначе повторится «ключ вдвое мельче соседей»");
    }

    [TestCaseSource(nameof(Names))]
    public void EveryIcon_RendersRealInk(string name)
    {
        int side = (int)(OutlineIconPaths.GridSize * OutlineIconPaths.TexelsPerGridUnit);
        var alpha = StrokeRaster.Alpha(StrokePath.Parse(OutlineIconPaths.Table[name]),
            OutlineIconPaths.GridSize, side, OutlineIconPaths.StrokeWidth);

        int ink = alpha.Count(a => a > 127);
        Assert.Greater(ink, 250, name + ": чернил почти нет — путь пуст или вырожден");
        Assert.Less(ink, side * side * 2 / 5, name + ": закрашено больше 40% холста — это пятно, а не контурная иконка (плотные контуры вроде кирпичной кладки дают 35%)");
    }

    [Test]
    public void TheStroke_IsTheDesignSystemOnePointSix_OnA20Grid()
    {
        Assert.AreEqual(1.6f, OutlineIconPaths.StrokeWidth, "D11: контур 1,6 px");
        Assert.AreEqual(20f, OutlineIconPaths.GridSize, "D11: сетка 20");
    }

    [Test]
    public void SidebarCategoryIcons_AreAllDistinctPaths()
    {
        var paths = ConstantNames().Where(n => n.StartsWith("Category")).Select(n => OutlineIconPaths.Table[n]).ToList();

        Assert.AreEqual(8, paths.Count, "восемь категорий каталога");
        Assert.AreEqual(paths.Count, paths.Distinct().Count());
    }

    [Test]
    public void ToolbarIcons_AreAllDistinctPaths()
    {
        var paths = OutlineIconPaths.ToolbarNames.Select(n => OutlineIconPaths.Table[n]).ToList();

        Assert.AreEqual(paths.Count, paths.Distinct().Count(), "две кнопки с одной и той же картинкой не различить");
    }
}
