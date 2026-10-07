using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core.MCP;

public class McpReferenceTests
{
    private static readonly Vector3 BoxMin = new Vector3(100f, 200f, 300f);
    private static readonly Vector3 BoxMax = new Vector3(300f, 600f, 700f);

    private static McpReference Parsed(string? text)
    {
        Assert.IsTrue(McpReference.TryParse(text, out var reference, out var error),
            $"ref '{text}' обязан разбираться: {error}");
        return reference;
    }

    private static IEnumerable<McpReference> AllTwentySeven()
    {
        foreach (ReferenceSide x in Enum.GetValues(typeof(ReferenceSide)))
        foreach (ReferenceSide y in Enum.GetValues(typeof(ReferenceSide)))
        foreach (ReferenceSide z in Enum.GetValues(typeof(ReferenceSide)))
            yield return new McpReference(x, y, z);
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    [TestCase("min")]
    [TestCase("left-bottom-back")]
    [TestCase("left")]
    public void Parse_NoRefOrTheMinCorner_IsTheDefaultMinimumCorner(string? text)
    {
        var reference = Parsed(text);

        Assert.IsTrue(reference.IsMinCorner,
            "без ref (и при ref=min) числа — МИНИМАЛЬНЫЙ угол, как было до ref: иначе старые вызовы тихо поедут");
        Assert.AreEqual(McpReference.DefaultName, reference.Canonical);
    }

    [TestCase("max", ReferenceSide.Max, ReferenceSide.Max, ReferenceSide.Max)]
    [TestCase("right-top-front", ReferenceSide.Max, ReferenceSide.Max, ReferenceSide.Max)]
    [TestCase("center", ReferenceSide.Center, ReferenceSide.Center, ReferenceSide.Center)]
    [TestCase("center-bottom", ReferenceSide.Center, ReferenceSide.Min, ReferenceSide.Center)]
    [TestCase("bottom-center", ReferenceSide.Center, ReferenceSide.Min, ReferenceSide.Center)]
    [TestCase("front-center", ReferenceSide.Center, ReferenceSide.Center, ReferenceSide.Max)]
    [TestCase("LEFT, Top", ReferenceSide.Min, ReferenceSide.Max, ReferenceSide.Min)]
    [TestCase("right front", ReferenceSide.Max, ReferenceSide.Min, ReferenceSide.Max)]
    [TestCase("centre-top", ReferenceSide.Center, ReferenceSide.Max, ReferenceSide.Center)]
    [TestCase("left-center-back", ReferenceSide.Min, ReferenceSide.Center, ReferenceSide.Min)]
    public void Parse_NamedSides_PicksThoseAndCentresTheRestOnlyWhenCenterIsSaid(
        string text, ReferenceSide x, ReferenceSide y, ReferenceSide z)
    {
        var reference = Parsed(text);

        Assert.AreEqual(new McpReference(x, y, z), reference,
            $"'{text}': названная ось берёт свою сторону, неназванные — центр, если есть слово center, иначе минимум");
    }

    [Test]
    public void Parse_EveryOfTheTwentySevenReferences_SurvivesItsOwnCanonicalName()
    {
        int seen = 0;
        foreach (var original in AllTwentySeven())
        {
            seen++;
            var echoed = original.Canonical;

            Assert.AreEqual(original, Parsed(echoed),
                $"ответ эхом возвращает '{echoed}', и агент подставит его обратно в запрос дословно: "
                + "каноническое имя обязано разбираться в ту же точку, иначе чтение->запись сдвигает деталь");
        }
        Assert.AreEqual(27, seen, "перебор не охватил все сочетания 3x3x3");
    }

    [Test]
    public void Canonical_IsAlwaysThreeWordsInAxisOrder_SoTheEchoNeverLeavesAnAxisToGuess()
    {
        Assert.AreEqual("center-bottom-center", Parsed("center-bottom").Canonical);
        Assert.AreEqual("right-top-front", Parsed("max").Canonical);
        Assert.AreEqual("center-center-center", Parsed("center").Canonical);
    }

    [TestCase("left-right")]
    [TestCase("top-bottom")]
    [TestCase("front-back-front-back")]
    [TestCase("middle")]
    [TestCase("min-top")]
    [TestCase("max-center")]
    [TestCase("lefft")]
    [TestCase("1-2-3")]
    public void Parse_ContradictionsAndUnknownWords_AreRefusedWithTheSyntaxInTheMessage(string text)
    {
        Assert.IsFalse(McpReference.TryParse(text, out _, out var error),
            $"'{text}' противоречиво или неизвестно: молча выбрать сторону значило бы поставить деталь не туда");
        StringAssert.Contains(text, error, "ошибка называет то, что прислали");
        StringAssert.Contains("left-bottom-back", error, "ошибка показывает слабой модели, как писать правильно");
    }

    [Test]
    public void Parse_TheSameSideTwice_IsNotAContradiction()
    {
        Assert.AreEqual(Parsed("left-bottom"), Parsed("left-left-bottom"),
            "повтор той же стороны — не конфликт, а лишний вес слова; отказ тут только раздражал бы модель");
    }

    [Test]
    public void PointOf_PicksTheNamedPointOfTheWorldBox()
    {
        Assert.AreEqual(new Vector3(100f, 200f, 300f), Parsed("min").PointOf(BoxMin, BoxMax));
        Assert.AreEqual(new Vector3(300f, 600f, 700f), Parsed("max").PointOf(BoxMin, BoxMax));
        Assert.AreEqual(new Vector3(200f, 400f, 500f), Parsed("center").PointOf(BoxMin, BoxMax));
        Assert.AreEqual(new Vector3(200f, 200f, 500f), Parsed("center-bottom").PointOf(BoxMin, BoxMax),
            "середина следа на уровне пола: x и z — центр, y — низ");
        Assert.AreEqual(new Vector3(300f, 200f, 300f), Parsed("right-bottom-back").PointOf(BoxMin, BoxMax));
    }

    [Test]
    public void OffsetFromMin_IsZeroForTheMinCorner_AndHalfTheExtentForCenter()
    {
        Assert.AreEqual(Vector3.zero, McpReference.MinCorner.OffsetFromMin(BoxMin, BoxMax),
            "для угла по умолчанию смещение нулевое — старый путь anchor=min не должен измениться ни на миллиметр");
        Assert.AreEqual(new Vector3(100f, 200f, 200f), Parsed("center").OffsetFromMin(BoxMin, BoxMax));
    }

    [Test]
    public void ReadThenWriteTheSamePoint_LeavesEveryReferenceWhereItWas_AlsoForARotatedFootprint()
    {
        var boxes = new[]
        {
            (min: BoxMin, max: BoxMax),
            (min: new Vector3(100f, 200f, 300f), max: new Vector3(700f, 600f, 500f)),
        };

        foreach (var box in boxes)
            foreach (var reference in AllTwentySeven())
            {
                var read = reference.PointOf(box.min, box.max);
                var offset = reference.OffsetFromMin(box.min, box.max);
                var minAfterWrite = read - offset;

                Assert.AreEqual(box.min.x, minAfterWrite.x, 1e-4f, $"{reference.Canonical}: x");
                Assert.AreEqual(box.min.y, minAfterWrite.y, 1e-4f, $"{reference.Canonical}: y");
                Assert.AreEqual(box.min.z, minAfterWrite.z, 1e-4f,
                    $"{reference.Canonical}: прочитанное в этой точке и записанное обратно в неё не двигает деталь");
            }
    }

    [Test]
    public void Syntax_NamesEveryWordTheParserAccepts()
    {
        foreach (var word in new[] { "left", "right", "bottom", "top", "back", "front", "center", "min", "max" })
            StringAssert.Contains(word, McpReference.Syntax,
                $"слово '{word}' разбирается, но модели о нём не сказано — она им не воспользуется");
    }
}
