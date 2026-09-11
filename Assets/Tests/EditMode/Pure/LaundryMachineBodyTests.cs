using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

/// <summary>Корпус стиральной и сушильной машины. Реализация ОДНА, а названий
/// два — приём тот же, что у ящика Movento (<see cref="DrawerSystem"/>): вид
/// приезжает перечислением, а не вторым классом. Здесь проверяется то, что
/// иначе стало бы комментарием: размеры по умолчанию, геометрия, которая идёт
/// за размерами, и петля дверцы на ЛЕВОЙ кромке.
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
    public void ControlPanel_SitsAtTheTopOfTheFront_AndTheDoorTakesTheRest()
    {
        var parts = LaundryMachineBody.ClosedPartsMM(Asymmetric);
        var panel = parts[LaundryMachineBody.IdxControlPanel];
        var door = parts[LaundryMachineBody.IdxDoor];

        float halfH = Asymmetric.y * 0.5f;
        Assert.AreEqual(halfH, panel.centerMM.y + panel.sizeMM.y * 0.5f, 0.001f,
            "панель управления прижата к верху");
        Assert.AreEqual(panel.centerMM.y - panel.sizeMM.y * 0.5f,
            door.centerMM.y + door.sizeMM.y * 0.5f, 0.001f,
            "дверца начинается ровно там, где кончается панель — без щели и без нахлёста");
        Assert.AreEqual(-halfH, door.centerMM.y - door.sizeMM.y * 0.5f, 0.001f,
            "дверца доходит до низа");
    }

    [Test]
    public void Porthole_StandsInFrontOfTheDoorSlab_ByItsOwnThickness()
    {
        var parts = LaundryMachineBody.ClosedPartsMM(Asymmetric);
        var slab = parts[LaundryMachineBody.IdxDoor];
        var glass = parts[LaundryMachineBody.IdxPorthole];

        float slabFront = slab.centerMM.z + slab.sizeMM.z * 0.5f;
        float glassFront = glass.centerMM.z + glass.sizeMM.z * 0.5f;

        Assert.AreEqual(LaundryMachineBody.OVERLAY_THICKNESS_MM, glassFront - slabFront, 0.001f,
            "стекло люка стоит ПЕРЕД плитой двери ровно на свою толщину");
        Assert.AreEqual(LaundryMachineBody.FRONT_THICKNESS_MM,
            glassFront - (slab.centerMM.z - slab.sizeMM.z * 0.5f), 0.001f,
            "общая толщина передка не изменилась от того, что плита утоплена");
    }

    [Test]
    public void Hinge_SitsOnTheLeftEdge_AtTheBackOfTheDoor()
    {
        var hinge = LaundryMachineBody.HingeLocalMM(Asymmetric);

        Assert.AreEqual(-Asymmetric.x * 0.5f, hinge.x, 0.001f,
            "петля на ЛЕВОЙ кромке: дверца открывается влево-вперёд");
        Assert.AreEqual(Asymmetric.z * 0.5f - LaundryMachineBody.FRONT_THICKNESS_MM, hinge.z, 0.001f,
            "петля на задней плоскости передка, иначе дверца при открывании въезжает в корпус");

        var door = LaundryMachineBody.DoorPartsMM(Asymmetric)[0];
        Assert.AreEqual(door.centerMM.y, hinge.y, 0.001f,
            "петля на высоте середины дверцы — вертикальная ось вращения");
    }

    [Test]
    public void Hinge_MovesWithWidth()
    {
        Assert.AreEqual(-300f, LaundryMachineBody.HingeLocalMM(new Vector3Int(600, 850, 600)).x, 0.001f);
        Assert.AreEqual(-450f, LaundryMachineBody.HingeLocalMM(new Vector3Int(900, 850, 600)).x, 0.001f,
            "петля обязана ехать за шириной, иначе широкая машина открывается из середины корпуса");
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
