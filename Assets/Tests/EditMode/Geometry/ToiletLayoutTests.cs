using System.Linq;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

/// <summary>
/// Раскладка напольного унитаза-компакта. Считает ЧИСЛА, без сцены: пять
/// керамических деталей и хромированная кнопка обязаны сложиться в габарит
/// 360×790×660 без щелей и без взаимных наездов, при любой допустимой высоте
/// сиденья.
///
/// Единственный выведенный наружу параметр — высота сиденья над полом. Он
/// двигает три границы сразу (верх пьедестала, чашу и низ бачка), и именно эта
/// связка тут и проверяется: подрезка обязана оставлять бачку минимум 250 мм, а
/// пьедесталу — положительную высоту. Забыть один из двух концов легко, и тогда
/// «унитаз просто разваливается» на крайнем значении, а не на умолчальном.
/// </summary>
public class ToiletLayoutTests
{
    private static FurniturePartBox Part(FurniturePartBox[] parts, string name)
    {
        var found = parts.FirstOrDefault(p => p.Name == name);
        Assert.AreEqual(name, found.Name, "деталь «" + name + "» пропала из раскладки");
        return found;
    }

    private static float Bottom(FurniturePartBox part) =>
        part.CentreMM.y - part.ThicknessMM * 0.5f + ToiletLayout.HeightMM * 0.5f;

    private static float Top(FurniturePartBox part) =>
        part.CentreMM.y + part.ThicknessMM * 0.5f + ToiletLayout.HeightMM * 0.5f;

    private static float Back(FurniturePartBox part) =>
        part.CentreMM.z - part.ProfileDepthMM * 0.5f;

    private static float Front(FurniturePartBox part) =>
        part.CentreMM.z + part.ProfileDepthMM * 0.5f;

    [Test]
    public void Dimensions_AreTheCompactStandard()
    {
        Assert.AreEqual(new Vector3Int(360, 790, 660), ToiletLayout.DimensionsMM,
            "габарит компакта фиксированный — по нему считает и сайдбар, и MCP, и "
            + "раскладка ниже");
    }

    [Test]
    public void SeatHeight_IsClampedToTheRangeThatLeavesRoomForTheCistern()
    {
        Assert.AreEqual(ToiletLayout.MinSeatHeightMM, ToiletLayout.ClampSeatHeightMM(100),
            "ниже 350 мм унитаз перестаёт быть унитазом");
        Assert.AreEqual(ToiletLayout.MaxSeatHeightMM, ToiletLayout.ClampSeatHeightMM(10000),
            "верхний предел не выдуман: это ровно та высота, при которой бачку "
            + "остаётся минимум");
        Assert.AreEqual(502, ToiletLayout.MaxSeatHeightMM,
            "790 − 20 (сиденье) − 18 (крышка) − 250 (минимум бачка)");
    }

    [Test]
    public void Cistern_NeverShrinksBelowItsMinimum()
    {
        Assert.AreEqual(ToiletLayout.MinCisternHeightMM,
            ToiletLayout.CisternHeightMM(ToiletLayout.MaxSeatHeightMM),
            "на предельной высоте сиденья бачок обязан быть ровно минимальным — "
            + "иначе предел посчитан не по той формуле, что раскладка");
        Assert.Greater(ToiletLayout.CisternHeightMM(ToiletLayout.MaxSeatHeightMM + 50), 0,
            "неподрезанное значение снаружи не имеет права дать отрицательный бачок");
    }

    [Test]
    public void Pedestal_StaysPositive_EvenAtTheLowestSeat()
    {
        Assert.Greater(ToiletLayout.PedestalHeightMM(ToiletLayout.MinSeatHeightMM), 0,
            "пьедестал = высота сиденья минус тело чаши; на нижнем пределе он ещё "
            + "обязан существовать, иначе чаша висит в воздухе");
        Assert.AreEqual(140, ToiletLayout.PedestalHeightMM(ToiletLayout.MinSeatHeightMM),
            "350 − 210: числа выписаны, чтобы правка тела чаши не уехала молча");
        Assert.AreEqual(190, ToiletLayout.PedestalHeightMM(ToiletLayout.DefaultSeatHeightMM),
            "400 − 210 на заводской высоте");
    }

    [Test]
    public void CeramicParts_StackFromTheFloorToTheTop_WithoutGaps()
    {
        var parts = ToiletLayout.CeramicParts(ToiletLayout.DefaultSeatHeightMM);

        var pedestal = Part(parts, ToiletLayout.PedestalName);
        var bowl = Part(parts, ToiletLayout.BowlName);
        var seat = Part(parts, ToiletLayout.SeatName);
        var lid = Part(parts, ToiletLayout.LidName);
        var cistern = Part(parts, ToiletLayout.CisternName);

        Assert.AreEqual(0f, Bottom(pedestal), 0.01f, "пьедестал стоит на полу");
        Assert.AreEqual(Top(pedestal), Bottom(bowl), 0.01f, "чаша садится на пьедестал");
        Assert.AreEqual(Top(bowl), Bottom(seat), 0.01f, "сиденье лежит на ободе чаши");
        Assert.AreEqual(Top(seat), Bottom(lid), 0.01f, "крышка лежит на сиденье");
        Assert.AreEqual(Top(lid), Bottom(cistern), 0.01f,
            "бачок начинается сразу над крышкой — щель между ними видна на изометрии");
        Assert.AreEqual(ToiletLayout.HeightMM, Top(cistern), 0.01f,
            "верх бачка и есть верх габарита");
    }

    [Test]
    public void CeramicParts_FitInsideThePlan()
    {
        foreach (int seatHeight in new[] { ToiletLayout.MinSeatHeightMM,
            ToiletLayout.DefaultSeatHeightMM, ToiletLayout.MaxSeatHeightMM })
        foreach (var part in ToiletLayout.CeramicParts(seatHeight))
        {
            Assert.LessOrEqual(part.ProfileWidthMM, ToiletLayout.WidthMM,
                "деталь шире габарита: " + part.Name);
            Assert.GreaterOrEqual(Back(part), -ToiletLayout.DepthMM * 0.5f - 0.01f,
                "деталь вылезает назад за габарит: " + part.Name);
            Assert.LessOrEqual(Front(part), ToiletLayout.DepthMM * 0.5f + 0.01f,
                "деталь вылезает вперёд за габарит: " + part.Name);
            Assert.Greater(part.ThicknessMM, 0f,
                "деталь схлопнулась в ноль на краю диапазона: " + part.Name);
        }
    }

    [Test]
    public void Cistern_SitsAtTheBack_AndTheBowlInFront()
    {
        var parts = ToiletLayout.CeramicParts(ToiletLayout.DefaultSeatHeightMM);
        var cistern = Part(parts, ToiletLayout.CisternName);
        var bowl = Part(parts, ToiletLayout.BowlName);

        Assert.AreEqual(-ToiletLayout.DepthMM * 0.5f, Back(cistern), 0.01f,
            "бачок прижат к задней грани: этой гранью унитаз ставится к стене");
        Assert.AreEqual(Back(bowl), Front(cistern), 0.01f,
            "чаша начинается там, где кончается бачок");
        Assert.AreEqual(ToiletLayout.DepthMM * 0.5f, Front(bowl), 0.01f,
            "перед чаши и есть перед габарита");
    }

    [Test]
    public void FlushButton_IsRecessedIntoTheCisternTop_NotStickingOut()
    {
        var button = Part(ToiletLayout.ChromeParts(), ToiletLayout.ButtonName);

        Assert.AreEqual(ToiletLayout.HeightMM, Top(button), 0.01f,
            "кнопка заподлицо с крышкой бачка: торчащая кнопка выносит габарит вверх, "
            + "а он фиксированный");
        Assert.AreEqual(ToiletLayout.CisternCentreZMM, button.CentreMM.z, 0.01f,
            "кнопка по центру бачка, а не по центру всего унитаза");
        Assert.AreEqual(0f, button.CentreMM.x, 0.01f,
            "кнопка по оси симметрии унитаза");
    }

    [Test]
    public void ChromeAndCeramic_NeverNameTheSamePart()
    {
        var ceramic = ToiletLayout.CeramicParts(ToiletLayout.DefaultSeatHeightMM)
            .Select(p => p.Name).ToList();
        var chrome = ToiletLayout.ChromeParts().Select(p => p.Name).ToList();

        CollectionAssert.IsEmpty(ceramic.Intersect(chrome).ToList(),
            "имена деталей — ключи в FurniturePartSet, и наборы у двух слотов декора "
            + "разные: совпавшее имя означало бы, что один набор стирает деталь другого");
    }
}
