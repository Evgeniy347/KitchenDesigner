using System.Linq;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

/// <summary>
/// Раскладка подвесного унитаза (инсталляции). Здесь два выведенных наружу
/// параметра, и они СВЯЗАНЫ: панель смыва не имеет права опуститься на крышку,
/// поэтому её нижний предел считается от высоты чаши. Пара «оба значения
/// подрезаются, но по-разному» — то место, где легче всего получить панель,
/// растущую вместе с чашей за пределы габарита.
///
/// Габарит 360×1000×540 намеренно включает пустоту под чашей: чаша висит, но
/// сама инсталляция занимает стену от пола, и коробка от пола до верха панели —
/// это то, что нельзя занять другой мебелью. Габарит ФИКСИРОВАН, поэтому обе
/// подрезки обязаны держать детали внутри него при любых значениях.
/// </summary>
public class WallHungToiletLayoutTests
{
    private static FurniturePartBox Part(FurniturePartBox[] parts, string name)
    {
        var found = parts.FirstOrDefault(p => p.Name == name);
        Assert.AreEqual(name, found.Name, "деталь «" + name + "» пропала из раскладки");
        return found;
    }

    private static float Bottom(FurniturePartBox part) =>
        part.CentreMM.y - part.SizeMM.y * 0.5f + WallHungToiletLayout.HeightMM * 0.5f;

    private static float Top(FurniturePartBox part) =>
        part.CentreMM.y + part.SizeMM.y * 0.5f + WallHungToiletLayout.HeightMM * 0.5f;

    private static float Back(FurniturePartBox part) =>
        part.CentreMM.z - part.SizeMM.z * 0.5f;

    private static float Front(FurniturePartBox part) =>
        part.CentreMM.z + part.SizeMM.z * 0.5f;

    private static readonly int[] SeatHeights =
    {
        WallHungToiletLayout.MinSeatHeightMM,
        WallHungToiletLayout.DefaultSeatHeightMM,
        WallHungToiletLayout.MaxSeatHeightMM,
    };

    [Test]
    public void Dimensions_CoverTheWholeInstallationZone()
    {
        Assert.AreEqual(new Vector3Int(360, 1000, 540), WallHungToiletLayout.DimensionsMM,
            "габарит включает пустоту под чашей и всю зону панели смыва: бачок в стене "
            + "не моделируется, но место под инсталляцию занято от пола");
    }

    [Test]
    public void Bowl_HangsAboveTheFloor_AtEverySeatHeight()
    {
        foreach (int seat in SeatHeights)
        {
            var bowl = Part(WallHungToiletLayout.CeramicParts(seat),
                WallHungToiletLayout.BowlName);
            Assert.Greater(Bottom(bowl), 0f,
                "подвесной унитаз обязан висеть: на высоте сиденья " + seat
                + " мм чаша касается пола, а это уже напольный");
            Assert.AreEqual(seat, Top(bowl), 0.01f,
                "верх чаши и есть заданная высота установки");
        }
    }

    [Test]
    public void SeatAndLid_LieOnTheBowl()
    {
        int seat = WallHungToiletLayout.DefaultSeatHeightMM;
        var parts = WallHungToiletLayout.CeramicParts(seat);
        var bowl = Part(parts, WallHungToiletLayout.BowlName);
        var seatPart = Part(parts, WallHungToiletLayout.SeatName);
        var lid = Part(parts, WallHungToiletLayout.LidName);

        Assert.AreEqual(Top(bowl), Bottom(seatPart), 0.01f,
            "сиденье лежит на ободе чаши без щели");
        Assert.AreEqual(Top(seatPart), Bottom(lid), 0.01f,
            "крышка лежит на сиденье без щели");
        Assert.AreEqual(WallHungToiletLayout.LidTopMM(seat), Top(lid), 0.01f,
            "верх крышки — та отметка, от которой считается нижний предел панели смыва: "
            + "разойдись эти две формулы, и панель села бы на крышку");
    }

    [Test]
    public void PlateBottom_IsClampedAboveTheLid_AndBelowTheTop()
    {
        foreach (int seat in SeatHeights)
        {
            int low = WallHungToiletLayout.ClampPlateBottomMM(seat, 0);
            Assert.AreEqual(WallHungToiletLayout.MinPlateBottomMM(seat), low,
                "панель, опущенная в пол, обязана всплыть ровно на минимальный зазор "
                + "над крышкой — иначе она въезжает в крышку унитаза");
            Assert.GreaterOrEqual(low, WallHungToiletLayout.LidTopMM(seat)
                + WallHungToiletLayout.MinPlateGapAboveLidMM,
                "зазор над крышкой обязан остаться и на нижнем пределе");

            int high = WallHungToiletLayout.ClampPlateBottomMM(seat, 100000);
            Assert.AreEqual(WallHungToiletLayout.MaxPlateBottomMM, high,
                "панель, задранная выше габарита, обязана опуститься до верхнего предела");
            Assert.AreEqual(WallHungToiletLayout.HeightMM,
                high + WallHungToiletLayout.PlateFaceHeightMM,
                "верхний предел — ровно тот, при котором верх панели совпадает с верхом "
                + "габарита");
        }
    }

    [Test]
    public void RaisingTheSeat_PushesTheDefaultPlateUp()
    {
        int atDefault = WallHungToiletLayout.ClampPlateBottomMM(
            WallHungToiletLayout.DefaultSeatHeightMM,
            WallHungToiletLayout.DefaultPlateBottomMM);
        Assert.AreEqual(WallHungToiletLayout.DefaultPlateBottomMM, atDefault,
            "на заводских значениях подрезка не имеет права ничего трогать: иначе "
            + "умолчание расходится с тем, что видит пользователь");

        int atTop = WallHungToiletLayout.ClampPlateBottomMM(
            WallHungToiletLayout.MaxSeatHeightMM,
            WallHungToiletLayout.DefaultPlateBottomMM);
        Assert.Greater(atTop, WallHungToiletLayout.DefaultPlateBottomMM,
            "при самой высокой чаше умолчальная панель оказывается под крышкой и "
            + "обязана уехать вверх — связь между двумя параметрами односторонняя");
    }

    [Test]
    public void PlateAndButtons_SitOnTheWallSide_AndButtonsStandOnThePlate()
    {
        int seat = WallHungToiletLayout.DefaultSeatHeightMM;
        var parts = WallHungToiletLayout.ChromeParts(seat,
            WallHungToiletLayout.DefaultPlateBottomMM);

        var plate = Part(parts, WallHungToiletLayout.PlateName);
        var full = Part(parts, WallHungToiletLayout.ButtonLargeName);
        var half = Part(parts, WallHungToiletLayout.ButtonSmallName);

        Assert.AreEqual(-WallHungToiletLayout.DepthMM * 0.5f, Back(plate), 0.01f,
            "панель прижата к задней грани — этой гранью элемент садится на стену");
        Assert.AreEqual(Front(plate), Back(full), 0.01f,
            "кнопка выступает из лицевой плоскости панели, а не утоплена в стену");
        Assert.AreEqual(Front(plate), Back(half), 0.01f,
            "малая кнопка стоит на той же лицевой плоскости, что и большая");

        Assert.AreEqual(plate.CentreMM.y, full.CentreMM.y, 0.01f,
            "обе кнопки по вертикали центрированы на панели");
        Assert.AreEqual(plate.CentreMM.y, half.CentreMM.y, 0.01f,
            "малая кнопка не имеет права уехать по высоте относительно большой");
        Assert.Less(full.CentreMM.x, half.CentreMM.x,
            "большая кнопка слева, малая справа — порядок дуального смыва");
    }

    [Test]
    public void Buttons_StayInsideThePlate()
    {
        var parts = WallHungToiletLayout.ChromeParts(
            WallHungToiletLayout.DefaultSeatHeightMM,
            WallHungToiletLayout.DefaultPlateBottomMM);
        var plate = Part(parts, WallHungToiletLayout.PlateName);

        foreach (var name in new[] { WallHungToiletLayout.ButtonLargeName,
            WallHungToiletLayout.ButtonSmallName })
        {
            var button = Part(parts, name);
            Assert.GreaterOrEqual(button.CentreMM.x - button.SizeMM.x * 0.5f,
                plate.CentreMM.x - plate.SizeMM.x * 0.5f - 0.01f,
                "кнопка вылезает за левый край панели: " + name);
            Assert.LessOrEqual(button.CentreMM.x + button.SizeMM.x * 0.5f,
                plate.CentreMM.x + plate.SizeMM.x * 0.5f + 0.01f,
                "кнопка вылезает за правый край панели: " + name);
            Assert.Less(button.SizeMM.y, plate.SizeMM.y,
                "кнопка во всю высоту панели — это уже не панель с кнопками: " + name);
        }
    }

    [Test]
    public void EveryPart_StaysInsideTheFixedEnvelope()
    {
        foreach (int seat in SeatHeights)
        foreach (int plate in new[] { 0, WallHungToiletLayout.DefaultPlateBottomMM, 100000 })
        {
            var all = WallHungToiletLayout.CeramicParts(seat)
                .Concat(WallHungToiletLayout.ChromeParts(seat, plate)).ToArray();

            foreach (var part in all)
            {
                string what = part.Name + " при чаше " + seat + " и панели " + plate;
                Assert.GreaterOrEqual(Bottom(part), -0.01f,
                    "деталь проваливается под пол: " + what);
                Assert.LessOrEqual(Top(part), WallHungToiletLayout.HeightMM + 0.01f,
                    "деталь выше габарита: " + what);
                Assert.GreaterOrEqual(Back(part), -WallHungToiletLayout.DepthMM * 0.5f - 0.01f,
                    "деталь уходит в стену за габарит: " + what);
                Assert.LessOrEqual(Front(part), WallHungToiletLayout.DepthMM * 0.5f + 0.01f,
                    "деталь вылезает вперёд за габарит: " + what);
            }
        }
    }

    [Test]
    public void CeramicAndChrome_NeverNameTheSamePart()
    {
        var ceramic = WallHungToiletLayout
            .CeramicParts(WallHungToiletLayout.DefaultSeatHeightMM)
            .Select(p => p.Name).ToList();
        var chrome = WallHungToiletLayout
            .ChromeParts(WallHungToiletLayout.DefaultSeatHeightMM,
                WallHungToiletLayout.DefaultPlateBottomMM)
            .Select(p => p.Name).ToList();

        CollectionAssert.IsEmpty(ceramic.Intersect(chrome).ToList(),
            "два набора деталей живут в разных FurniturePartSet и различаются только "
            + "именами: совпадение означало бы, что один слот стирает деталь другого");
        CollectionAssert.IsEmpty(ceramic.Intersect(
            ToiletLayout.CeramicParts(ToiletLayout.DefaultSeatHeightMM)
                .Select(p => p.Name)).ToList(),
            "имена деталей напольного и подвесного тоже обязаны различаться: оба "
            + "варианта живут в одной сцене, а имена уезжают в снимок геометрии");
    }
}
