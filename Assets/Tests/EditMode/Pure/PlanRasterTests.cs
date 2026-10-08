using NUnit.Framework;
using KitchenDesigner.Core.MCP;

/// <summary>Растеризатор целочисленный и свой: у него нет вывода «примерно так же», поэтому проверки точные,
/// по пикселям, и на несимметричных входах — квадрат и «I» скрыли бы перепутанные оси и зеркало.</summary>
public class PlanRasterTests
{
    private const int Ink = PlanPalette.Ink;
    private const int Paper = PlanPalette.Background;

    private static PlanCanvas Blank(int w = 40, int h = 30) => new PlanCanvas(w, h, Paper);

    private static int Count(PlanCanvas canvas, int color)
    {
        int n = 0;
        foreach (var pixel in canvas.Pixels) if (pixel == color) n++;
        return n;
    }

    [Test]
    public void FillRect_CoversExactlyTheHalfOpenRectangle()
    {
        var canvas = Blank();

        canvas.FillRect(3, 4, 10, 9, Ink);

        Assert.AreEqual(7 * 5, Count(canvas, Ink));
        Assert.AreEqual(Ink, canvas.At(3, 4));
        Assert.AreEqual(Ink, canvas.At(9, 8));
        Assert.AreEqual(Paper, canvas.At(10, 8), "правая граница не входит");
        Assert.AreEqual(Paper, canvas.At(9, 9), "нижняя граница не входит");
    }

    [Test]
    public void FillRect_PastTheEdges_IsClippedNotThrown()
    {
        var canvas = Blank(10, 10);

        canvas.FillRect(-5, -5, 4, 4, Ink);
        canvas.FillRect(8, 8, 50, 50, Ink);

        Assert.AreEqual(16 + 4, Count(canvas, Ink));
    }

    [Test]
    public void StrokeRectInside_StaysInsideTheRectangle()
    {
        var canvas = Blank();

        canvas.StrokeRectInside(5, 5, 15, 12, 2, Ink);

        Assert.AreEqual(10 * 7 - 6 * 3, Count(canvas, Ink), "рамка 2 пикселя: 70 минус внутренность 6 x 3");
        Assert.AreEqual(Paper, canvas.At(4, 8), "наружу рамка не выходит");
        Assert.AreEqual(Paper, canvas.At(10, 8), "середина не закрашена");
    }

    [Test]
    public void StrokeRectInside_AThinRectangleIsFilledSolid_NotInverted()
    {
        var canvas = Blank();

        canvas.StrokeRectInside(5, 5, 25, 7, 3, Ink);

        Assert.AreEqual(20 * 2, Count(canvas, Ink));
    }

    [Test]
    public void Text_DrawsTheGlyphBitsAtTheRightScale()
    {
        var canvas = Blank();

        canvas.DrawText(10, 5, "!", 2, Ink);

        Assert.AreEqual(Ink, canvas.At(14, 5), "палка «!» стоит в третьей колонке: 10 + 2 * 2");
        Assert.AreEqual(Ink, canvas.At(15, 14), "и занимает блок 2 x 2");
        Assert.AreEqual(Paper, canvas.At(14, 15), "шестая строка пуста");
        Assert.AreEqual(Ink, canvas.At(14, 17), "точка в седьмой строке");
        Assert.AreEqual(Paper, canvas.At(12, 5), "соседняя колонка пуста");
    }

    [Test]
    public void Text_AdvancesBySixColumnsPerCharacter()
    {
        var canvas = Blank(40, 12);

        canvas.DrawText(0, 0, "II", 1, Ink);

        Assert.AreEqual(Ink, canvas.At(2, 3), "палка первого «I»");
        Assert.AreEqual(Ink, canvas.At(8, 3), "второй знак начинается на 6 пикселей правее");
        Assert.AreEqual(Paper, canvas.At(5, 3), "между палками «I» пусто");
    }

    [TestCase(PlanTextAnchor.Start, 100)]
    [TestCase(PlanTextAnchor.Middle, 100 - 17)]
    [TestCase(PlanTextAnchor.End, 100 - 34)]
    public void AShape_PlacesItsTextByTheAnchor(PlanTextAnchor anchor, int expectedLeft)
    {
        var label = PlanShape.Label(100, 0, "abc", anchor, 2, Ink);

        Assert.AreEqual(34, label.TextWidth, "три знака по 12 минус промежуток после последнего");
        Assert.AreEqual(expectedLeft, label.TextLeft);
    }

    [Test]
    public void Triangle_PointingDownHasItsWideSideOnTop()
    {
        var canvas = Blank();

        canvas.FillTriangle(5, 5, 21, 5, 13, 17, Ink);

        int top = 0, bottom = 0;
        for (int x = 0; x < canvas.Width; x++)
        {
            if (canvas.At(x, 5) == Ink) top++;
            if (canvas.At(x, 15) == Ink) bottom++;
        }
        Assert.AreEqual(16, top, "основание в строке 5");
        Assert.Less(bottom, 5, "у вершины строка узкая");
        Assert.AreEqual(Paper, canvas.At(13, 18), "ниже вершины ничего");
    }

    [Test]
    public void Triangle_AnyWindingFillsTheSamePixels()
    {
        var one = Blank();
        var other = Blank();

        one.FillTriangle(5, 20, 25, 20, 15, 5, Ink);
        other.FillTriangle(5, 20, 15, 5, 25, 20, Ink);

        CollectionAssert.AreEqual(one.Pixels, other.Pixels);
    }

    [Test]
    public void Line_ReachesBothEndsAndStaysOnePixelWide()
    {
        var canvas = Blank();

        canvas.Line(2, 3, 30, 3, Ink);
        canvas.Line(5, 6, 5, 25, Ink);
        canvas.Line(10, 10, 20, 20, Ink);

        Assert.AreEqual(Ink, canvas.At(2, 3));
        Assert.AreEqual(Ink, canvas.At(30, 3));
        Assert.AreEqual(Paper, canvas.At(31, 3));
        Assert.AreEqual(Ink, canvas.At(5, 25));
        Assert.AreEqual(Ink, canvas.At(15, 15), "диагональ проходит через середину");
        Assert.AreEqual(Paper, canvas.At(15, 14));
    }

    [Test]
    public void Line_DrawnBackwardsMakesTheSamePixels()
    {
        var forward = Blank();
        var backward = Blank();

        forward.Line(3, 4, 27, 19, Ink);
        backward.Line(27, 19, 3, 4, Ink);

        Assert.AreEqual(Count(forward, Ink), Count(backward, Ink));
    }

    [Test]
    public void Render_PaintsShapesInTheirOrder_TheLaterOneWins()
    {
        var drawing = new PlanDrawing(20, 20, 1.0, PlanView.Top, null);
        drawing.Shapes.Add(PlanShape.Rect(0, 0, 10, 10, PlanPalette.Wall, PlanShape.NoColor, 0));
        drawing.Shapes.Add(PlanShape.Rect(5, 5, 15, 15, PlanPalette.Part, PlanPalette.Issue, 1));

        var canvas = PlanRaster.Render(drawing);

        Assert.AreEqual(PlanPalette.Wall, canvas.At(2, 2));
        Assert.AreEqual(PlanPalette.Part, canvas.At(8, 8));
        Assert.AreEqual(PlanPalette.Issue, canvas.At(5, 8), "рамка красная");
        Assert.AreEqual(PlanPalette.Background, canvas.At(17, 17));
    }
}
