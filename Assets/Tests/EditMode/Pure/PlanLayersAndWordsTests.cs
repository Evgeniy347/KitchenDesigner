using NUnit.Framework;
using KitchenDesigner.Core.MCP;

/// <summary>Слова и слои картинки. Неизвестный вид детали рисуется обычной деталью, а не пропадает: список слоёв
/// обязан отставать от каталога типов тихо и безопасно, потому что новый тип появляется без оглядки на картинку.</summary>
public class PlanLayersAndWordsTests
{
    private static DigestEntry Entry(string kind, int parts = 0) =>
        new DigestEntry { Name = "N", Kind = kind, PartsCount = parts };

    [TestCase("floor", PlanLayer.Floor)]
    [TestCase("floor_slab", PlanLayer.Floor)]
    [TestCase("foundation", PlanLayer.Floor)]
    [TestCase("wall", PlanLayer.Wall)]
    [TestCase("door", PlanLayer.Door)]
    [TestCase("window", PlanLayer.Window)]
    [TestCase("sink", PlanLayer.Appliance)]
    [TestCase("cooktop", PlanLayer.Appliance)]
    [TestCase("oven", PlanLayer.Appliance)]
    [TestCase("dishwasher", PlanLayer.Appliance)]
    [TestCase("board", PlanLayer.Part)]
    [TestCase("chair", PlanLayer.Part)]
    [TestCase("a_type_nobody_has_heard_of", PlanLayer.Part)]
    public void TheKindOfAPart_ChoosesItsLayer(string kind, PlanLayer expected)
    {
        Assert.AreEqual(expected, PlanLayers.Of(Entry(kind)));
    }

    [Test]
    public void AModule_IsAModuleWhateverItsKindSays()
    {
        Assert.AreEqual(PlanLayer.Module, PlanLayers.Of(Entry("wall", parts: 3)));
    }

    [Test]
    public void EveryLayer_HasItsOwnFill_AndTheFillsAreDistinguishableByLightnessToo()
    {
        var seen = new System.Collections.Generic.HashSet<int>();
        foreach (PlanLayer layer in System.Enum.GetValues(typeof(PlanLayer)))
            Assert.IsTrue(seen.Add(PlanPalette.FillOf(layer)), layer + " красится так же, как другой слой");
        Assert.AreEqual(PlanLayerCount, seen.Count);
    }

    private static int PlanLayerCount => System.Enum.GetValues(typeof(PlanLayer)).Length;

    [Test]
    public void TheIssueColour_IsNotAFill_SoRedAlwaysMeansTrouble()
    {
        foreach (PlanLayer layer in System.Enum.GetValues(typeof(PlanLayer)))
            Assert.AreNotEqual(PlanPalette.Issue, PlanPalette.FillOf(layer));
    }

    [TestCase("top", true, PlanView.Top)]
    [TestCase(" FRONT ", true, PlanView.Front)]
    [TestCase("Top", true, PlanView.Top)]
    [TestCase("side", false, PlanView.Top)]
    [TestCase("", false, PlanView.Top)]
    [TestCase(null, false, PlanView.Top)]
    public void TheViewWord_IsParsedWithoutCaseOrSpaces(string? text, bool ok, PlanView expected)
    {
        Assert.AreEqual(ok, PlanViewWord.TryParse(text, out var view));
        Assert.AreEqual(expected, view);
    }

    [Test]
    public void TheAxisWords_SayWhichWayEachAxisRuns()
    {
        Assert.AreEqual("x right, z up", PlanViewWord.Axes(PlanView.Top));
        Assert.AreEqual("x right, y up", PlanViewWord.Axes(PlanView.Front));
    }
}
