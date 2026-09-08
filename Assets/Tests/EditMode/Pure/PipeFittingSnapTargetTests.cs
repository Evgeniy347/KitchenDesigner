using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;
using KitchenDesigner.Core.Plumbing;

/// <summary>ТРЕБОВАНИЕ «поднёс — соединилось», записанное раньше кода и теперь
/// закрытое: <c>[Ignore]</c> снят вместе с посадкой по устьям (<c>SnapPortSeat</c>).
///
/// Требование сформулировано через ПОРТЫ, а не через коробки, и это главное. Снэп
/// раньше ровнял габаритные коробки, и совпадение портов у муфты было побочным
/// эффектом того, что её устье случайно попадало в центр грани. Правило, записанное
/// здесь, от формы коробки не зависит вовсе: сошлись устья — стык есть, не сошлись —
/// нет, и никакое «выглядит прижатым» этого не заменяет.
///
/// Проверяется УГОЛОК, а не муфта. Муфта проходила и до работы, и тест на ней был бы
/// зелёным при полностью нерабочей посадке — ровно тот случай, про который
/// CONVENTIONS.md → «If a test would not go red, DELETE it». Уголок же не соединялся
/// НИКОГДА и ни при каком повороте: промах был 11,73 мм при допуске 0,5 мм.
///
/// Стенд обязан отдавать устья: коробка без портов вернула бы старое поведение, и оба
/// теста снова стали бы красными — это и есть их доказательство красноты.</summary>
public class PipeFittingSnapTargetTests
{
    private const float ToU = AppConstants.MM_TO_UNITS;

    private const int PipeLengthMm = 600;

    private static readonly PointMm PipeLowEndMm = new PointMm(0f, 0f, 0f);

    private sealed class PosedElbow : IPosedGeometry
    {
        private readonly Vector3 _sizeUnits;
        public PosedElbow(Vector3 sizeUnits) => _sizeUnits = sizeUnits;
        public ElementGeometry At(Vector3 position) =>
            ElementGeometry.Box("Fit", position, _sizeUnits, Quaternion.identity, false,
                default, 0f,
                PipeSnapPorts.OfFitting(PipeNodeKind.Elbow, Quaternion.identity, position));
    }

    private static Vector3 PipeBoxUnits => new Vector3(
        PipeElementSpec.SectionMM(PipeSpec.DEFAULT_SIZE) * ToU,
        PipeLengthMm * ToU,
        PipeElementSpec.SectionMM(PipeSpec.DEFAULT_SIZE) * ToU);

    private static Vector3 ElbowBoxUnits()
    {
        var cover = PipeFittingLayout.CoverSizeMM(PipeNodeKind.Elbow, PipeSpec.DEFAULT_SIZE,
            PipeFittingLayout.UniformBores(PipeSpec.DEFAULT_SIZE));
        return new Vector3(
            PipeFittingSpec.RoundedMm(cover.x) * ToU,
            PipeFittingSpec.RoundedMm(cover.y) * ToU,
            PipeFittingSpec.RoundedMm(cover.z) * ToU);
    }

    private static SnapResult SnapElbowUnderThePipe(float startXMm)
    {
        var pipeCentre = new Vector3(0f, PipeLengthMm * 0.5f * ToU, 0f);
        var pipe = ElementGeometry.Box("Run", pipeCentre, PipeBoxUnits, Quaternion.identity,
            false, default, 0f,
            PipeSnapPorts.OfPipe(PipeLengthMm, Quaternion.identity, pipeCentre));
        var box = ElbowBoxUnits();
        var start = new Vector3(startXMm * ToU, -(box.y * 0.5f) - 10f * ToU, 0f);
        return SnapCore.TrySnap(new PosedElbow(box), new List<ElementGeometry> { pipe }, start,
            KitchenSettings.SNAP_THRESHOLD_DEFAULT_MM * ToU);
    }

    private static float NearestElbowPortGapMm(Vector3 snappedPos)
    {
        float best = float.MaxValue;
        for (int i = 0; i < PipeFittingSpec.PortCount(PipeNodeKind.Elbow); i++)
        {
            var offset = PipeFittingSpec.PortOffsetMm(PipeNodeKind.Elbow, PipeSpec.DEFAULT_SIZE, i);
            var world = new PointMm(
                snappedPos.x / ToU + offset.XMm,
                snappedPos.y / ToU + offset.YMm,
                snappedPos.z / ToU + offset.ZMm);
            float d = world.DistanceMmTo(PipeLowEndMm);
            if (d < best) best = d;
        }
        return best;
    }

    [Test]
    public void Elbow_BroughtToAPipeEnd_PutsOneOfItsPortsOnThatEnd()
    {
        var snap = SnapElbowUnderThePipe(0f);

        Assert.IsTrue(snap.snapped, "уголок обязан прилипать — он и сегодня прилипает");
        Assert.LessOrEqual(NearestElbowPortGapMm(snap.position), PipeJoint.JoinToleranceMm,
            "после посадки хотя бы одно устье уголка обязано лежать на торце трубы в "
            + "пределах допуска стыка: иначе PIP-01 продолжит считать конец открытым, а "
            + "пользователь будет видеть собранную трассу. До посадки по устьям здесь "
            + "было 11,7 мм при любом повороте");
    }

    [Test]
    public void Elbow_BroughtInWellOffTheAxis_IsStillPulledOntoThePipeAxis()
    {
        var snap = SnapElbowUnderThePipe(12f);

        Assert.LessOrEqual(NearestElbowPortGapMm(snap.position), PipeJoint.JoinToleranceMm,
            "поднесённая мимо оси деталь обязана выводиться НА ось трубы, а не на "
            + "выравнивание кромок коробок: у фитинга ось — это устье порта, и полоса "
            + "захвата должна мериться от неё, а не от габарита");
    }
}
