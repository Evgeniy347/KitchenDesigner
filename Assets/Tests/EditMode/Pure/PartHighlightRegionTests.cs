using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;
using KitchenDesigner.Core.Plumbing;

/// <summary>Правило «покрась красным вот этот участок вот этого элемента»,
/// вынутое из подсветки кромок доски. Геометрия участка считается здесь, без
/// сцены: `SideHighlighter` и `PartHighlighter` только рисуют то, что решено тут.
///
/// Полоса у кромки доски и полоса у конца трубы — одно правило глубины
/// (`PartHighlightBands.DepthMM`), поэтому обе накладки одинаково ведут себя на
/// коротком и на длинном элементе. Устье фитинга — отдельная мерка: там нужен
/// именно КРАЙ ноги, а не её пятая часть.</summary>
public class PartHighlightRegionTests
{
    [Test]
    public void BandDepth_OnAShortSpan_IsAFifthOfIt()
    {
        Assert.AreEqual(20f, PartHighlightBands.DepthMM(100f), 1e-4f,
            "на коротком участке полоса задаётся долей: постоянные 50 мм съели бы "
            + "половину стомиллиметровой детали и перестали бы читаться как полоса");
    }

    [Test]
    public void BandDepth_OnALongSpan_IsCappedInMillimetres()
    {
        Assert.AreEqual(PartHighlightBands.BandMaxMM, PartHighlightBands.DepthMM(1000f), 1e-4f,
            "на длинной детали доля превратила бы полосу в закрашенную половину: "
            + "выше потолка растёт длина элемента, а не ширина подсветки");
    }

    [Test]
    public void BandDepth_OfNothing_IsNothing()
    {
        Assert.AreEqual(0f, PartHighlightBands.DepthMM(0f), 1e-4f,
            "нулевой пролёт не имеет участка: накладку нулевой глубины рисовать нечем");
        Assert.AreEqual(0f, PartHighlightBands.DepthMM(-10f), 1e-4f,
            "отрицательный пролёт — это не полоса наизнанку, а отсутствие полосы");
    }

    [Test]
    public void PipeEnd_Start_SitsAtTheNearTip_AndReachesInwards()
    {
        var sleeve = PipePartHighlight.PipeEnd(1000, 32f, PartEnd.Start);

        Assert.AreEqual(-500f, sleeve.ToMM.y, 1e-3f,
            "начало трубы — это её ближний торец в местных координатах, "
            + "труба центрирована в своём начале координат");
        Assert.AreEqual(-500f + PartHighlightBands.BandMaxMM, sleeve.FromMM.y, 1e-3f,
            "полоса уходит ВНУТРЬ трубы от торца, иначе она висит в воздухе за деталью");
        Assert.AreEqual(16f, sleeve.RadiusMM, 1e-3f,
            "гильза садится на наружный диаметр трубы: тоньше — утонет в меше, "
            + "толще — оторвётся от него");
    }

    [Test]
    public void PipeEnd_End_MirrorsTheStart()
    {
        var start = PipePartHighlight.PipeEnd(400, 32f, PartEnd.Start);
        var end = PipePartHighlight.PipeEnd(400, 32f, PartEnd.End);

        Assert.AreEqual(-start.ToMM.y, end.ToMM.y, 1e-3f,
            "конец — зеркало начала: два конца одной трубы обязаны подсвечиваться "
            + "одинаковой полосой с разных сторон");
        Assert.AreEqual(start.LengthMM, end.LengthMM, 1e-3f,
            "разная длина полос на двух концах читалась бы как разный смысл");
    }

    [Test]
    public void PipeEnd_OfAShortPipe_ShrinksWithIt()
    {
        var sleeve = PipePartHighlight.PipeEnd(100, 32f, PartEnd.End);

        Assert.AreEqual(20f, sleeve.LengthMM, 1e-3f,
            "стомиллиметровый огрызок трубы не может нести пятидесятимиллиметровую "
            + "полосу — это правило одно с кромкой доски");
    }

    [Test]
    public void PipeEnd_WithoutSection_IsEmpty()
    {
        Assert.IsTrue(PipePartHighlight.PipeEnd(1000, 0f, PartEnd.Start).IsEmpty,
            "труба без диаметра не даёт гильзы: рисовать нулевой цилиндр нечем");
        Assert.IsTrue(PipePartHighlight.PipeEnd(0, 32f, PartEnd.Start).IsEmpty,
            "труба нулевой длины не имеет ни начала, ни конца");
    }

    [Test]
    public void FittingMouth_EndsExactlyAtThePort()
    {
        var mouth = PipePartHighlight.FittingMouth(
            PipeNodeKind.Tee, PipeSpec.DEFAULT_SIZE, null, 1);
        var port = PipeFittingSpec.PortOffsetMm(PipeNodeKind.Tee, PipeSpec.DEFAULT_SIZE, 1);

        Assert.AreEqual(port.XMm, mouth.ToMM.x, 1e-3f,
            "полоса обязана заканчиваться там же, где порт: это ТОТ САМЫЙ край ноги, "
            + "на который человек наводится в списке портов");
        Assert.AreEqual(port.YMm, mouth.ToMM.y, 1e-3f);
        Assert.AreEqual(port.ZMm, mouth.ToMM.z, 1e-3f);
    }

    [Test]
    public void FittingMouth_IsTenMillimetresWide_ByANamedConstant()
    {
        var mouth = PipePartHighlight.FittingMouth(
            PipeNodeKind.Coupling, PipeSpec.DEFAULT_SIZE, null, 0);

        Assert.AreEqual(10f, PartHighlightBands.FittingMouthBandMM, 1e-4f,
            "ширина полосы у устья — именованная постоянная: её просят увеличить "
            + "глазами, и менять её надо в одном месте");
        Assert.AreEqual(PartHighlightBands.FittingMouthBandMM, mouth.LengthMM, 1e-3f,
            "устье — это КРАЙ ноги, а не её доля: у фитингов ноги короткие, и доля "
            + "покрасила бы ногу целиком");
    }

    [Test]
    public void FittingMouth_OnAStubbyLeg_NeverOutgrowsTheLeg()
    {
        float leg = PipeFittingSpec.LegLengthMm(PipeSpec.DEFAULT_SIZE);
        Assert.LessOrEqual(PartHighlightBands.MouthDepthMM(leg), leg,
            "полоса длиннее ноги вылезла бы из фитинга наружу");
        Assert.AreEqual(4f, PartHighlightBands.MouthDepthMM(4f), 1e-4f,
            "на ноге короче полосы красится вся нога, а не воздух за ней");
    }

    [Test]
    public void FittingMouth_EachPort_LooksItsOwnWay()
    {
        var first = PipePartHighlight.FittingMouth(
            PipeNodeKind.Tee, PipeSpec.DEFAULT_SIZE, null, 0);
        var second = PipePartHighlight.FittingMouth(
            PipeNodeKind.Tee, PipeSpec.DEFAULT_SIZE, null, 1);
        var third = PipePartHighlight.FittingMouth(
            PipeNodeKind.Tee, PipeSpec.DEFAULT_SIZE, null, 2);

        Assert.AreNotEqual(first.ToMM, second.ToMM,
            "у тройника три разных устья: одинаковые полосы не отличили бы порт от порта");
        Assert.AreNotEqual(second.ToMM, third.ToMM);
        Assert.AreNotEqual(first.ToMM, third.ToMM);
    }

    [Test]
    public void FittingMouth_OutsideThePortRange_IsEmpty()
    {
        Assert.IsTrue(PipePartHighlight
            .FittingMouth(PipeNodeKind.Cap, PipeSpec.DEFAULT_SIZE, null, 1).IsEmpty,
            "у заглушки один порт: подсветка несуществующего порта обязана быть пустой, "
            + "а не полосой на первой попавшейся ноге");
        Assert.IsTrue(PipePartHighlight
            .FittingMouth(PipeNodeKind.Tee, PipeSpec.DEFAULT_SIZE, null, -1).IsEmpty);
    }

    [Test]
    public void FittingMouth_TakesTheRadiusOfItsOwnBore()
    {
        var bores = new string?[] { PipeSpec.Dn15, PipeSpec.DEFAULT_SIZE };
        var narrow = PipePartHighlight.FittingMouth(
            PipeNodeKind.Coupling, PipeSpec.DEFAULT_SIZE, bores, 0);
        var wide = PipePartHighlight.FittingMouth(
            PipeNodeKind.Coupling, PipeSpec.DEFAULT_SIZE, bores, 1);

        Assert.AreEqual(PipeFittingSpec.BodyDiameterMm(bores[0]) * 0.5f, narrow.RadiusMM, 1e-3f,
            "у перехода ноги разной толщины: общий радиус утопил бы полосу в одной ноге "
            + "и оторвал бы от другой");
        Assert.AreEqual(PipeFittingSpec.BodyDiameterMm(bores[1]) * 0.5f, wide.RadiusMM, 1e-3f);
    }

    [Test]
    public void Sleeve_KnowsItsOwnCentreAndLength()
    {
        var sleeve = new HighlightSleeve(new Vector3(0f, 0f, 0f), new Vector3(0f, 10f, 0f), 5f);

        Assert.AreEqual(new Vector3(0f, 5f, 0f), sleeve.CentreMM,
            "цилиндр строится от центра: смещённый центр посадил бы полосу мимо участка");
        Assert.AreEqual(10f, sleeve.LengthMM, 1e-4f);
        Assert.IsFalse(sleeve.IsEmpty);
    }
}
