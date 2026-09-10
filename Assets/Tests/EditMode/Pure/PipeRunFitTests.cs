using System.Collections.Generic;
using NUnit.Framework;
using KitchenDesigner.Core.Plumbing;

/// <summary>Труба Truba_2 и отвод Truba_pipe_elbow из проекта пользователя
/// (docs/example.save.json, сам файл тест не читает — числа сняты с него и заморожены).
///
/// Устья фитингов стоят на ДРОБНЫХ миллиметрах: нога отвода ДН20 равна 40,2 мм
/// (наружный 26,8 × 1,5), ступица смещена на −11,725 мм, поэтому устье отвода,
/// стоящего на 441,5 мм, попадает на 469,975 мм. Пролёт между устьями отвода и
/// тройника — 118,655 мм, а длина трубы целое число миллиметров — и не из-за
/// мм-сетки: MmGrid.OffsetToGrid отдаёт нулевой сдвиг всему, что несёт ISnapPorts,
/// то есть для труб и фитингов он no-op (ElementMoverSeatingOrderTests →
/// PipeWithPorts_IsLeftAloneByTheGrid_UnlikeAnOrdinaryPart). Целая она потому, что
/// PipeElement.LengthMM объявлен int. Поэтому труба длиной 118 мм, севшая
/// заподлицо на тройник, не достаёт до отвода 0,65 мм — при допуске стыка 0,5 мм.
/// Отвод показывает один порт занятым, второй пустым.</summary>
[TestFixture]
public class PipeRunFitTests
{
    private const string Pipe = "Truba_2";
    private const float ElbowMouthXMm = 469.9749773f;
    private const float TeeMouthXMm = 588.6299f;
    private const float YMm = 86.2250402f;
    private const float ZMm = 911.0858f;

    private static PipePort ElbowMouth() => new PipePort("Truba_pipe_elbow", PipeNodeKind.Elbow, 1,
        new PointMm(ElbowMouthXMm, YMm, ZMm), PipeAxis.Right);

    private static PipePort TeeMouth() => new PipePort("Obratka_1_pipe_tee_1", PipeNodeKind.Tee, 1,
        new PointMm(TeeMouthXMm, YMm, ZMm), PipeAxis.Left);

    private static PipePort[] EndsOf(float leftXMm, int lengthMm) => new[]
    {
        new PipePort(Pipe, PipeNodeKind.Pipe, 0, new PointMm(leftXMm, YMm, ZMm), PipeAxis.Left,
            "dn20"),
        new PipePort(Pipe, PipeNodeKind.Pipe, 1, new PointMm(leftXMm + lengthMm, YMm, ZMm),
            PipeAxis.Right, "dn20"),
    };

    [Test]
    public void TheSpanBetweenTheTwoMouths_IsNotAWholeMillimetre()
    {
        float span = ElbowMouth().PositionMm.DistanceMmTo(TeeMouth().PositionMm);
        Assert.AreEqual(118.655f, span, 0.002f,
            "пролёт между устьями дробный — целая труба его точно не закрывает");
    }

    [Test]
    public void AWholeMillimetrePipeSeatedOnTheTee_MissesTheElbow()
    {
        var ends = EndsOf(TeeMouthXMm - 118, 118);

        Assert.IsTrue(PipeJoint.Connects(ends[1], TeeMouth()), "у тройника труба сидит");
        Assert.IsFalse(PipeJoint.Connects(ends[0], ElbowMouth()),
            "именно это видит пользователь: до отвода 0,65 мм при допуске 0,5 мм");
        Assert.AreEqual(0.655f, ends[0].PositionMm.DistanceMmTo(ElbowMouth().PositionMm), 0.002f);
    }

    [Test]
    public void RefittingTheRun_ClosesBothJointsAtOnce()
    {
        var ends = EndsOf(TeeMouthXMm - 118, 118);
        var mouths = new List<PipePort> { ElbowMouth(), TeeMouth() };

        var fit = PipeRunFit.ForRun(ends[0], ends[1], mouths);
        Assert.IsNotNull(fit, "оба конца смотрят в устья ближе 2 мм — пролёт восстановим");

        var refitted = EndsOf(fit!.Value.CentreMm.XMm - fit.Value.LengthMm * 0.5f,
            fit.Value.LengthMm);
        Assert.AreEqual(119, fit.Value.LengthMm, "118,655 округляется вверх, а не отбрасывается");
        Assert.IsTrue(PipeJoint.Connects(refitted[0], ElbowMouth()), "отвод соединился");
        Assert.IsTrue(PipeJoint.Connects(refitted[1], TeeMouth()), "тройник остался соединён");
    }

    [Test]
    public void TheRoundingResidue_IsSplitBetweenTheTwoJoints()
    {
        var fit = PipeRunFit.Between(ElbowMouth().PositionMm, TeeMouth().PositionMm);

        Assert.AreEqual(0.1725f,
            fit.ResidueMm(ElbowMouth().PositionMm, TeeMouth().PositionMm), 0.002f,
            "остаток округления делится пополам, а не сваливается на один стык");
        Assert.IsTrue(fit.Holds(ElbowMouth().PositionMm, TeeMouth().PositionMm));
    }

    [Test]
    public void AHalfMillimetreSpan_StillHolds_AndTheWorstResidueIsUnderTheTolerance()
    {
        var a = new PointMm(0f, 0f, 0f);
        var b = new PointMm(100.5f, 0f, 0f);
        var fit = PipeRunFit.Between(a, b);

        Assert.AreEqual(101, fit.LengthMm);
        Assert.AreEqual(0.25f, fit.ResidueMm(a, b), 0.002f,
            "худший случай — ровно половина миллиметра пролёта, по 0,25 мм на стык");
        Assert.IsTrue(fit.Holds(a, b));
    }

    [Test]
    public void AnEndWithNoMouthWithinReach_IsNotRefitted()
    {
        var ends = EndsOf(TeeMouthXMm - 118, 118);
        var mouths = new List<PipePort> { TeeMouth() };

        Assert.IsNull(PipeRunFit.ForRun(ends[0], ends[1], mouths),
            "свободный конец трубы не повод менять её длину");
    }

    [Test]
    public void AMouthFartherThanTheReach_IsNotPickedUp()
    {
        var ends = EndsOf(TeeMouthXMm - 118, 118);
        var far = new PipePort("Otvod_daleko", PipeNodeKind.Elbow, 1,
            new PointMm(ElbowMouthXMm - 5f, YMm, ZMm), PipeAxis.Right);

        Assert.IsNull(PipeRunFit.ForRun(ends[0], ends[1], new List<PipePort> { far, TeeMouth() }),
            "устье в 5 мм — это другая деталь, а не разошедшийся стык");
    }

    [Test]
    public void AMouthFacingTheSameWay_IsNotAJoint()
    {
        var ends = EndsOf(TeeMouthXMm - 118, 118);
        var sameWay = new PipePort("Otvod_spinoy", PipeNodeKind.Elbow, 1,
            new PointMm(ElbowMouthXMm, YMm, ZMm), PipeAxis.Left);

        Assert.IsNull(PipeRunFit.ForRun(ends[0], ends[1],
                new List<PipePort> { sameWay, TeeMouth() }),
            "устье, смотрящее в ту же сторону, стыка не образует");
    }

    [Test]
    public void AnotherPipeEnd_IsNeverTakenForAJoint()
    {
        var ends = EndsOf(TeeMouthXMm - 118, 118);
        var otherPipe = new PipePort("Truba_9", PipeNodeKind.Pipe, 1,
            new PointMm(ElbowMouthXMm, YMm, ZMm), PipeAxis.Right, "dn20");

        Assert.IsNull(PipeRunFit.ForRun(ends[0], ends[1],
                new List<PipePort> { otherPipe, TeeMouth() }),
            "труба в трубу не соединяется — правило одно, в PipeConnectionRule");
    }
}
