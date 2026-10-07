using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using KitchenDesigner.Core.MCP;

public class SceneDigestTextTests
{
    private static DigestEntry Part(string name, float x = 0f, string kind = "board", string? on = "Floor",
        params string[] issues)
    {
        var entry = new DigestEntry { Name = name, Kind = kind };
        entry.Placement.name = name;
        entry.Placement.posMm = new[] { x, 0f, 0f };
        entry.Placement.footprintMm = new[] { 600f, 720f, 560f };
        entry.Placement.on = on;
        entry.Placement.issues = issues.ToList();
        return entry;
    }

    private static DigestEntry Module(string name, int parts, string? room = null)
    {
        var entry = Part(name, kind: "module");
        entry.Parts = parts;
        entry.Placement.room = room;
        return entry;
    }

    private static DigestInput Scene(params DigestEntry[] entries) =>
        new DigestInput { Entries = entries.ToList() };

    private static DigestInput FiftyParts()
    {
        var input = new DigestInput();
        for (int m = 1; m <= 6; m++) input.Entries.Add(Module("Module" + m, 6));
        for (int i = 1; i <= 14; i++) input.Entries.Add(Part("Loose" + i, i * 600f));
        return input;
    }

    private static string[] Lines(string text) => text.Split('\n');

    [Test]
    public void EntryLine_ACabinetBetweenTwoNeighboursAgainstAWall_ReadsAsOneSentence()
    {
        var entry = Part("B4", 1200f);
        entry.Placement.touches = new List<PlacementContact>
        {
            new PlacementContact { n = "B3", face = "left" },
            new PlacementContact { n = "Floor", face = "bottom" },
            new PlacementContact { n = "Wall_N", face = "back" },
        };
        entry.Placement.gaps = new List<PlacementGap> { new PlacementGap { n = "B5", face = "right", gapMm = 2f } };

        var line = SceneDigestText.EntryLine(entry);

        Assert.AreEqual("B4 board 600×720×560 @(1200,0,0) on Floor; left→B3, back→Wall_N, right→B5 gap2; ok", line,
            "стоит на полу: касание низа с тем, на чём стоит, не повторяется; остальное — теми же словами, что touches и gaps размещения");
    }

    [Test]
    public void EntryLine_ABottomContactWithSomethingOtherThanTheSupport_IsKept()
    {
        var entry = Part("Shelf", on: "Cab1");
        entry.Placement.touches = new List<PlacementContact>
        {
            new PlacementContact { n = "Cab1", face = "bottom" },
            new PlacementContact { n = "Cab2", face = "bottom" },
        };

        var line = SceneDigestText.EntryLine(entry);

        StringAssert.Contains("on Cab1; bottom→Cab2;", line, "опора названа словом on, второй контакт низа остаётся отношением");
        StringAssert.DoesNotContain("bottom→Cab1", line);
    }

    [Test]
    public void EntryLine_AnIssue_IsMarkedAndNotCalledOk()
    {
        var entry = Part("Clash", issues: new[] { "deep_penetration: Wall 12.2mm", "disconnected" });

        var line = SceneDigestText.EntryLine(entry);

        StringAssert.EndsWith("; !deep_penetration: Wall 12.2mm | disconnected", line);
        StringAssert.DoesNotContain("; ok", line);
    }

    [Test]
    public void EntryLine_APartWithNoSupportAndNoNeighbours_HasNoDanglingSeparators()
    {
        var entry = Part("Lone", on: null);

        Assert.AreEqual("Lone board 600×720×560 @(0,0,0); ok", SceneDigestText.EntryLine(entry));
    }

    [Test]
    public void EntryLine_AModule_SaysHowManyPartsItHolds()
    {
        var line = SceneDigestText.EntryLine(Module("B4", 6));

        StringAssert.StartsWith("B4 module(6) 600×720×560 @(0,0,0)", line);
    }

    [Test]
    public void EntryLine_AFractionalMillimetre_KeepsOneDecimal_AndNegativeZeroIsZero()
    {
        var entry = Part("Odd", x: -0.04f);
        entry.Placement.footprintMm = new[] { 600.3f, 720f, 18f };

        var line = SceneDigestText.EntryLine(entry);

        StringAssert.Contains("600.3×720×18 @(0,0,0)", line);
        StringAssert.DoesNotContain("-0", line, "минус нуля слабая модель читает как отрицательную координату");
    }

    [Test]
    public void EntryLine_APlacementWithoutAFootprint_ShowsAQuestionMark_NotACrash()
    {
        var entry = new DigestEntry { Name = "Bare", Kind = "board" };

        Assert.AreEqual("Bare board ? @(?); ok", SceneDigestText.EntryLine(entry));
    }

    [Test]
    public void Build_TheFirstLineIsTheSummary_ThenTheLegend_ThenLevelsAndRooms_ThenModules_ThenLooseParts()
    {
        var input = FiftyParts();
        input.Levels.Add(new DigestGroup { Id = "L1", Detail = "\"Ground\" 0..2700" });
        input.Rooms.Add(new DigestGroup { Id = "Kitchen" });

        var lines = Lines(SceneDigestText.Build(input, SceneDigestText.MaxMaxChars));

        StringAssert.StartsWith("scene: 50 parts, 6 modules, 14 loose; no issues", lines[0]);
        StringAssert.StartsWith("mm; size", lines[1]);
        StringAssert.StartsWith("levels: L1 \"Ground\" 0..2700", lines[2]);
        StringAssert.StartsWith("rooms: Kitchen", lines[3]);
        Assert.IsTrue(lines[4].StartsWith("Module1 module(6)"), "сначала модули: " + lines[4]);
        Assert.IsTrue(lines[10].StartsWith("Loose1 board"), "потом одиночные детали: " + lines[10]);
        Assert.AreEqual(4 + 6 + 14, lines.Length, "ничего не потеряно, когда бюджет не жмёт");
    }

    [Test]
    public void Build_LevelsAndRooms_CountThePartsInsideThem_ModulePartsIncluded()
    {
        var input = Scene(Module("M1", 6, room: "Kitchen"), Part("Loose1"), Part("Loose2"));
        input.Entries[1].Placement.room = "Kitchen";
        input.Entries[0].Placement.level = "L1";
        input.Entries[1].Placement.level = "L1";
        input.Entries[2].Placement.level = "L2";
        input.Rooms.Add(new DigestGroup { Id = "Kitchen" });
        input.Rooms.Add(new DigestGroup { Id = "Hall" });
        input.Levels.Add(new DigestGroup { Id = "L1" });
        input.Levels.Add(new DigestGroup { Id = "L2" });

        var text = SceneDigestText.Build(input, 5000);

        StringAssert.Contains("rooms: Kitchen (7), Hall (0)", text, "6 деталей модуля и 1 одиночная; пустая комната названа с нулём");
        StringAssert.Contains("levels: L1 (7), L2 (1)", text);
    }

    [Test]
    public void Build_MoreGroupsThanTheLineAllows_AreCappedAndCounted()
    {
        var input = Scene(Part("A"));
        for (int i = 1; i <= SceneDigestText.MaxGroupsPerLine + 3; i++) input.Rooms.Add(new DigestGroup { Id = "R" + i });

        var text = SceneDigestText.Build(input, 5000);

        StringAssert.Contains("R" + SceneDigestText.MaxGroupsPerLine + " (0), +3 more", text);
        StringAssert.DoesNotContain("R" + (SceneDigestText.MaxGroupsPerLine + 1) + " ", text);
    }

    [TestCase(200)]
    [TestCase(333)]
    [TestCase(800)]
    [TestCase(1500)]
    [TestCase(2999)]
    public void Build_NeverExceedsMaxChars_WhateverTheBudget(int maxChars)
    {
        var text = SceneDigestText.Build(FiftyParts(), maxChars);

        Assert.LessOrEqual(text.Length, maxChars, text);
        bool cut = text.Contains("more, use scope");
        if (cut)
            Assert.Greater(text.Length, maxChars - 130, "бюджет используется: недобор больше одной строки означал бы, что строки отбрасываются зря");
        else
            Assert.AreEqual(SceneDigestText.Build(FiftyParts(), SceneDigestText.MaxMaxChars), text, "без хвоста — значит всё поместилось целиком");
    }

    [Test]
    public void Build_WhenItDoesNotFit_SaysExactlyHowManyLinesWereLeftOut_AndHowToGetThem()
    {
        var input = FiftyParts();
        var full = Lines(SceneDigestText.Build(input, SceneDigestText.MaxMaxChars));

        var cut = Lines(SceneDigestText.Build(input, 1000));

        var footer = cut[cut.Length - 1];
        StringAssert.EndsWith(" more, use scope", footer);
        int hidden = int.Parse(footer.Substring(1, footer.IndexOf(' ') - 1));
        Assert.AreEqual(full.Length, cut.Length - 1 + hidden, "показано + скрыто = всё: счёт в хвосте не врёт");
        CollectionAssert.AreEqual(full.Take(cut.Length - 1), cut.Take(cut.Length - 1), "обрезка берёт ПРЕФИКС, а не выбирает строки по вкусу");
    }

    [Test]
    public void Build_WhenEverythingFits_HasNoFooter_AndTheLastBudgetCharIsUsable()
    {
        var input = FiftyParts();
        var whole = SceneDigestText.Build(input, SceneDigestText.MaxMaxChars);

        Assert.AreEqual(whole, SceneDigestText.Build(input, whole.Length), "ровно по размеру — без обрезки");
        StringAssert.DoesNotContain("more, use scope", whole);
        StringAssert.Contains("more, use scope", SceneDigestText.Build(input, whole.Length - 1), "на знак меньше — уже хвост");
    }

    [Test]
    public void Build_EveryModuleNameSurvivesTheDefaultBudget_BecauseModulesComeBeforeLooseParts()
    {
        var input = FiftyParts();

        var text = SceneDigestText.Build(input, SceneDigestText.DefaultMaxChars);

        for (int m = 1; m <= 6; m++) StringAssert.Contains("Module" + m + " module(6)", text);
        Assert.LessOrEqual(text.Length, SceneDigestText.DefaultMaxChars);
    }

    [Test]
    public void Build_APartWithAnIssue_IsListedBeforeCleanOnes_SoTruncationDropsTheCleanFirst()
    {
        var input = Scene(Part("A1"), Part("A2"), Part("Z9", issues: new[] { "overlap: A1 3mm" }));

        var lines = Lines(SceneDigestText.Build(input, 5000));

        Assert.IsTrue(lines[2].StartsWith("Z9 "), "первой идёт деталь с проблемой: " + string.Join(" / ", lines));
        StringAssert.Contains("1 with issues", lines[0]);
    }

    [Test]
    public void Build_ThePartsWithIssuesStayVisibleWhenTheBudgetIsTight()
    {
        var input = FiftyParts();
        input.Entries.Add(Part("Zzz_last_by_name", issues: new[] { "disconnected" }));

        var text = SceneDigestText.Build(input, 600);

        StringAssert.Contains("Zzz_last_by_name", text, "проблемная деталь важнее алфавита");
    }

    [Test]
    public void Build_NamesAreOrderedNaturally_B2BeforeB10()
    {
        var input = Scene(Part("B10"), Part("B2"), Part("B1"));

        var text = SceneDigestText.Build(input, 5000);

        Assert.Less(text.IndexOf("B1 board", System.StringComparison.Ordinal), text.IndexOf("B2 board", System.StringComparison.Ordinal));
        Assert.Less(text.IndexOf("B2 board", System.StringComparison.Ordinal), text.IndexOf("B10 board", System.StringComparison.Ordinal));
    }

    [Test]
    public void Build_TheSameSceneInAnyInputOrder_GivesTheSameText()
    {
        var forward = FiftyParts();
        var backward = FiftyParts();
        backward.Entries.Reverse();

        foreach (var budget in new[] { 400, 1500, 20000 })
            Assert.AreEqual(SceneDigestText.Build(forward, budget), SceneDigestText.Build(backward, budget),
                "порядок строк — функция содержимого, а не порядка обхода сцены");
    }

    [Test]
    public void Build_AScopedView_NamesTheScope_AndLeavesOutTheSceneWideLines()
    {
        var input = Scene(Part("B4_Side_L"), Part("B4_Side_R"));
        input.Scope = "module B4";
        input.Levels.Add(new DigestGroup { Id = "L1" });
        input.Rooms.Add(new DigestGroup { Id = "Kitchen" });

        var text = SceneDigestText.Build(input, 5000);

        StringAssert.StartsWith("scope module B4: 2 parts; no issues", text);
        StringAssert.DoesNotContain("levels:", text);
        StringAssert.DoesNotContain("rooms:", text);
    }

    [Test]
    public void Build_TheLegendNamesTheRefOfTheCoordinates()
    {
        var input = Scene(Part("A"));
        input.Ref = "center-bottom-back";

        StringAssert.Contains("@(x,y,z) = center-bottom-back point", SceneDigestText.Build(input, 5000));
    }

    [TestCase(1)]
    [TestCase(10)]
    [TestCase(60)]
    public void Build_ABudgetSmallerThanTheSummary_IsStillHonoured(int maxChars)
    {
        var text = SceneDigestText.Build(FiftyParts(), maxChars);

        Assert.AreEqual(maxChars, text.Length);
        Assert.IsTrue(text.EndsWith("…"), "обрезанное заголовком видно по многоточию");
    }

    [Test]
    public void Build_AnEmptyScene_SaysSoInsteadOfReturningNothing()
    {
        var text = SceneDigestText.Build(new DigestInput(), 1500);

        StringAssert.StartsWith("scene: 0 parts; no issues", text);
    }

    [Test]
    public void MoreFooter_NamesTheCountAndTheRemedy()
    {
        Assert.AreEqual("+12 more, use scope", SceneDigestText.MoreFooter(12));
    }
}
