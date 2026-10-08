using System;
using NUnit.Framework;
using KitchenDesigner.Core.MCP;
using KitchenDesigner.Core.MCP.Contract;

/// <summary>Бюджет картинки в байтах. PNG уходит по проводу base64 (+33%), и слабой модели каждый килобайт — деньги и
/// внимание: поэтому потолок записан числом и сторожит, что картинка не потяжелеет молча (градиент, сглаживание,
/// лишняя палитра). Потолки — с запасом около полутора раз над замером на дату записи.</summary>
public class PlanRenderBudgetTests
{
    private const int KitchenPngBudgetBytes = 4500;
    private const int FiftyPartsPngBudgetBytes = 7000;
    private const int LargePicturePngBudgetBytes = 14000;

    private static DigestInput FiftyParts()
    {
        var input = PlanFixtures.Kitchen();
        for (int i = 0; i < 50; i++)
        {
            float x = 100 + (i % 10) * 380, z = 700 + (i / 10) * 400;
            input.Entries.Add(PlanFixtures.Entry("Part" + i.ToString("00"), "board", x, 0, z, x + 350, 720, z + 360,
                issue: i % 17 == 0 ? "overlap" : null));
        }
        return input;
    }

    [Test]
    public void TheKitchenPicture_FitsItsByteBudget()
    {
        foreach (var view in new[] { PlanView.Top, PlanView.Front })
        {
            var reply = PlanRender.Render(PlanFixtures.Kitchen(), view, true, 512);

            TestContext.WriteLine(view + ": " + reply.Png.Length + " bytes PNG, " + Base64Length(reply.Png) + " bytes base64");
            Assert.LessOrEqual(reply.Png.Length, KitchenPngBudgetBytes, view + ": картинка кухни стала тяжелее бюджета");
        }
    }

    [Test]
    public void AFiftyPartPicture_FitsItsByteBudget()
    {
        var reply = PlanRender.Render(FiftyParts(), PlanView.Top, true, 512);

        TestContext.WriteLine("66 деталей: " + reply.Png.Length + " bytes PNG, " + Base64Length(reply.Png) + " bytes base64");
        Assert.LessOrEqual(reply.Png.Length, FiftyPartsPngBudgetBytes);
    }

    [Test]
    public void ALargePicture_StillFitsItsByteBudget()
    {
        var reply = PlanRender.Render(FiftyParts(), PlanView.Top, true, 1024);

        TestContext.WriteLine("1024 px: " + reply.Png.Length + " bytes PNG");
        Assert.LessOrEqual(reply.Png.Length, LargePicturePngBudgetBytes);
    }

    [Test]
    public void TheBudgetCanFail_ABusyPictureOfNoiseWouldBust_ItsCeiling()
    {
        var canvas = new PlanCanvas(512, 400, 0);
        var random = new Random(3);
        for (int i = 0; i < canvas.Pixels.Length; i++) canvas.Pixels[i] = (byte)random.Next(PlanPalette.Count);

        Assert.Greater(PlanPng.Encode(canvas).Length, KitchenPngBudgetBytes, "потолок бесполезен, если им не поймать шум");
    }

    [Test]
    public void ThePicture_UsesNothingButThePalette()
    {
        var reply = PlanRender.Render(FiftyParts(), PlanView.Top, true, 512);

        var read = PlanPngReader.Read(reply.Png);

        foreach (var pixel in read.Indices) Assert.Less(pixel, PlanPalette.Count);
        Assert.AreEqual(PlanPalette.Count * 3, read.Palette.Length);
    }

    [Test]
    public void ThePictureIsExactlyWhatTheDrawingSays_PixelForPixel()
    {
        var input = PlanFixtures.Kitchen();

        var canvas = PlanRaster.Render(PlanComposer.Compose(input, PlanView.Top, true, 512));
        var read = PlanPngReader.Read(PlanRender.Render(input, PlanView.Top, true, 512).Png);

        Assert.AreEqual(canvas.Width, read.Width);
        CollectionAssert.AreEqual(canvas.Pixels, read.Indices);
    }

    [Test]
    public void TheSvgAndThePng_CarryTheSameShapes()
    {
        var drawing = PlanComposer.Compose(PlanFixtures.Kitchen(), PlanView.Top, true, 512);

        var svg = PlanSvg.Write(drawing);

        Assert.AreEqual(Count(drawing, PlanShapeKind.Text), Occurrences(svg, "<text "));
        Assert.AreEqual(Count(drawing, PlanShapeKind.Line), Occurrences(svg, "<line "));
        Assert.AreEqual(Count(drawing, PlanShapeKind.Triangle), Occurrences(svg, "<polygon "));
        Assert.AreEqual(Count(drawing, PlanShapeKind.Rect) + 1, Occurrences(svg, "<rect "), "и фон");
        StringAssert.Contains("width=\"" + drawing.Width + "\" height=\"" + drawing.Height + "\"", svg);
    }

    [Test]
    public void ThePxRangeInTheContractText_IsTheRangeTheRendererAccepts()
    {
        var field = typeof(ParamsRenderPlan).GetField(nameof(ParamsRenderPlan.px))!;
        var param = (McpParamAttribute)Attribute.GetCustomAttribute(field, typeof(McpParamAttribute))!;

        Assert.AreEqual(PlanRender.MinPx, param.Min);
        Assert.AreEqual(PlanRender.MaxPx, param.Max);
        Assert.AreEqual(PlanRender.DefaultPx, new ParamsRenderPlan().px);
        StringAssert.Contains(PlanRender.MinPx + ".." + PlanRender.MaxPx, param.Description);
        StringAssert.Contains("default " + PlanRender.DefaultPx, param.Description);
    }

    [Test]
    public void TheToolIsRegistered_AsAReadTool_WithAnImageDescription()
    {
        var tool = System.Linq.Enumerable.Single(McpToolRegistry.Tools, t => t.Name == "render_plan");

        Assert.AreEqual(McpToolKind.Read, tool.Kind, "картинка ничего не меняет в сцене");
        Assert.AreEqual(typeof(ParamsRenderPlan), tool.ParamsType);
        StringAssert.Contains("describe_scene", tool.Description, "сказано, когда брать картинку, а когда текст");
        StringAssert.Contains("NEVER read coordinates off the picture", tool.Description);
    }

    private static int Base64Length(byte[] bytes) => (bytes.Length + 2) / 3 * 4;

    private static int Count(PlanDrawing drawing, PlanShapeKind kind)
    {
        int n = 0;
        foreach (var shape in drawing.Shapes) if (shape.Kind == kind) n++;
        return n;
    }

    private static int Occurrences(string text, string needle)
    {
        int count = 0, at = 0;
        while ((at = text.IndexOf(needle, at, StringComparison.Ordinal)) >= 0) { count++; at += needle.Length; }
        return count;
    }
}
