using System.Linq;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core.MCP;

public class RunLayoutBuilderTests
{
    private static readonly BoxMm WallBox = new BoxMm(new Vector3(0f, 0f, -100f), new Vector3(4000f, 2700f, 100f));

    private static RunWall WallAlongX(bool roomOnMax = true) => new RunWall("Wall", WallBox, 0, roomOnMax);

    private static RunWall WallAlongZ(bool roomOnMax = true) =>
        new RunWall("Side", new BoxMm(new Vector3(-100f, 0f, 0f), new Vector3(100f, 2700f, 3000f)), 2, roomOnMax);

    private static RunModuleDecl Cab(string name, string kind = "base", int width = 600) =>
        new RunModuleDecl { Name = name, Kind = kind, WidthMm = width };

    private static RunDeclaration Run(params RunModuleDecl[] modules)
    {
        var run = new RunDeclaration { Id = "Row" };
        run.Modules.AddRange(modules);
        return run;
    }

    private static RunLayout Build(RunDeclaration run, RunWall? wall = null, BoxMm? part = null,
        float lift = 0f, params string[] existing) =>
        RunLayoutBuilder.Build(run, wall ?? WallAlongX(), part, lift, ValidName, existing.Contains);

    private static bool ValidName(string name) =>
        !string.IsNullOrEmpty(name) && name.All(c => (c < 128 && char.IsLetterOrDigit(c)) || c == '_' || c == '-');

    private static (string target, string face)[] Faces(RunStep step) =>
        step.Spec.Against.Select(a => (a.Target, a.Face)).ToArray();

    [Test]
    public void Build_ThreeBaseCabinets_FromTheLeft_FirstAlignsToTheWallEnd_TheRestStandAgainstTheirLeftNeighbour()
    {
        var layout = Build(Run(Cab("C1"), Cab("C2"), Cab("C3")));

        Assert.IsTrue(layout.Ok, string.Join(" | ", layout.Problems));
        Assert.AreEqual(3, layout.Steps.Count);
        var first = layout.Steps[0].Spec.Align.Single();
        Assert.AreEqual("min", first.At, "первый шкаф встаёт к меньшему концу стены");
        Assert.AreEqual("x", first.Axis);
        Assert.AreEqual(0f, first.OffsetMm);
        Assert.IsEmpty(layout.Steps[1].Spec.Align, "второй вплотную к первому, без выравнивания по стене");
        CollectionAssert.AreEqual(new[] { ("Wall", "front"), ("C1", "right") }, Faces(layout.Steps[1]),
            "стена со стороны комнаты (z+ — front) и правая грань предыдущего шкафа");
        CollectionAssert.AreEqual(new[] { ("Wall", "front"), ("C2", "right") }, Faces(layout.Steps[2]));
    }

    [Test]
    public void Build_FromTheRight_TheFirstCabinetIsTheRightmost_AndTheRestGrowToTheLeft()
    {
        var run = Run(Cab("C1"), Cab("C2"));
        run.From = "right";
        run.StartMm = 150f;

        var layout = Build(run);

        var first = layout.Steps[0].Spec.Align.Single();
        Assert.AreEqual("max", first.At);
        Assert.AreEqual(-150f, first.OffsetMm, "отступ от правого конца уходит внутрь стены, то есть в минус");
        CollectionAssert.AreEqual(new[] { ("Wall", "front"), ("C1", "left") }, Faces(layout.Steps[1]),
            "следующий — слева от предыдущего");
    }

    [Test]
    public void Build_FromAPart_StartsAtItsFarSide_WithStartMmAsTheGap()
    {
        var run = Run(Cab("C1"));
        run.From = "Fridge";
        run.StartMm = 20f;

        var layout = Build(run, part: new BoxMm(new Vector3(0f, 0f, 100f), new Vector3(600f, 1800f, 700f)));

        Assert.IsEmpty(layout.Steps[0].Spec.Align);
        var anchor = layout.Steps[0].Spec.Against.Single(a => a.Target == "Fridge");
        Assert.AreEqual("right", anchor.Face);
        Assert.AreEqual(20f, anchor.GapMm);
    }

    [Test]
    public void Build_GapMm_GoesBetweenNeighboursOnly_NotOnTheFirstCabinet()
    {
        var run = Run(Cab("C1"), Cab("C2"));
        run.GapMm = 3f;

        var layout = Build(run);

        Assert.AreEqual(3f, layout.Steps[1].Spec.Against.Single(a => a.Target == "C1").GapMm);
        Assert.AreEqual(0f, layout.Steps[0].Spec.Align.Single().OffsetMm, "gap_mm — между соседями, не отступ от стены");
    }

    [Test]
    public void Build_WallCabinet_HangsAtTheKindsHeight_AndABaseLiftIsAddedOn()
    {
        var layout = Build(Run(Cab("W1", "wall"), Cab("B1", "base")), lift: 100f);

        Assert.AreEqual(RunKindDefaults.WallHangsAboveFloorMm + 100f, layout.Steps[0].Spec.LiftMm);
        Assert.AreEqual(100f, layout.Steps[1].Spec.LiftMm, "напольный шкаф стоит на base_y, без подвеса");
        Assert.AreEqual("floor", layout.Steps[0].Spec.On,
            "высота задана явно: существующую деталь иначе оставили бы на старой высоте");
    }

    [Test]
    public void Build_KindDefaults_FillTheMissingHeightAndDepth_AndExplicitValuesWin()
    {
        var tall = Cab("T1", "tall");
        tall.HeightMm = 2300;

        var layout = Build(Run(Cab("W1", "wall"), tall));

        Assert.AreEqual(RunKindDefaults.WallHeightMm, layout.Steps[0].Size.HeightMm);
        Assert.AreEqual(RunKindDefaults.WallDepthMm, layout.Steps[0].Size.DepthMm);
        Assert.AreEqual(2300, layout.Steps[1].Size.HeightMm, "явная высота сильнее умолчания вида");
        Assert.AreEqual(RunKindDefaults.TallDepthMm, layout.Steps[1].Size.DepthMm);
    }

    [Test]
    public void Build_MarksWhichCabinetsAreNew_ByTheExistsPredicate()
    {
        var layout = Build(Run(Cab("C1"), Cab("C2")), existing: "C1");

        Assert.IsFalse(layout.Steps[0].IsNew, "C1 уже в сцене: двигается и меняет размер, не создаётся");
        Assert.IsTrue(layout.Steps[1].IsNew);
    }

    [Test]
    public void Build_RotationFollowsTheSideOfTheRoom()
    {
        Assert.AreEqual(0f, Build(Run(Cab("C1")), WallAlongX(true)).Steps[0].RotYDeg);
        Assert.AreEqual(180f, Build(Run(Cab("C1")), WallAlongX(false)).Steps[0].RotYDeg);
        Assert.AreEqual(90f, Build(Run(Cab("C1")), WallAlongZ(true)).Steps[0].RotYDeg);
        Assert.AreEqual(270f, Build(Run(Cab("C1")), WallAlongZ(false)).Steps[0].RotYDeg);
    }

    [Test]
    public void Build_AWallAlongZ_UsesTheZAxisAndTheXFaces()
    {
        var layout = Build(Run(Cab("C1"), Cab("C2")), WallAlongZ(true));

        Assert.AreEqual("z", layout.Steps[0].Spec.Align.Single().Axis);
        Assert.AreEqual("right", layout.Steps[0].Spec.Against.Single().Face, "комната со стороны x+ — правая грань стены");
        Assert.AreEqual("front", layout.Steps[1].Spec.Against.Single(a => a.Target == "C1").Face, "ряд растёт к большему z");
    }

    [Test]
    public void Build_ARunLongerThanTheWall_RefusesWithTheArithmeticAndAWayOut()
    {
        var layout = Build(Run(Cab("C1", width: 2000), Cab("C2", width: 2000), Cab("C3", width: 500)));

        Assert.IsFalse(layout.Ok);
        var message = string.Join(" | ", layout.Problems);
        StringAssert.Contains("500 mm longer", message, "насколько не влезло");
        StringAssert.Contains("4500", message, "сколько нужно");
        StringAssert.Contains("start_mm", message, "что можно подкрутить");
        Assert.IsEmpty(layout.Steps, "отказ не оставляет полуготовых шагов");
    }

    [Test]
    public void Build_StartMmCountsTowardTheLength()
    {
        var run = Run(Cab("C1", width: 2000), Cab("C2", width: 2000));
        run.StartMm = 100f;

        Assert.IsFalse(Build(run).Ok, "2000+2000+100 > 4000");
        run.StartMm = 0f;
        Assert.IsTrue(Build(run).Ok, "ровно по длине стены — влезает");
    }

    [Test]
    public void Build_FromAPart_MeasuresTheFreeLengthFromItsFarSide()
    {
        var run = Run(Cab("C1", width: 2000));
        run.From = "Fridge";
        var fridge = new BoxMm(new Vector3(0f, 0f, 100f), new Vector3(2500f, 1800f, 700f));

        var layout = Build(run, part: fridge);

        Assert.IsFalse(layout.Ok, "после холодильника до конца стены 1500, шкаф 2000");
        StringAssert.Contains("far side of 'Fridge'", string.Join(" | ", layout.Problems));
    }

    [Test]
    public void Build_BadInput_ReportsEverythingAtOnce()
    {
        var run = Run(Cab("C 1"), Cab("C2", "island"), Cab("C2", width: 0));
        run.Id = "ro w";
        run.GapMm = -1f;

        var layout = Build(run);

        Assert.IsFalse(layout.Ok);
        var all = string.Join(" | ", layout.Problems);
        StringAssert.Contains("id 'ro w'", all);
        StringAssert.Contains("gap_mm -1", all);
        StringAssert.Contains("module 1 'C 1'", all);
        StringAssert.Contains("kind 'island' is not one of base|wall|tall", all);
        StringAssert.Contains("width_mm must be at least 1", all);
        StringAssert.Contains("used twice", all, "дубль имени: в ряду один шкаф — одно имя");
    }

    [Test]
    public void Build_EmptyModules_RefusesInsteadOfSilentlyDeletingTheRun()
    {
        var layout = Build(Run());

        Assert.IsFalse(layout.Ok);
        StringAssert.Contains("modules is empty", layout.Problems.Single());
    }

    [Test]
    public void Build_ResolvedSizes_FollowTheDeclarationOrder()
    {
        var layout = Build(Run(Cab("A", width: 400), Cab("B", width: 800), Cab("C", width: 500)));

        CollectionAssert.AreEqual(new[] { 400, 800, 500 }, layout.Modules.Select(m => m.WidthMm).ToArray());
        CollectionAssert.AreEqual(new[] { "A", "B", "C" }, layout.Steps.Select(s => s.Spec.Name).ToArray());
    }
}
