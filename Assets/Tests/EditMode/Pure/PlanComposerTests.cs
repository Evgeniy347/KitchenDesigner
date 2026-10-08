using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using KitchenDesigner.Core.MCP;

/// <summary>Сборка рисунка из дайджеста сцены. Проверяется сам смысл картинки — какая ось куда идёт, кто кого
/// закрывает, что подписано и что нет, — а не пиксели: пиксели держит снапшот SVG. Входы несимметричны нарочно:
/// на квадрате перепутанные оси, зеркало и «вверх ногами» дают тот же рисунок.</summary>
public class PlanComposerTests
{
    private static PlanDrawing Compose(DigestInput input, PlanView view = PlanView.Top, bool labels = true, int px = 512) =>
        PlanComposer.Compose(input, view, labels, px);

    private static PlanShape RectOf(PlanDrawing drawing, string name) =>
        drawing.Shapes.Single(s => s.Kind == PlanShapeKind.Rect && s.Name == name);

    private static List<PlanShape> Texts(PlanDrawing drawing) =>
        drawing.Shapes.Where(s => s.Kind == PlanShapeKind.Text).ToList();

    private static (int left, int top, int right, int bottom) Box(PlanShape text) =>
        (text.TextLeft, text.Y0, text.TextLeft + text.TextWidth, text.Y0 + PlanFont.Rows * text.Scale);

    private static DigestInput Of(params DigestEntry[] entries)
    {
        var input = new DigestInput();
        input.Entries.AddRange(entries);
        return input;
    }

    private static DigestEntry Part(string name, float x0, float y0, float z0, float x1, float y1, float z1, string? issue = null) =>
        PlanFixtures.Entry(name, "board", x0, y0, z0, x1, y1, z1, issue);

    [Test]
    public void Projection_TopDrawsXAndZ_FrontDrawsXAndY()
    {
        var input = Of(Part("Slab", 0, 0, 0, 1000, 200, 500));

        double top = Ratio(RectOf(Compose(input, PlanView.Top), "Slab"));
        double front = Ratio(RectOf(Compose(input, PlanView.Front), "Slab"));

        Assert.AreEqual(1000.0 / 500.0, top, 0.1, "сверху видны ширина и глубина");
        Assert.AreEqual(1000.0 / 200.0, front, 0.25, "спереди видны ширина и высота");
    }

    [Test]
    public void Projection_BothViewsPutGrowingXRightAndGrowingZOrYUp()
    {
        var input = Of(Part("Near", 0, 0, 0, 100, 100, 100), Part("Far", 900, 900, 900, 1000, 1000, 1000));

        var top = Compose(input, PlanView.Top);
        var front = Compose(input, PlanView.Front);

        Assert.Greater(RectOf(top, "Near").Y0, RectOf(top, "Far").Y0, "сверху: большая Z выше по картинке, север сверху, как в preview_floorplan");
        Assert.Greater(RectOf(front, "Near").Y0, RectOf(front, "Far").Y0, "спереди: большая Y выше по картинке");
        Assert.Less(RectOf(top, "Near").X0, RectOf(top, "Far").X0);
        Assert.Less(RectOf(front, "Near").X0, RectOf(front, "Far").X0);
    }

    [TestCase(256)]
    [TestCase(512)]
    [TestCase(1024)]
    [TestCase(2048)]
    public void TheLongerSideIsExactlyPx_AWideSceneAndATallOne(int px)
    {
        var wide = Compose(PlanFixtures.Kitchen(), PlanView.Top, true, px);
        var tall = Compose(Of(Part("Rail", 0, 0, 0, 500, 100, 4000)), PlanView.Top, true, px);

        Assert.AreEqual(px, wide.Width);
        Assert.Less(wide.Height, px);
        Assert.AreEqual(px, tall.Height);
        Assert.Less(tall.Width, px);
    }

    [Test]
    public void ANarrowerScope_ZoomsIn()
    {
        var whole = Compose(PlanFixtures.Kitchen());
        var one = Compose(Of(PlanFixtures.Entry("Cab01", "board", 0, 0, 0, 600, 720, 560)));

        Assert.Less(one.MmPerPixel * 3, whole.MmPerPixel, "один шкаф занимает картинку целиком, а не угол пустой сцены");
    }

    [Test]
    public void EveryEntryIsDrawnOnce_UnderTheNameThatDescribeSceneUses()
    {
        var input = PlanFixtures.Kitchen();

        foreach (var view in new[] { PlanView.Top, PlanView.Front })
        {
            var drawn = Compose(input, view).Shapes.Where(s => s.Kind == PlanShapeKind.Rect && s.Name != null)
                .Select(s => s.Name).ToList();

            CollectionAssert.AreEquivalent(input.Entries.Select(e => e.Name).ToList(), drawn);
        }
    }

    [Test]
    public void ALabelStartsWithTheNameItIsAbout_AndSitsOnItsRectangle()
    {
        var drawing = Compose(PlanFixtures.Kitchen());

        var label = Texts(drawing).Single(t => t.Text == "Table1");

        var rect = RectOf(drawing, "Table1");
        Assert.AreEqual((rect.X0 + rect.X1) / 2, label.X0, "по центру детали");
        Assert.Greater(label.Y0, rect.Y0);
        Assert.Less(label.Y0 + PlanFont.Rows * label.Scale, rect.Y1);
    }

    [Test]
    public void LabelsOff_DrawsNoNameAtAll_ButKeepsTheScaleAndTheAxes()
    {
        var input = PlanFixtures.Kitchen();

        var drawing = Compose(input, PlanView.Top, labels: false);

        var words = Texts(drawing).Select(t => t.Text).ToList();
        CollectionAssert.IsEmpty(words.Where(w => input.Entries.Any(e => w.EndsWith(e.Name))), "ни одной подписи детали");
        CollectionAssert.Contains(words, "TOP");
        CollectionAssert.Contains(words, "X");
        CollectionAssert.Contains(words, "Z");
        Assert.That(words.Any(w => w.EndsWith(" mm")), "линейка с подписью остаётся");
        CollectionAssert.IsEmpty(drawing.Unlabelled, "без подписей нечего недосказывать");
        StringAssert.Contains("labels off", PlanCaption.Of(drawing));
    }

    [Test]
    public void TheTexts_NeverOverlapEachOther_AndNeverLeaveThePicture()
    {
        foreach (var view in new[] { PlanView.Top, PlanView.Front })
        {
            var drawing = Compose(PlanFixtures.Kitchen(), view);
            var texts = Texts(drawing);

            for (int i = 0; i < texts.Count; i++)
            {
                var a = Box(texts[i]);
                Assert.That(a.left >= 0 && a.top >= 0 && a.right <= drawing.Width && a.bottom <= drawing.Height,
                    view + ": «" + texts[i].Text + "» вышла за картинку");
                for (int j = i + 1; j < texts.Count; j++)
                {
                    var b = Box(texts[j]);
                    bool overlap = a.left < b.right && b.left < a.right && a.top < b.bottom && b.top < a.bottom;
                    Assert.IsFalse(overlap, view + ": «" + texts[i].Text + "» налезла на «" + texts[j].Text + "»");
                }
            }
        }
    }

    [Test]
    public void AFloorLabel_StaysClearOfEveryPartStandingOnIt()
    {
        var drawing = Compose(PlanFixtures.Kitchen());
        var floor = Texts(drawing).Single(t => t.Text == "Floor");
        var box = Box(floor);

        foreach (var rect in drawing.Shapes.Where(s => s.Kind == PlanShapeKind.Rect && s.Name != null && s.Name != "Floor"))
        {
            bool overlap = box.left < rect.X1 && rect.X0 < box.right && box.top < rect.Y1 && rect.Y0 < box.bottom;
            Assert.IsFalse(overlap, "«Floor» лежит на детали " + rect.Name);
        }
    }

    [Test]
    public void APartWithAnIssue_IsMarkedByColourShapeAndText_NotByColourAlone()
    {
        var drawing = Compose(PlanFixtures.Kitchen());

        var rect = RectOf(drawing, "Cab03");
        var triangles = drawing.Shapes.Where(s => s.Kind == PlanShapeKind.Triangle && s.Fill == PlanPalette.Issue).ToList();

        Assert.AreEqual(PlanPalette.Issue, rect.Stroke, "красная рамка");
        Assert.GreaterOrEqual(rect.StrokeWidth, 2, "толще обычной");
        Assert.AreEqual(1, triangles.Count, "и треугольник — для тех, кто не различает красный");
        Assert.AreEqual(rect.X0, triangles[0].X0, "треугольник у левого верхнего угла детали");
        CollectionAssert.Contains(Texts(drawing).Select(t => t.Text).ToList(), "!Cab03", "и восклицательный знак перед именем");
        Assert.AreEqual(1, drawing.WithIssues);
    }

    [Test]
    public void ACleanScene_HasNoRedAnywhere()
    {
        var input = PlanFixtures.Kitchen();
        input.Entries.ForEach(e => e.Placement.issues.Clear());

        var drawing = Compose(input);

        Assert.That(drawing.Shapes.All(s => s.Stroke != PlanPalette.Issue && s.Fill != PlanPalette.Issue));
        StringAssert.Contains("no issues", PlanCaption.Of(drawing));
    }

    [Test]
    public void ThePartsWithIssuesAreDrawnLast_SoNothingCoversTheirRedOutline()
    {
        var drawing = Compose(PlanFixtures.Kitchen());

        var order = drawing.Shapes.Where(s => s.Kind == PlanShapeKind.Rect && s.Name != null).Select(s => s.Name).ToList();

        Assert.AreEqual("Cab03", order.Last());
    }

    [Test]
    public void TheLayersGoFloorWallOpeningThenParts_WhateverTheInputOrder()
    {
        var input = PlanFixtures.Kitchen();
        input.Entries.Reverse();

        var order = Compose(input).Shapes.Where(s => s.Kind == PlanShapeKind.Rect && s.Name != null).Select(s => s.Name).ToList();

        Assert.AreEqual("Floor", order[0]);
        Assert.Less(order.IndexOf("Wall_S"), order.IndexOf("Window_1"));
        Assert.Less(order.IndexOf("Window_1"), order.IndexOf("Cab01"));
        Assert.Less(order.IndexOf("Wall_N"), order.IndexOf("Table1"));
    }

    [Test]
    public void InTheTopView_TheHigherPartCoversTheLowerOne()
    {
        var input = Of(Part("Upper", 0, 1400, 0, 800, 2100, 300), Part("Base", 0, 0, 0, 800, 720, 560));

        var order = Compose(input, PlanView.Top).Shapes.Where(s => s.Kind == PlanShapeKind.Rect && s.Name != null)
            .Select(s => s.Name).ToList();

        CollectionAssert.AreEqual(new[] { "Base", "Upper" }, order);
    }

    [Test]
    public void AModuleIsNotAlwaysBelowAPart_HeightDecidesInTheTopView()
    {
        var module = PlanFixtures.Entry("Upper", "board", 0, 1400, 0, 800, 2100, 300, parts: 4);
        var lowModule = PlanFixtures.Entry("LowBox", "board", 0, 0, 0, 800, 300, 300, parts: 2);
        var input = Of(module, lowModule, Part("Mid", 0, 0, 0, 800, 720, 560));

        var order = Compose(input, PlanView.Top).Shapes.Where(s => s.Kind == PlanShapeKind.Rect && s.Name != null)
            .Select(s => s.Name).ToList();

        CollectionAssert.AreEqual(new[] { "LowBox", "Mid", "Upper" }, order, "модуль и деталь равноправны: выше лежит тот, что выше");
    }

    [Test]
    public void InTheFrontView_TheNearerPartCoversTheFartherOne()
    {
        var input = Of(Part("Near", 0, 0, 0, 800, 720, 300), Part("Far", 0, 0, 1000, 800, 720, 1560));

        var order = Compose(input, PlanView.Front).Shapes.Where(s => s.Kind == PlanShapeKind.Rect && s.Name != null)
            .Select(s => s.Name).ToList();

        CollectionAssert.AreEqual(new[] { "Far", "Near" }, order, "зритель стоит на малых Z и смотрит в сторону больших");
    }

    [Test]
    public void TheSameSceneInAnyEntryOrder_GivesTheSameSvg()
    {
        var forward = PlanFixtures.Kitchen();
        var backward = PlanFixtures.Kitchen();
        backward.Entries.Reverse();

        foreach (var view in new[] { PlanView.Top, PlanView.Front })
            Assert.AreEqual(PlanRender.Svg(forward, view, true, 512), PlanRender.Svg(backward, view, true, 512),
                view + ": картинка не должна зависеть от порядка обхода сцены");
    }

    [Test]
    public void TwoRenderings_AreByteForByteTheSame()
    {
        var first = PlanRender.Render(PlanFixtures.Kitchen(), PlanView.Top, true, 512);
        var second = PlanRender.Render(PlanFixtures.Kitchen(), PlanView.Top, true, 512);

        CollectionAssert.AreEqual(first.Png, second.Png);
        Assert.AreEqual(first.Text, second.Text);
    }

    [Test]
    public void APartTooSmallToName_IsListedInTheCaption_InTheOrderDescribeSceneUses()
    {
        var input = new DigestInput();
        for (int i = 1; i <= 40; i++)
            input.Entries.Add(Part("P" + i, i * 400, 0, 0, i * 400 + 300, 700, 500));
        input.Entries.Add(Part("Z99", 20000, 0, 0, 20300, 700, 500, issue: "overlap"));

        var drawing = Compose(input);

        Assert.Greater(drawing.Unlabelled.Count, 10, "сорок деталей на пятьсот пикселей не подписать целиком");
        Assert.AreEqual("Z99", drawing.Unlabelled.FirstOrDefault() ?? "Z99", "деталь с замечанием идёт первой в списке");
        var numbers = drawing.Unlabelled.Where(n => n != "Z99").Select(n => int.Parse(n.Substring(1))).ToList();
        CollectionAssert.AreEqual(numbers.OrderBy(n => n).ToList(), numbers, "P2 раньше P10, как в describe_scene");
        StringAssert.Contains("no room for the name of: ", PlanCaption.Of(drawing));
        StringAssert.Contains(" more", PlanCaption.Of(drawing), "хвост списка называется числом, а не молчит");
    }

    [Test]
    public void ALongName_IsShortenedToWhatFits_AndTheCaptionSaysWhatItStandsFor()
    {
        var drawing = Compose(PlanFixtures.Kitchen());

        var shortened = drawing.Shortened.Single();

        Assert.AreEqual("Cab_Left_Tall_Section", shortened.Value);
        Assert.Less(shortened.Key.Length, shortened.Value.Length);
        CollectionAssert.Contains(Texts(drawing).Select(t => t.Text).ToList(), shortened.Key);
        StringAssert.Contains("shortened: " + shortened.Key + "=" + shortened.Value, PlanCaption.Of(drawing));
    }

    [Test]
    public void TwoLongNamesThatCutTheSame_GetDifferentLabels()
    {
        var input = new DigestInput();
        input.Entries.Add(Part("Cabinet_Left_A1", 0, 0, 0, 1200, 700, 500));
        input.Entries.Add(Part("Cabinet_Right_A1", 1200, 0, 0, 2400, 700, 500));
        input.Entries.Add(Part("Cabinet_Far_A1", 10800, 0, 0, 12000, 700, 500));

        var drawing = Compose(input);

        var shown = drawing.Shortened.Select(p => p.Key).ToList();
        Assert.GreaterOrEqual(shown.Count, 2, "подписи сокращены");
        CollectionAssert.AllItemsAreUnique(shown, "по картинке нельзя было бы отличить одну деталь от другой");
        Assert.AreEqual(shown.Count, drawing.Shortened.Select(p => p.Value).Distinct().Count());
    }

    [Test]
    public void TheScaleBar_IsAWholeNumberOfMillimetres_DrawnAtThePictureScale()
    {
        var drawing = Compose(PlanFixtures.Kitchen());

        var bar = drawing.Shapes.Where(s => s.Kind == PlanShapeKind.Rect && s.Name == null && s.Fill == PlanPalette.Ink
            && s.Y0 > drawing.Height * 0.8).OrderByDescending(s => s.X1 - s.X0).First();

        Assert.That(new[] { 10, 20, 50, 100, 200, 500, 1000, 2000, 5000, 10000, 20000, 50000 }, Contains.Item(drawing.ScaleBarMm));
        Assert.AreEqual(drawing.ScaleBarMm / drawing.MmPerPixel, bar.X1 - bar.X0, 1.0, "длина линейки в пикселях = мм / (мм на пиксель)");
        CollectionAssert.Contains(Texts(drawing).Select(t => t.Text).ToList(), drawing.ScaleBarMm + " mm");
    }

    [Test]
    public void TheRoomOutline_IsOnlyInTheTopView()
    {
        var input = PlanFixtures.Kitchen();

        var top = Compose(input, PlanView.Top).Shapes.Count(s => s.Kind == PlanShapeKind.Line && s.Stroke == PlanPalette.RoomLine);
        var front = Compose(input, PlanView.Front).Shapes.Count(s => s.Kind == PlanShapeKind.Line);

        Assert.AreEqual(4, top);
        Assert.AreEqual(0, front, "в разрезе у комнаты нет контура");
        CollectionAssert.Contains(Texts(Compose(input, PlanView.Top)).Select(t => t.Text).ToList(), "kitchen");
    }

    [Test]
    public void ARoomOutsideThePartsExtends_TheTopViewToIt()
    {
        var input = Of(Part("Cab", 0, 0, 0, 600, 720, 560));
        input.Rooms.Add(new DigestGroup { Id = "R", PolygonXz = new[] { 0, 0, 6000, 0, 6000, 4000, 0, 4000 } });

        var withRoom = Compose(input, PlanView.Top);
        var withoutRoom = Compose(input, PlanView.Front);

        Assert.Greater(withRoom.MmPerPixel, withoutRoom.MmPerPixel * 3, "комната шесть метров, шкаф шестьсот миллиметров");
    }

    [Test]
    public void AnEmptyScene_StillMakesAValidPicture()
    {
        var drawing = Compose(new DigestInput());

        Assert.LessOrEqual(Math.Max(drawing.Width, drawing.Height), 512);
        var read = PlanPngReader.Read(PlanPng.Encode(PlanRaster.Render(drawing)));
        Assert.AreEqual(drawing.Width, read.Width);
        StringAssert.Contains("0 items", PlanCaption.Of(drawing));
    }

    [Test]
    public void ADegeneratePart_WithNoThickness_IsStillVisible()
    {
        var input = Of(Part("Film", 0, 0, 0, 2000, 0, 0), Part("Cab", 0, 0, 0, 600, 720, 560));

        var film = RectOf(Compose(input), "Film");

        Assert.GreaterOrEqual(film.Y1 - film.Y0, PlanLayout.MinPixelsPerRect);
        Assert.GreaterOrEqual(film.X1 - film.X0, PlanLayout.MinPixelsPerRect);
    }

    [Test]
    public void TheCaption_NamesTheViewTheAxesAndTheScale()
    {
        var top = PlanCaption.Of(Compose(PlanFixtures.Kitchen(), PlanView.Top));
        var front = PlanCaption.Of(Compose(PlanFixtures.Kitchen(), PlanView.Front));

        StringAssert.StartsWith("render_plan top 512x", top);
        StringAssert.Contains("x right, z up", top);
        StringAssert.Contains("x right, y up", front);
        StringAssert.Contains("scope whole scene", top);
        StringAssert.Contains("1 px = ", top);
        StringAssert.Contains("1 with issues (red outline + triangle, ! before the name)", top);
        Assert.Less(top.Length, 400, "подпись коротка: это строка рядом с картинкой, а не отчёт");
    }

    [Test]
    public void TheCaption_NamesTheScope()
    {
        var input = PlanFixtures.Kitchen();
        input.Scope = "module Upper";

        StringAssert.Contains("scope module Upper", PlanCaption.Of(Compose(input)));
    }

    private static PlanShape FrontRect(string name) => RectOf(Compose(PlanFixtures.Kitchen(), PlanView.Front), name);

    private static List<string> Names(PlanDrawing drawing) =>
        drawing.Shapes.Where(s => s.Kind == PlanShapeKind.Rect && s.Name != null).Select(s => s.Name!).ToList();

    [Test]
    public void FrontView_AWallSeenEdgeOn_IsAThinOutlineWithNoFill()
    {
        foreach (var name in new[] { "Wall_W", "Wall_E" })
        {
            var wall = FrontRect(name);

            Assert.AreEqual(PlanShape.NoColor, wall.Fill, name + ": боковая стена видна ребром и ничего не закрывает");
            Assert.AreEqual(PlanPalette.WallLine, wall.Stroke);
            Assert.AreEqual(1, wall.StrokeWidth);
            Assert.Less(wall.X1 - wall.X0, 20, name + ": её ширина — её толщина");
        }
    }

    [Test]
    public void FrontView_AWallFacingTheViewer_IsALightBackdropNotAGreyBlock()
    {
        var drawing = Compose(PlanFixtures.Kitchen(), PlanView.Front);

        foreach (var name in new[] { "Wall_N", "Wall_S" })
        {
            var wall = RectOf(drawing, name);
            Assert.AreEqual(PlanPalette.WallFacing, wall.Fill, name);
            Assert.AreNotEqual(PlanPalette.Wall, wall.Fill, "серая заливка на всю картинку прятала и путала детали");
        }
        Assert.That(drawing.Shapes.All(s => s.Fill != PlanPalette.Wall), "в разрезе серого цвета стен нет вовсе");
    }

    [Test]
    public void TopView_KeepsItsGreyWalls()
    {
        Assert.AreEqual(PlanPalette.Wall, RectOf(Compose(PlanFixtures.Kitchen(), PlanView.Top), "Wall_N").Fill);
    }

    [Test]
    public void FrontView_EveryWallIsDrawnBeforeEveryPart_FarWallsFirst()
    {
        var order = Names(Compose(PlanFixtures.Kitchen(), PlanView.Front));

        int lastWall = new[] { "Wall_N", "Wall_S", "Wall_W", "Wall_E" }.Max(n => order.IndexOf(n));
        foreach (var part in new[] { "Cab01", "Upper", "Sink1", "Table1", "Cab_Left_Tall_Section" })
            Assert.Less(lastWall, order.IndexOf(part), "стена перекрыла бы деталь " + part);
        Assert.Less(order.IndexOf("Wall_N"), order.IndexOf("Wall_S"), "север в глубине (Z 3000), юг ближе к зрителю");
    }

    [Test]
    public void FrontView_WallAndFloorNamesSitInAStripAboveThePicture_NeverInTheFrame()
    {
        var drawing = Compose(PlanFixtures.Kitchen(), PlanView.Front);
        int frameTop = drawing.Shapes.Where(s => s.Kind == PlanShapeKind.Rect && s.Name != null).Min(s => s.Y0);

        foreach (var name in new[] { "Wall_N", "Wall_S", "Wall_W", "Wall_E", "Floor" })
        {
            var label = Texts(drawing).Single(t => t.Text == name);
            Assert.LessOrEqual(label.Y0 + PlanFont.Rows * label.Scale, frameTop, name + ": подпись выше кадра, а не поверх деталей");
        }
    }

    [Test]
    public void FrontView_NoLabelsMeansNoStrip()
    {
        var input = PlanFixtures.Kitchen();

        Assert.AreEqual(0, PlanLayout.For(input, PlanView.Front, 512, labels: false).StripRows);
        Assert.AreEqual(PlanLayout.MaxStripRows, PlanLayout.For(input, PlanView.Front, 512, labels: true).StripRows);
        Assert.AreEqual(0, PlanLayout.For(input, PlanView.Top, 512, labels: true).StripRows, "сверху стены подписаны на самом плане");
    }

    [Test]
    public void FrontView_AnOpeningIsAnOutline_AndItsNameIsInsideItClearOfEveryPart()
    {
        var drawing = Compose(PlanFixtures.Kitchen(), PlanView.Front);

        var window = RectOf(drawing, "Window_1");
        var label = Texts(drawing).Single(t => t.Text == "Window_1");
        var box = Box(label);

        Assert.AreEqual(PlanShape.NoColor, window.Fill, "проём — контур: за ним видна стена");
        Assert.AreEqual(PlanPalette.WindowLine, window.Stroke);
        Assert.AreEqual(PlanPalette.DoorLine, FrontRect("Door_1").Stroke);
        Assert.That(box.left >= window.X0 && box.right <= window.X1 && box.top >= window.Y0 && box.bottom <= window.Y1,
            "подпись целиком внутри проёма");
        foreach (var part in drawing.Shapes.Where(s => s.Kind == PlanShapeKind.Rect && s.Name is "Upper" or "Cab01" or "Sink1"))
        {
            bool overlap = box.left < part.X1 && part.X0 < box.right && box.top < part.Y1 && part.Y0 < box.bottom;
            Assert.IsFalse(overlap, "подпись окна налезла на " + part.Name);
        }
    }

    [Test]
    public void FrontView_AnOpeningHiddenBehindAPart_IsReportedNotLeftFloating()
    {
        var input = Of(
            PlanFixtures.Entry("Door_1", "door", 0, 0, 3000, 700, 2100, 3100),
            PlanFixtures.Entry("Cab", "board", 0, 0, 0, 800, 2100, 560),
            Part("Wide", 800, 0, 0, 4000, 700, 500));

        var drawing = Compose(input, PlanView.Front);

        Assert.IsFalse(Texts(drawing).Any(t => t.Text == "Door_1"), "негде встать внутри видимой части проёма: плавающей подписи нет");
        CollectionAssert.Contains(drawing.Unlabelled, "Door_1");
    }

    [Test]
    public void Label_TheFontShrinksBeforeTheNameIsShortened()
    {
        var input = Of(Part("Cab_Left_Tall_01", 0, 0, 0, 1200, 700, 500), Part("Wide", 1200, 0, 0, 6000, 700, 500));

        var drawing = Compose(input);

        var label = Texts(drawing).Single(t => t.Text == "Cab_Left_Tall_01");
        Assert.AreEqual(1, label.Scale, "крупный шрифт не помещается, мелкий читаем — берём мелкий, имя целое");
        CollectionAssert.IsEmpty(drawing.Shortened);
    }

    [Test]
    public void OnlyWhenTheSmallestFontDoesNotFit_TheNameIsShortened_KeepingItsNumber()
    {
        var input = Of(Part("Cab_Left_Tall_Section_01", 0, 0, 0, 1500, 700, 500), Part("Wide", 1500, 0, 0, 12000, 700, 500));

        var drawing = Compose(input);

        var pair = drawing.Shortened.Single();
        Assert.AreEqual("Cab_Left_Tall_Section_01", pair.Value);
        StringAssert.EndsWith("_01", pair.Key, "номер на месте");
        StringAssert.StartsWith("Cab_", pair.Key);
        Assert.AreEqual(pair.Key, McpNameShortening.Fit(pair.Value, pair.Key.Length), "то же правило, что у любой другой строки с именем");
        Assert.AreEqual(1, Texts(drawing).Single(t => t.Text == pair.Key).Scale, "сокращённое имя — только на минимальном шрифте");
    }

    private static double Ratio(PlanShape rect) => (rect.X1 - rect.X0) / (double)(rect.Y1 - rect.Y0);
}
