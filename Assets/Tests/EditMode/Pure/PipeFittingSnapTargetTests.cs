using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;
using KitchenDesigner.Core.Plumbing;

/// <summary>ТРЕБОВАНИЕ, которого сегодня нет: «поднёс — повернулось — соединилось».
///
/// Соседний файл <c>PipeFittingSnapProbeTests</c> измеряет то, что есть, и потому
/// зелёный. Этот — описывает то, что нужно, и потому помечен <c>[Ignore]</c>: он не
/// падение сборки, а ЗАДАНИЕ. Снимите <c>[Ignore]</c> вместе с работой, которая его
/// закрывает; удалять тест, чтобы стало тихо, нельзя — тогда следующему агенту
/// придётся заново выяснять, почему муфта «как-то держится», а уголок нет.
///
/// Требование сформулировано через ПОРТЫ, а не через коробки, и это главное. Снэп
/// сегодня ровняет габаритные коробки, и совпадение портов у муфты — побочный
/// эффект того, что её устье случайно попало в центр грани. Правило, записанное
/// здесь, от формы коробки не зависит вовсе: сошлись устья — стык есть, не сошлись —
/// нет, и никакое «выглядит прижатым» этого не заменяет.
///
/// Проверять надо на УГОЛКЕ, а не на муфте. Муфта проходит уже сегодня, и тест на
/// ней был бы зелёным при полностью нерабочей посадке — ровно тот случай, про
/// который CONVENTIONS.md → «If a test would not go red, DELETE it».</summary>
public class PipeFittingSnapTargetTests
{
    private const float ToU = AppConstants.MM_TO_UNITS;

    private const int PipeLengthMm = 600;

    private static readonly PointMm PipeLowEndMm = new PointMm(0f, 0f, 0f);

    private sealed class PosedBox : IPosedGeometry
    {
        private readonly Vector3 _sizeUnits;
        public PosedBox(Vector3 sizeUnits) => _sizeUnits = sizeUnits;
        public ElementGeometry At(Vector3 position) =>
            ElementGeometry.Box("Fit", position, _sizeUnits);
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
        var pipe = ElementGeometry.Box("Run", new Vector3(0f, PipeLengthMm * 0.5f * ToU, 0f),
            PipeBoxUnits);
        var box = ElbowBoxUnits();
        var start = new Vector3(startXMm * ToU, -(box.y * 0.5f) - 10f * ToU, 0f);
        return SnapCore.TrySnap(new PosedBox(box), new List<ElementGeometry> { pipe }, start,
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
    [Ignore("Требование, а не регресс: посадки фитинга по портам в проекте пока нет. "
        + "Измерение сегодняшнего поведения — PipeFittingSnapProbeTests. Снять вместе "
        + "с работой, которая заводит посадку по портам.")]
    public void Elbow_BroughtToAPipeEnd_PutsOneOfItsPortsOnThatEnd()
    {
        var snap = SnapElbowUnderThePipe(0f);

        Assert.IsTrue(snap.snapped, "уголок обязан прилипать — он и сегодня прилипает");
        Assert.LessOrEqual(NearestElbowPortGapMm(snap.position), PipeJoint.JoinToleranceMm,
            "после посадки хотя бы одно устье уголка обязано лежать на торце трубы в "
            + "пределах допуска стыка: иначе PIP-01 продолжит считать конец открытым, а "
            + "пользователь будет видеть собранную трассу. Сегодня здесь 11,7 мм");
    }

    [Test]
    [Ignore("Требование, а не регресс: снэп выбирает засечку по кромкам коробок и про "
        + "ось трубы не знает. Снять вместе с работой, которая заводит посадку по портам.")]
    public void Elbow_BroughtInWellOffTheAxis_IsStillPulledOntoThePipeAxis()
    {
        var snap = SnapElbowUnderThePipe(12f);

        Assert.LessOrEqual(NearestElbowPortGapMm(snap.position), PipeJoint.JoinToleranceMm,
            "поднесённая мимо оси деталь обязана выводиться НА ось трубы, а не на "
            + "выравнивание кромок коробок: у фитинга ось — это устье порта, и полоса "
            + "захвата должна мериться от неё, а не от габарита");
    }
}
