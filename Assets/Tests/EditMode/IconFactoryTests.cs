using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core.UI;

public class IconFactoryTests
{
    private const int Size = 64;

    private static Color32[] Pixels(Sprite sprite) => sprite.texture.GetPixels32();

    private static bool HasInk(Color32[] px, int x, int y) => px[y * Size + x].a > 0;

    private static IEnumerable<(int x, int y)> InkPixels(Sprite sprite)
    {
        var px = Pixels(sprite);
        for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
                if (HasInk(px, x, y))
                    yield return (x, y);
    }

    private static int InkIn(Sprite sprite, int x0, int x1, int y0, int y1) =>
        InkPixels(sprite).Count(p => p.x >= x0 && p.x <= x1 && p.y >= y0 && p.y <= y1);

    [Test]
    public void IconFactory_Warning_IsDrawnWhite_SoTheCallerCanTintIt()
    {
        var px = Pixels(IconFactory.Warning);
        var tinted = px.Where(p => p.a > 0)
            .Where(p => p.r != 255 || p.g != 255 || p.b != 255)
            .ToList();

        Assert.IsNotEmpty(px.Where(p => p.a > 0).ToList(), "иконка пустая — тест бы зеленел впустую");
        Assert.IsEmpty(tinted,
            "Треугольник предупреждения рисуется БЕЛЫМ, в отличие от прочих иконок: цвет ему "
            + "задаёт вызывающий через Image.color — красный при ошибках, оранжевый при "
            + "предупреждениях, серый когда кнопка неактивна. Собственный цвет в пикселях "
            + "перекрасить нечем");
    }

    [Test]
    public void IconFactory_FindIssue_IsDrawnWhite_SoTheCallerCanTintIt()
    {
        var px = Pixels(IconFactory.FindIssue);
        var tinted = px.Where(p => p.a > 0)
            .Where(p => p.r != 255 || p.g != 255 || p.b != 255)
            .ToList();

        Assert.IsNotEmpty(px.Where(p => p.a > 0).ToList(), "иконка пустая — тест бы зеленел впустую");
        Assert.IsEmpty(tinted,
            "«Перейти к первой проблеме» перекрашивается тем же способом, что и «Ошибки» — "
            + "красным при ошибках, оранжевым при предупреждениях, серым когда неактивна — "
            + "поэтому рисуется белым, а не своим цветом");
    }

    [Test]
    public void IconFactory_Warning_And_FindIssue_AreDifferentSilhouettes()
    {
        var warningInk = new HashSet<(int x, int y)>(InkPixels(IconFactory.Warning));
        var findIssueInk = InkPixels(IconFactory.FindIssue).ToList();

        int overlap = findIssueInk.Count(p => warningInk.Contains(p));

        Assert.Less(overlap, findIssueInk.Count / 2,
            "«Ошибки» и «Перейти к первой проблеме» раньше делили один и тот же треугольник и "
            + "отличались только tooltip'ом и бейджем — теперь форма обязана их различать сама "
            + "(доступность, LEAD-AGENT.md §2): треугольник предупреждения против лупы поиска");
    }

    [Test]
    public void IconFactory_HandleModeIcons_AreDifferentShapes_NotJustColor()
    {
        var resizeInk = new HashSet<(int x, int y)>(InkPixels(IconFactory.ResizeHandles));
        var moveInk = InkPixels(IconFactory.MoveHandles).ToList();

        int overlap = moveInk.Count(p => resizeInk.Contains(p));

        Assert.Less(overlap, moveInk.Count / 2,
            "«Ручки: растяжение» (квадраты-хваталки по диагонали) и «Ручки: перенос» "
            + "(четырёхлучевая стрелка) обязаны различаться формой — с уходом текста именно "
            + "форма теперь несёт режим");
    }

    [Test]
    public void IconFactory_Undo_PutsTheArrowheadOnTheLeft_Redo_OnTheRight()
    {
        const int aboveTheArc = 21, belowTheArc = 27;

        Assert.Greater(InkIn(IconFactory.Undo, 0, 31, aboveTheArc, belowTheArc), 0,
            "Отмена — наконечник свисает с ЛЕВОГО конца дуги");
        Assert.AreEqual(0, InkIn(IconFactory.Undo, 32, 63, aboveTheArc, belowTheArc),
            "у отмены справа от дуги ничего не свисает");
        Assert.Greater(InkIn(IconFactory.Redo, 32, 63, aboveTheArc, belowTheArc), 0,
            "Повтор — тот же значок зеркально: наконечник с ПРАВОГО конца");
        Assert.AreEqual(0, InkIn(IconFactory.Redo, 0, 31, aboveTheArc, belowTheArc),
            "у повтора слева от дуги ничего не свисает");
    }

    [Test]
    public void IconFactory_Caret_IsVerticallyCentred_NotSittingOnATextBaseline()
    {
        foreach (var (name, caret) in new[] { ("вверх", IconFactory.CaretUp), ("вниз", IconFactory.CaretDown) })
        {
            var ink = InkPixels(caret).ToList();
            int bottomGap = ink.Min(p => p.y);
            int topGap = Size - 1 - ink.Max(p => p.y);

            Assert.LessOrEqual(Mathf.Abs(bottomGap - topGap), 2,
                "Стрелка порядка «" + name + "» — спрайт, а не текстовый глиф, именно потому, "
                + "что в кнопке строки списка высотой 13 px символ упирался бы в базовую линию. "
                + "Треугольник обязан стоять по центру: поля " + bottomGap + " снизу и "
                + topGap + " сверху");
        }
    }

    [Test]
    public void IconFactory_Pencil_RunsAlongTheDiagonal_Eyedropper_StaysUpright()
    {
        Assert.Greater(InkIn(IconFactory.Pencil, 0, 23, 0, 23), 0, "грифель карандаша внизу слева");
        Assert.Greater(InkIn(IconFactory.Pencil, 40, 63, 40, 63), 0, "обух карандаша вверху справа");
        Assert.AreEqual(0, InkIn(IconFactory.Pencil, 0, 23, 40, 63),
            "карандаш идёт по диагонали, второй угол пуст");

        var offCentre = InkPixels(IconFactory.Eyedropper).Where(p => Mathf.Abs(p.x - 32) > 14).ToList();
        Assert.IsEmpty(offCentre,
            "Пипетка рисуется ВЕРТИКАЛЬНОЙ, а не диагональной как карандаш: рядом в тулбаре "
            + "две диагональные иконки было бы не отличить одну от другой. Вышло за колонку: "
            + offCentre.Count + " пикселей");
    }

    [Test]
    public void IconFactory_Ruler_IsAWideBar_NotARoundTapeCase()
    {
        var ink = InkPixels(IconFactory.Ruler).ToList();
        int width = ink.Max(p => p.x) - ink.Min(p => p.x);
        int height = ink.Max(p => p.y) - ink.Min(p => p.y);

        Assert.Greater(width, height * 2,
            "«Рулетка» рисуется линейкой, а не круглым корпусом с лентой: в кнопке 28 px от "
            + "корпуса осталось бы неразличимое кольцо, а насечки на широкой полосе читаются "
            + "как «мерить». Габарит чернил " + width + "×" + height);
    }

    [Test]
    public void IconFactory_SidebarGroupIcons_AreAllNonEmpty()
    {
        foreach (var (name, icon) in SidebarGroupIcons())
            Assert.IsNotEmpty(InkPixels(icon).ToList(), name + " — иконка группы не должна быть пустой");
    }

    /// <summary>Пять из семи иконок ниже намеренно занимают РАЗНЫЕ углы холста
    /// 64×64, а не общий прямоугольник «корпус» посередине: при общей рамке
    /// силуэты почти целиком совпадают и этот тест красит любую пару.</summary>
    [Test]
    public void IconFactory_SidebarGroupIcons_AreDistinctSilhouettes()
    {
        var icons = SidebarGroupIcons();
        for (int i = 0; i < icons.Count; i++)
        {
            var inkA = new HashSet<(int x, int y)>(InkPixels(icons[i].icon));
            for (int j = i + 1; j < icons.Count; j++)
            {
                var inkB = InkPixels(icons[j].icon).ToList();
                int overlap = inkB.Count(p => inkA.Contains(p));
                Assert.Less(overlap, inkB.Count / 2,
                    $"«{icons[i].name}» и «{icons[j].name}» — рейка категорий сайдбара рисует "
                    + "буквы иконками (docs/todo_evolution.md, дефект D5): каждая обязана "
                    + "различаться формой, а не только оттенком чернил");
            }
        }
    }

    private static List<(string name, Sprite icon)> SidebarGroupIcons() => new()
    {
        ("Детали", IconFactory.Shelf),
        ("Фасады", IconFactory.Facade),
        ("Ящики", IconFactory.Drawer),
        ("Мебель", IconFactory.Furniture),
        ("Техника", IconFactory.Appliance),
        ("Сантехника", IconFactory.Faucet),
        ("Помещение", IconFactory.Room),
        ("Конструкции", IconFactory.Brickwork),
    };

    [Test]
    public void IconFactory_Pencil_IsDrawn_BecauseThePencilGlyphIsOutsideWgl4()
    {
        Assert.IsFalse(Wgl4CharSet.Contains('✎'),
            "Глифа карандаша (U+270E) в рантайм-атласе TMP нет — атлас собирается из "
            + "LiberationSans по WGL4, поэтому иконка рисуется, а не пишется символом");
        Assert.IsNotNull(IconFactory.Pencil, "и потому карандаш обязан существовать как спрайт");
    }
}
