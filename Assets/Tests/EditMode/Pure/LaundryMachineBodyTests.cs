using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

/// <summary>Корпус стиральной и сушильной машины. Реализация ОДНА, а названий
/// два — приём тот же, что у ящика Movento (<see cref="DrawerSystem"/>): вид
/// приезжает перечислением, а не вторым классом. Здесь проверяется то, что
/// иначе стало бы комментарием: размеры по умолчанию, геометрия, которая идёт
/// за размерами, и КРУГЛЫЙ люк, петля которого лежит на его собственной левой
/// кромке, а не на кромке корпуса.
///
/// Размеры берутся НЕсимметричные (`conventions/SHAPE-AND-SCREENSHOTS.md` →
/// «A mesh and its metadata must describe the SAME shape»): на кубе 600×600×600
/// перепутанные оси дают тот же ответ, что и правильные.</summary>
public class LaundryMachineBodyTests
{
    private static readonly Vector3Int Asymmetric = new Vector3Int(700, 900, 550);

    [Test]
    public void DefaultDimensions_AreSixHundredByEightFiftyBySixHundred()
    {
        Assert.AreEqual(new Vector3Int(600, 850, 600), LaundryMachineBody.DefaultDimensionsMM,
            "размеры по умолчанию заданы пользователем: 600 × 850 × 600 мм");
    }

    [Test]
    public void NameOf_TellsTheTwoKindsApart()
    {
        Assert.AreEqual(LaundryMachineBody.WasherName,
            LaundryMachineBody.NameOf(LaundryMachineKind.Washer));
        Assert.AreEqual(LaundryMachineBody.DryerName,
            LaundryMachineBody.NameOf(LaundryMachineKind.Dryer));
        Assert.AreNotEqual(LaundryMachineBody.NameOf(LaundryMachineKind.Washer),
            LaundryMachineBody.NameOf(LaundryMachineKind.Dryer),
            "два вида отличаются ровно названием — если названия совпали, различать нечем");
    }

    [Test]
    public void TypeId_AndTryKindOf_AgreeInBothDirections()
    {
        foreach (var kind in new[] { LaundryMachineKind.Washer, LaundryMachineKind.Dryer })
        {
            Assert.IsTrue(LaundryMachineBody.TryKindOf(LaundryMachineBody.TypeId(kind), out var back),
                "имя типа, выданное самой таблицей, обязано ею же читаться: " + kind);
            Assert.AreEqual(kind, back, "круг «вид → имя типа → вид» потерял вид: " + kind);
        }

        Assert.IsFalse(LaundryMachineBody.TryKindOf("dishwasher", out _),
            "чужое имя типа обязано быть ОТКАЗОМ, а не молчаливой стиральной машиной");
        Assert.IsFalse(LaundryMachineBody.TryKindOf("", out _),
            "пустая строка — тоже отказ");
    }

    [Test]
    public void ClosedPose_HasNoTwoSurfacesFightingForTheSamePixel()
    {
        var fights = CoplanarSurfaceDetector.Fights(
            LaundryMachineBody.ClosedPartsMM(Asymmetric), LaundryMachineBody.PartName);

        Assert.IsEmpty(fights,
            "две грани смотрят в одну сторону из одной плоскости и перекрываются площадью — "
            + "пользователь увидит мерцание (z-fighting). Найдено:\n" + string.Join("\n", fights));
    }

    [Test]
    public void EveryPart_StaysInsideTheDeclaredBox()
    {
        var d = Asymmetric;
        foreach (var (centerMM, sizeMM) in LaundryMachineBody.ClosedPartsMM(d))
            for (int axis = 0; axis < 3; axis++)
            {
                float half = d[axis] * 0.5f;
                Assert.LessOrEqual(centerMM[axis] + sizeMM[axis] * 0.5f, half + 0.001f,
                    "деталь вышла за объявленную коробку по оси " + "XYZ"[axis]
                    + " — валидация судит по коробке, и всё, что снаружи, для неё не существует");
                Assert.GreaterOrEqual(centerMM[axis] - sizeMM[axis] * 0.5f, -half - 0.001f,
                    "деталь вышла за объявленную коробку по оси " + "XYZ"[axis]);
            }
    }

    [Test]
    public void Geometry_FollowsTheDimensions_OnEveryAxis()
    {
        var small = LaundryMachineBody.ClosedPartsMM(new Vector3Int(600, 850, 600));
        var large = LaundryMachineBody.ClosedPartsMM(new Vector3Int(900, 1100, 700));

        Assert.AreEqual(600f, small[LaundryMachineBody.IdxShell].sizeMM.x, 0.001f);
        Assert.AreEqual(900f, large[LaundryMachineBody.IdxShell].sizeMM.x, 0.001f,
            "ширина корпуса идёт за шириной элемента");
        Assert.AreEqual(1100f, large[LaundryMachineBody.IdxShell].sizeMM.y, 0.001f,
            "высота корпуса идёт за высотой элемента");
        Assert.AreEqual(700f - LaundryMachineBody.FRONT_THICKNESS_MM,
            large[LaundryMachineBody.IdxShell].sizeMM.z, 0.001f,
            "глубина корпуса — глубина элемента без передка");
    }

    [Test]
    public void ControlPanel_SitsAtTheTopOfTheFront_AndTheFrontPanelTakesTheRest()
    {
        var parts = LaundryMachineBody.ClosedPartsMM(Asymmetric);
        var panel = parts[LaundryMachineBody.IdxControlPanel];
        var front = parts[LaundryMachineBody.IdxFrontPanel];

        float halfH = Asymmetric.y * 0.5f;
        Assert.AreEqual(halfH, panel.centerMM.y + panel.sizeMM.y * 0.5f, 0.001f,
            "панель управления прижата к верху");
        Assert.AreEqual(panel.centerMM.y - panel.sizeMM.y * 0.5f,
            front.centerMM.y + front.sizeMM.y * 0.5f, 0.001f,
            "передняя стенка начинается ровно там, где кончается панель — без щели и нахлёста");
        Assert.AreEqual(-halfH, front.centerMM.y - front.sizeMM.y * 0.5f, 0.001f,
            "передняя стенка доходит до низа");
    }

    [Test]
    public void TheHatch_IsRound_AndItsGlassSitsInsideItsRim()
    {
        var parts = LaundryMachineBody.ClosedPartsMM(Asymmetric);
        var rim = parts[LaundryMachineBody.IdxHatchRim];
        var glass = parts[LaundryMachineBody.IdxHatchGlass];

        Assert.AreEqual(rim.sizeMM.x, rim.sizeMM.y, 0.001f,
            "люк КРУГЛЫЙ: ширина и высота обода равны, иначе это овал или прямоугольник");
        Assert.AreEqual(glass.sizeMM.x, glass.sizeMM.y, 0.001f,
            "стекло люка тоже круглое");
        Assert.AreEqual(2 * LaundryMachineBody.HATCH_RIM_WIDTH_MM,
            rim.sizeMM.x - glass.sizeMM.x, 0.001f,
            "стекло уже обода ровно на две ширины обода — иначе ободка не видно");
        Assert.IsTrue(LaundryMachineBody.IsRound(LaundryMachineBody.IdxHatchRim)
            && LaundryMachineBody.IsRound(LaundryMachineBody.IdxHatchGlass),
            "обе детали люка обязаны быть объявлены круглыми — по этому флагу им ставится диск");
        Assert.IsFalse(LaundryMachineBody.IsRound(LaundryMachineBody.IdxFrontPanel),
            "противоположный вход: передняя стенка — коробка, и диск ей не полагается");
    }

    [Test]
    public void TheHatch_StandsProudOfTheFrontPanel_SoNothingFlickers()
    {
        var parts = LaundryMachineBody.ClosedPartsMM(Asymmetric);
        var front = parts[LaundryMachineBody.IdxFrontPanel];
        var rim = parts[LaundryMachineBody.IdxHatchRim];
        var glass = parts[LaundryMachineBody.IdxHatchGlass];

        float frontFace = front.centerMM.z + front.sizeMM.z * 0.5f;
        float rimFace = rim.centerMM.z + rim.sizeMM.z * 0.5f;
        float glassFace = glass.centerMM.z + glass.sizeMM.z * 0.5f;

        Assert.Greater(rimFace, frontFace,
            "обод люка выступает вперёд передней стенки, а не кончается в её плоскости");
        Assert.Greater(glassFace, rimFace,
            "стекло выступает вперёд обода — иначе тёмного круга в белом кольце не видно");
        Assert.AreEqual(Asymmetric.z * 0.5f, glassFace, 0.001f,
            "стекло — самая передняя точка машины и лежит ровно на её объявленной грани");
    }

    [Test]
    public void TheHatchDiameter_FollowsTheSmallerOfWidthAndFrontHeight()
    {
        Assert.AreEqual(600 - 2 * LaundryMachineBody.HATCH_MARGIN_MM,
            LaundryMachineBody.HatchDiameterMM(new Vector3Int(600, 1400, 600)), 0.001f,
            "у высокой узкой машины диаметр люка ограничен ШИРИНОЙ");
        Assert.AreEqual(1400 - LaundryMachineBody.CONTROL_PANEL_HEIGHT_MM
                - 2 * LaundryMachineBody.HATCH_MARGIN_MM,
            LaundryMachineBody.HatchDiameterMM(new Vector3Int(2000, 1400, 600)), 0.001f,
            "у широкой низкой машины — ВЫСОТОЙ передней стенки; на квадрате эти два "
            + "правила неотличимы, поэтому вход намеренно не квадратный");
    }

    [Test]
    public void Hinge_SitsOnTheLeftEdgeOfTheHatchItself_AtItsBack()
    {
        var hinge = LaundryMachineBody.HingeLocalMM(Asymmetric);
        float hatchHalf = LaundryMachineBody.HatchDiameterMM(Asymmetric) * 0.5f;

        Assert.AreEqual(-hatchHalf, hinge.x, 0.001f,
            "петля на левой кромке ЛЮКА, а не корпуса: люк круглый и уже передней стенки, "
            + "петля по кромке корпуса развернула бы его вокруг пустоты");
        Assert.Greater(hinge.x, -Asymmetric.x * 0.5f,
            "и потому она заведомо правее левой стенки машины");
        Assert.AreEqual(LaundryMachineBody.HatchCenterYMM, hinge.y, 0.001f,
            "петля на высоте центра люка — вертикальная ось вращения");

        var rim = LaundryMachineBody.DoorPartsMM(Asymmetric)[0];
        Assert.AreEqual(rim.centerMM.z - rim.sizeMM.z * 0.5f, hinge.z, 0.001f,
            "петля на задней плоскости люка, иначе при открывании он въезжает в стенку");
    }

    [Test]
    public void Hinge_MovesWithTheHatch_NotWithTheShell()
    {
        float narrow = LaundryMachineBody.HingeLocalMM(new Vector3Int(600, 850, 600)).x;
        float wide = LaundryMachineBody.HingeLocalMM(new Vector3Int(900, 850, 600)).x;

        Assert.Less(wide, narrow,
            "шире машина — больше люк — левее его кромка; неподвижная петля означала бы, "
            + "что люк вращается не вокруг себя");
        Assert.AreEqual(-LaundryMachineBody.HatchDiameterMM(new Vector3Int(900, 850, 600)) * 0.5f,
            wide, 0.001f);
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
        Assert.Greater(LaundryMachineBody.GlassDiameterMM(clamped), 0f,
            "на самом маленьком законном размере стекло люка обязано остаться видимым");
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
