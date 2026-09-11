using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

/// <summary>Корпус стиральной и сушильной машины. Реализация ОДНА, а названий
/// два — приём тот же, что у ящика Movento (<see cref="DrawerSystem"/>): вид
/// приезжает перечислением, а не вторым классом.
///
/// Общего у двух машин всё, кроме ОДНОГО числа — диаметра люка: у сушильной он
/// больше. Числа названы (<c>WASHER_HATCH_DIAMETER_MM</c>,
/// <c>DRYER_HATCH_DIAMETER_MM</c>), от них же считаются обод, стекло и барабан,
/// поэтому вид не может подействовать на что-то ещё незаметно — это здесь и
/// проверяется, в обе стороны.
///
/// Размеры берутся НЕсимметричные (`conventions/SHAPE-AND-SCREENSHOTS.md` →
/// «A mesh and its metadata must describe the SAME shape»): на кубе 600×600×600
/// перепутанные оси дают тот же ответ, что и правильные.</summary>
public class LaundryMachineBodyTests
{
    private static readonly Vector3Int Asymmetric = new Vector3Int(700, 900, 550);

    private const LaundryMachineKind Washer = LaundryMachineKind.Washer;
    private const LaundryMachineKind Dryer = LaundryMachineKind.Dryer;

    [Test]
    public void DefaultDimensions_AreSixHundredByEightFiftyBySixHundred()
    {
        Assert.AreEqual(new Vector3Int(600, 850, 600), LaundryMachineBody.DefaultDimensionsMM,
            "размеры по умолчанию заданы пользователем: 600 × 850 × 600 мм");
    }

    [Test]
    public void NameOf_TellsTheTwoKindsApart()
    {
        Assert.AreEqual(LaundryMachineBody.WasherName, LaundryMachineBody.NameOf(Washer));
        Assert.AreEqual(LaundryMachineBody.DryerName, LaundryMachineBody.NameOf(Dryer));
        Assert.AreNotEqual(LaundryMachineBody.NameOf(Washer), LaundryMachineBody.NameOf(Dryer),
            "два вида отличаются названием — если названия совпали, различать нечем");
    }

    [Test]
    public void TypeId_AndTryKindOf_AgreeInBothDirections()
    {
        foreach (var kind in new[] { Washer, Dryer })
        {
            Assert.IsTrue(LaundryMachineBody.TryKindOf(LaundryMachineBody.TypeId(kind), out var back),
                "имя типа, выданное самой таблицей, обязано ею же читаться: " + kind);
            Assert.AreEqual(kind, back, "круг «вид → имя типа → вид» потерял вид: " + kind);
        }

        Assert.IsFalse(LaundryMachineBody.TryKindOf("dishwasher", out _),
            "чужое имя типа обязано быть ОТКАЗОМ, а не молчаливой стиральной машиной");
        Assert.IsFalse(LaundryMachineBody.TryKindOf("", out _), "пустая строка — тоже отказ");
    }

    [Test]
    public void TheDryerHatch_IsWiderThanTheWasherHatch()
    {
        Assert.Greater(LaundryMachineBody.DRYER_HATCH_DIAMETER_MM,
            LaundryMachineBody.WASHER_HATCH_DIAMETER_MM,
            "у сушильной люк больше — это единственное, чем машины отличаются размером");

        var dims = LaundryMachineBody.DefaultDimensionsMM;
        Assert.AreEqual(LaundryMachineBody.WASHER_HATCH_DIAMETER_MM,
            LaundryMachineBody.HatchDiameterMM(Washer, dims), 0.001f,
            "на обычном корпусе люк берётся номинальным, а не выводится из ширины");
        Assert.AreEqual(LaundryMachineBody.DRYER_HATCH_DIAMETER_MM,
            LaundryMachineBody.HatchDiameterMM(Dryer, dims), 0.001f);
    }

    [Test]
    public void TheRimAndTheDrum_FollowTheHatch_SoTheKindReachesThemToo()
    {
        var dims = LaundryMachineBody.DefaultDimensionsMM;

        foreach (var kind in new[] { Washer, Dryer })
            Assert.AreEqual(2 * LaundryMachineBody.HATCH_RIM_WIDTH_MM,
                LaundryMachineBody.HatchDiameterMM(kind, dims)
                    - LaundryMachineBody.GlassDiameterMM(kind, dims), 0.001f,
                kind + ": обод считается ОТ люка, иначе у второго вида кольцо поедет");

        Assert.Greater(LaundryMachineBody.DrumDiameterMM(Dryer, dims),
            LaundryMachineBody.DrumDiameterMM(Washer, dims),
            "барабан идёт за люком: больше люк — шире углубление");
    }

    [Test]
    public void TheKind_ChangesTheHatchAndNothingElse()
    {
        var dims = LaundryMachineBody.DefaultDimensionsMM;
        var washer = LaundryMachineBody.ClosedPartsMM(Washer, dims);
        var dryer = LaundryMachineBody.ClosedPartsMM(Dryer, dims);

        Assert.AreEqual(washer[LaundryMachineBody.IdxShell], dryer[LaundryMachineBody.IdxShell],
            "корпус у двух машин один и тот же");
        Assert.AreEqual(washer[LaundryMachineBody.IdxControlPanel],
            dryer[LaundryMachineBody.IdxControlPanel],
            "панель управления у двух машин одна и та же");
        Assert.AreNotEqual(washer[LaundryMachineBody.IdxHatchRim].sizeMM,
            dryer[LaundryMachineBody.IdxHatchRim].sizeMM,
            "а люк — разный; равные обода означали бы, что вид до геометрии не доехал");
        Assert.AreEqual(washer[LaundryMachineBody.IdxHatchRim].centerMM,
            dryer[LaundryMachineBody.IdxHatchRim].centerMM,
            "люк у обоих в одном месте — отличается только диаметр");
    }

    [Test]
    public void ANarrowBody_ShrinksTheHatch_SoItCannotOutgrowTheFront()
    {
        var narrow = new Vector3Int(LaundryMachineBody.MIN_WIDTH_MM, 850, 600);
        float hatch = LaundryMachineBody.HatchDiameterMM(Dryer, narrow);

        Assert.Less(hatch, LaundryMachineBody.DRYER_HATCH_DIAMETER_MM,
            "в узкий корпус номинальный люк не влезает и обязан ужаться");
        Assert.AreEqual(narrow.x - 2 * LaundryMachineBody.HATCH_MARGIN_MM, hatch, 0.001f,
            "ужимается ровно до ширины без двух отступов");
        Assert.Greater(LaundryMachineBody.GlassDiameterMM(Dryer, narrow), 0f,
            "и стекло при этом обязано остаться видимым");
    }

    [Test]
    public void ClosedPose_HasNoTwoSurfacesFightingForTheSamePixel()
    {
        foreach (var kind in new[] { Washer, Dryer })
        {
            var fights = CoplanarSurfaceDetector.Fights(
                LaundryMachineBody.ClosedPartsMM(kind, Asymmetric), LaundryMachineBody.PartName);

            Assert.IsEmpty(fights,
                kind + ": две грани смотрят в одну сторону из одной плоскости и перекрываются "
                + "площадью — пользователь увидит мерцание (z-fighting). Найдено:\n"
                + string.Join("\n", fights));
        }
    }

    [Test]
    public void EveryPart_StaysInsideTheDeclaredBox()
    {
        var d = Asymmetric;
        foreach (var kind in new[] { Washer, Dryer })
            foreach (var (centerMM, sizeMM) in LaundryMachineBody.ClosedPartsMM(kind, d))
                for (int axis = 0; axis < 3; axis++)
                {
                    float half = d[axis] * 0.5f;
                    Assert.LessOrEqual(centerMM[axis] + sizeMM[axis] * 0.5f, half + 0.001f,
                        kind + ": деталь вышла за объявленную коробку по оси " + "XYZ"[axis]
                        + " — валидация судит по коробке, и всё, что снаружи, для неё не существует");
                    Assert.GreaterOrEqual(centerMM[axis] - sizeMM[axis] * 0.5f, -half - 0.001f,
                        kind + ": деталь вышла за объявленную коробку по оси " + "XYZ"[axis]);
                }
    }

    [Test]
    public void Geometry_FollowsTheDimensions_OnEveryAxis()
    {
        var large = LaundryMachineBody.ClosedPartsMM(Washer, new Vector3Int(900, 1100, 700));
        var shell = large[LaundryMachineBody.IdxShell];

        Assert.AreEqual(900f, shell.sizeMM.x, 0.001f, "ширина корпуса идёт за шириной элемента");
        Assert.AreEqual(1100f, shell.sizeMM.y, 0.001f, "высота корпуса идёт за высотой элемента");
        Assert.AreEqual(700f - LaundryMachineBody.FRONT_FACE_SETBACK_MM, shell.sizeMM.z, 0.001f,
            "глубина корпуса — глубина элемента без выступа люка");
    }

    [Test]
    public void ControlPanel_SitsAtTheTopOfTheFront_AndStandsProudOfTheShell()
    {
        var parts = LaundryMachineBody.ClosedPartsMM(Washer, Asymmetric);
        var panel = parts[LaundryMachineBody.IdxControlPanel];
        var shell = parts[LaundryMachineBody.IdxShell];

        float halfH = Asymmetric.y * 0.5f;
        Assert.AreEqual(halfH, panel.centerMM.y + panel.sizeMM.y * 0.5f, 0.001f,
            "панель управления прижата к верху");
        Assert.AreEqual(shell.centerMM.z + shell.sizeMM.z * 0.5f,
            panel.centerMM.z - panel.sizeMM.z * 0.5f, 0.001f,
            "панель лежит НА лицевой грани корпуса, а не кончается в ней");
    }

    [Test]
    public void TheHatch_IsRound_AndItsGlassSitsInsideItsRim()
    {
        var parts = LaundryMachineBody.ClosedPartsMM(Washer, Asymmetric);
        var rim = parts[LaundryMachineBody.IdxHatchRim];
        var glass = parts[LaundryMachineBody.IdxHatchGlass];

        Assert.AreEqual(rim.sizeMM.x, rim.sizeMM.y, 0.001f,
            "люк КРУГЛЫЙ: ширина и высота обода равны, иначе это овал или прямоугольник");
        Assert.AreEqual(glass.sizeMM.x, glass.sizeMM.y, 0.001f, "стекло люка тоже круглое");
        Assert.AreEqual(2 * LaundryMachineBody.HATCH_RIM_WIDTH_MM,
            rim.sizeMM.x - glass.sizeMM.x, 0.001f,
            "стекло уже обода ровно на две ширины обода — иначе ободка не видно");

        Assert.IsTrue(LaundryMachineBody.IsRound(LaundryMachineBody.IdxHatchRim)
            && LaundryMachineBody.IsRound(LaundryMachineBody.IdxHatchGlass)
            && LaundryMachineBody.IsRound(LaundryMachineBody.IdxDrumBack),
            "обод, стекло и дно барабана круглые — по этому флагу им ставится диск");
        Assert.IsFalse(LaundryMachineBody.IsRound(LaundryMachineBody.IdxControlPanel),
            "противоположный вход: панель управления — коробка, и диск ей не полагается");
        Assert.IsTrue(LaundryMachineBody.IsBored(LaundryMachineBody.IdxShell),
            "корпус — единственная деталь с расточкой под барабан");
        Assert.IsFalse(LaundryMachineBody.IsBored(LaundryMachineBody.IdxHatchRim));
    }

    [Test]
    public void TheHatch_StandsProudOfTheShellFace_SoNothingFlickers()
    {
        var parts = LaundryMachineBody.ClosedPartsMM(Washer, Asymmetric);
        var shell = parts[LaundryMachineBody.IdxShell];
        var rim = parts[LaundryMachineBody.IdxHatchRim];
        var glass = parts[LaundryMachineBody.IdxHatchGlass];

        float shellFace = shell.centerMM.z + shell.sizeMM.z * 0.5f;
        float rimFace = rim.centerMM.z + rim.sizeMM.z * 0.5f;
        float glassFace = glass.centerMM.z + glass.sizeMM.z * 0.5f;

        Assert.Greater(rimFace, shellFace, "обод люка выступает вперёд лицевой грани корпуса");
        Assert.AreEqual(LaundryMachineBody.GLASS_PROUD_MM, glassFace - rimFace, 0.001f,
            "стекло выступает вперёд обода на свою объявленную величину — "
            + "иначе тёмного круга в белом кольце не видно");
        Assert.AreEqual(Asymmetric.z * 0.5f, glassFace, 0.001f,
            "стекло — самая передняя точка машины и лежит ровно на её объявленной грани");
    }

    [Test]
    public void TheDrum_IsACylindricalRecess_IntoTheShell()
    {
        var dims = LaundryMachineBody.DefaultDimensionsMM;
        float depth = LaundryMachineBody.DrumDepthMM(dims);
        var shell = LaundryMachineBody.BodyPartsMM(Washer, dims)[LaundryMachineBody.IdxShell];

        Assert.Greater(depth, 0f, "барабан — УГЛУБЛЕНИЕ, а не плоская стенка: глубина ненулевая");
        Assert.LessOrEqual(depth, shell.sizeMM.z - LaundryMachineBody.DRUM_BACK_WALL_MM,
            "расточка обязана оставить корпусу заднюю стенку, иначе барабан просверлит машину "
            + "насквозь и сзади появится дыра");
        Assert.AreEqual(LaundryMachineBody.GlassDiameterMM(Washer, dims),
            LaundryMachineBody.DrumDiameterMM(Washer, dims), 0.001f,
            "устье барабана ровно под стеклом: закрытый люк обязан закрывать дыру целиком");
    }

    [Test]
    public void TheDrumBack_SitsAtTheBottomOfTheRecess_NotOnItsWall()
    {
        var dims = LaundryMachineBody.DefaultDimensionsMM;
        var parts = LaundryMachineBody.ClosedPartsMM(Washer, dims);
        var shell = parts[LaundryMachineBody.IdxShell];
        var back = parts[LaundryMachineBody.IdxDrumBack];

        float shellFace = shell.centerMM.z + shell.sizeMM.z * 0.5f;
        float bottom = shellFace - LaundryMachineBody.DrumDepthMM(dims);

        Assert.AreEqual(bottom + LaundryMachineBody.DRUM_BACK_GAP_MM,
            back.centerMM.z - back.sizeMM.z * 0.5f, 0.001f,
            "дно барабана стоит на дне расточки с объявленным зазором, а не в её плоскости");
        Assert.Less(back.sizeMM.x, LaundryMachineBody.DrumDiameterMM(Washer, dims),
            "дно уже устья: иначе его кромка совпала бы со стенкой расточки");
    }

    [Test]
    public void Hinge_SitsOnTheLeftEdgeOfTheHatchItself_AtTheShellFace()
    {
        var hinge = LaundryMachineBody.HingeLocalMM(Washer, Asymmetric);
        float hatchHalf = LaundryMachineBody.HatchDiameterMM(Washer, Asymmetric) * 0.5f;

        Assert.AreEqual(-hatchHalf, hinge.x, 0.001f,
            "петля на левой кромке ЛЮКА, а не корпуса: люк круглый и уже передней грани, "
            + "петля по кромке корпуса развернула бы его вокруг пустоты");
        Assert.Greater(hinge.x, -Asymmetric.x * 0.5f,
            "и потому она заведомо правее левой стенки машины");
        Assert.AreEqual(LaundryMachineBody.HatchCenterYMM, hinge.y, 0.001f,
            "петля на высоте центра люка — вертикальная ось вращения");

        var rim = LaundryMachineBody.DoorPartsMM(Washer, Asymmetric)[0];
        Assert.AreEqual(rim.centerMM.z - rim.sizeMM.z * 0.5f, hinge.z, 0.001f,
            "петля на задней плоскости люка, иначе при открывании он въезжает в корпус");
    }

    [Test]
    public void Hinge_MovesWithTheHatch_NotWithTheShell()
    {
        var dims = LaundryMachineBody.DefaultDimensionsMM;
        float washer = LaundryMachineBody.HingeLocalMM(Washer, dims).x;
        float dryer = LaundryMachineBody.HingeLocalMM(Dryer, dims).x;

        Assert.Less(dryer, washer,
            "у сушильной люк шире, значит и петля левее — при одинаковом корпусе. "
            + "Равные петли означали бы, что люк вращается не вокруг себя");
        Assert.AreEqual(-LaundryMachineBody.HatchDiameterMM(Dryer, dims) * 0.5f, dryer, 0.001f);
    }

    [Test]
    public void Clamp_LiftsAnImpossibleSize_AndLeavesALegalOneAlone()
    {
        var legal = new Vector3Int(600, 850, 600);
        Assert.AreEqual(legal, LaundryMachineBody.ClampMM(legal),
            "законный размер обязан пройти нетронутым — иначе замок молча переписывает размер");

        var clamped = LaundryMachineBody.ClampMM(new Vector3Int(10, 10, 10));
        Assert.AreEqual(LaundryMachineBody.MIN_WIDTH_MM, clamped.x);
        Assert.AreEqual(LaundryMachineBody.MIN_HEIGHT_MM, clamped.y);
        Assert.AreEqual(LaundryMachineBody.MIN_DEPTH_MM, clamped.z);

        foreach (var kind in new[] { Washer, Dryer })
        {
            Assert.Greater(LaundryMachineBody.GlassDiameterMM(kind, clamped), 0f,
                kind + ": на самом маленьком законном размере стекло обязано остаться видимым");
            Assert.Greater(LaundryMachineBody.DrumDepthMM(clamped),
                LaundryMachineBody.DRUM_BACK_GAP_MM + LaundryMachineBody.DRUM_BACK_THICKNESS_MM,
                kind + ": и дно барабана обязано поместиться в расточку");
        }
    }

    [Test]
    public void PartName_NamesEveryPart_AndNamesThemApart()
    {
        var seen = new System.Collections.Generic.HashSet<string>();
        for (int i = 0; i < LaundryMachineBody.PartCount; i++)
        {
            var name = LaundryMachineBody.PartName(i);
            Assert.IsNotEmpty(name, "деталь без имени не найти ни в сцене, ни в сообщении теста");
            Assert.IsTrue(seen.Add(name), "две детали носят одно имя: " + name);
        }
    }
}
