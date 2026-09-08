using System.Collections.Generic;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;
using KitchenDesigner.Core.Plumbing;

/// <summary>Что происходит, когда фитинг подносят к торцу трубы.
///
/// Вопрос пользователя — «я не понимаю, как крепить муфту/уголок/тройник/заглушку»
/// и «не могу уголок подсоединить к подаче». Он раскладывался на два РАЗНЫХ вопроса,
/// между которыми не было ни одной строки кода:
///
/// 1. ПРИЛИПАЕТ ли деталь визуально — работа снэпа, который ровнял ГАБАРИТНЫЕ КОРОБКИ
///    (<c>ElementGeometry.Box</c>): у трубы и у фитинга нет собственных граней,
///    <c>KitchenElement.GetFacesAt</c> отдаёт коробку по <c>DimensionsMM</c>.
/// 2. СОЕДИНЯЮТСЯ ли порты логически — работа <c>PipeJoint</c>: устья ближе
///    <c>Tolerance.ContactMm</c> = 0,5 мм и оси навстречу. Пока порты не сошлись,
///    <c>PIP-01</c> считает конец открытым, как бы красиво деталь ни встала.
///
/// Замер 2026-09-08 показал цену этого разрыва. Прилипало ВСЁ; стыковалось не всё, и
/// разница держалась на одной величине: ГДЕ УСТЬЕ ЛЕЖИТ ОТНОСИТЕЛЬНО ГРАНИ КОРОБКИ.
/// У муфты, заглушки, подачи и обратки устье попадало в центр грани, и стык выходил
/// ПОПУТНО; у уголка ни один из двух портов не центрован — устье уходит вдоль грани
/// на (длина плеча − радиус тела)/2 = 11,72 мм для DN20, то есть уголок не стыковался
/// НИКОГДА и ни при каком повороте. Плюс полоса захвата на ось у муфты была ±1,75 мм
/// с обрывом за краем: дальше побеждала засечка «кромка».
///
/// Теперь устье порта — полноправная геометрия элемента: <c>ElementGeometry.Ports</c>,
/// источник — <c>ISnapPorts</c> у самого элемента, посадка — <c>SnapPortSeat</c>, и она
/// идёт ДО граневых детентов. Тесты ниже фиксируют ИЗМЕРЕННОЕ поведение и станут
/// красными, если оно изменится в любую сторону: полоса захвата снова сожмётся до
/// полуразницы ширин коробок или устье перестанет доезжать до снимка.
///
/// Форма коробки в стык больше не входит ВООБЩЕ — это главное, что здесь проверяется,
/// и проверяется оно на уголке и тройнике, а не на муфте: муфта была зелёной и при
/// полностью нерабочей посадке (CONVENTIONS.md → «If a test would not go red,
/// DELETE it»).
///
/// Числа берутся ИЗ КОДА, а не из головы: коробка — <c>PipeFittingLayout</c>, устье —
/// <c>PipeFittingSpec.PortOffsetMm</c> через <c>PipeSnapPorts</c>, порог —
/// <c>KitchenSettings</c>.</summary>
public class PipeFittingSnapProbeTests
{
    private const float ToU = AppConstants.MM_TO_UNITS;

    private const int PipeLengthMm = 600;

    private static readonly PipeNodeKind[] PortCentredOnTheBoxFace =
    {
        PipeNodeKind.Coupling, PipeNodeKind.Cap, PipeNodeKind.Supply, PipeNodeKind.Return,
    };

    private sealed class PosedFitting : IPosedGeometry
    {
        private readonly PipeNodeKind _kind;
        private readonly Vector3 _sizeUnits;
        private readonly Quaternion _rotation;

        public PosedFitting(PipeNodeKind kind, Vector3 sizeUnits, Quaternion rotation)
        {
            _kind = kind;
            _sizeUnits = sizeUnits;
            _rotation = rotation;
        }

        public ElementGeometry At(Vector3 position) =>
            ElementGeometry.Box("Fit", position, _sizeUnits, _rotation, false, default, 0f,
                PipeSnapPorts.OfFitting(_kind, _rotation, position));
    }

    private sealed class PosedPipe : IPosedGeometry
    {
        private readonly int _lengthMm;

        public PosedPipe(int lengthMm) => _lengthMm = lengthMm;

        public ElementGeometry At(Vector3 position) =>
            ElementGeometry.Box("Run", position, PipeBoxUnits, Quaternion.identity, false,
                default, 0f, PipeSnapPorts.OfPipe(_lengthMm, Quaternion.identity, position));
    }

    private static float Threshold => KitchenSettings.SNAP_THRESHOLD_DEFAULT_MM * ToU;

    private static Vector3 PipeBoxUnits => new Vector3(
        PipeElementSpec.SectionMM(PipeSpec.DEFAULT_SIZE) * ToU,
        PipeLengthMm * ToU,
        PipeElementSpec.SectionMM(PipeSpec.DEFAULT_SIZE) * ToU);

    private static Vector3 FittingBoxUnits(PipeNodeKind kind)
    {
        var cover = PipeFittingLayout.CoverSizeMM(kind, PipeSpec.DEFAULT_SIZE,
            PipeFittingLayout.UniformBores(PipeSpec.DEFAULT_SIZE));
        return new Vector3(
            PipeFittingSpec.RoundedMm(cover.x) * ToU,
            PipeFittingSpec.RoundedMm(cover.y) * ToU,
            PipeFittingSpec.RoundedMm(cover.z) * ToU);
    }

    private static Vector3 PortLocalUnits(PipeNodeKind kind, int port) =>
        PipeSnapPorts.FittingMouthLocalUnits(kind, port);

    private static Vector3 PortAxisUnits(PipeNodeKind kind, int port) =>
        PipeSnapPorts.FittingMouthAxisLocal(kind, port);

    private static Vector3 PipeCentreUnits => new Vector3(0f, PipeLengthMm * 0.5f * ToU, 0f);

    private static ElementGeometry StandingPipe() => new PosedPipe(PipeLengthMm).At(PipeCentreUnits);

    private static SnapResult BringFittingUnderThePipe(PipeNodeKind kind, float startXMm,
        Quaternion rotation, float approachGapMm = 10f)
    {
        var box = FittingBoxUnits(kind);
        var reach = Mathf.Abs((rotation * box).y) * 0.5f;
        var start = new Vector3(startXMm * ToU, -reach - approachGapMm * ToU, 0f);
        return SnapCore.TrySnap(new PosedFitting(kind, box, rotation),
            new List<ElementGeometry> { StandingPipe() }, start, Threshold);
    }

    private static PointMm PortMm(Vector3 snappedPos, PipeNodeKind kind, int port,
        Quaternion rotation)
    {
        var world = snappedPos + rotation * PortLocalUnits(kind, port);
        return new PointMm(world.x / ToU, world.y / ToU, world.z / ToU);
    }

    private static PipeAxis PortAxisAfter(PipeNodeKind kind, int port, Quaternion rotation)
    {
        var axis = rotation * PortAxisUnits(kind, port);
        return new PipeAxis(axis.x, axis.y, axis.z);
    }

    private static readonly PointMm PipeLowEndMm = new PointMm(0f, 0f, 0f);

    private static PipePort PipeLowEndPort() => new PipePort("Run", PipeNodeKind.Pipe, 0,
        PipeLowEndMm, PipeAxis.Down, PipeSpec.DEFAULT_SIZE);

    private static float NearestPortGapMm(SnapResult snap, PipeNodeKind kind, Quaternion rotation,
        out int nearestPort)
    {
        nearestPort = -1;
        float best = float.MaxValue;
        for (int i = 0; i < PipeFittingSpec.PortCount(kind); i++)
        {
            float d = PortMm(snap.position, kind, i, rotation).DistanceMmTo(PipeLowEndMm);
            if (d >= best) continue;
            best = d;
            nearestPort = i;
        }
        return best;
    }

    private static bool AnyPortConnects(SnapResult snap, PipeNodeKind kind, Quaternion rotation)
    {
        for (int i = 0; i < PipeFittingSpec.PortCount(kind); i++)
        {
            var port = new PipePort("Fit", kind, i, PortMm(snap.position, kind, i, rotation),
                PortAxisAfter(kind, i, rotation));
            if (PipeJoint.Connects(PipeLowEndPort(), port)) return true;
        }
        return false;
    }

    [Test]
    public void EveryFitting_BroughtUnderAPipeEnd_PutsOneOfItsMouthsOnThatEnd()
    {
        var missed = new StringBuilder();
        foreach (var kind in PipeNodePorts.Kinds)
        {
            if (!PipeFittingSpec.IsFitting(kind)) continue;
            var snap = BringFittingUnderThePipe(kind, 0f, Quaternion.identity);
            float gap = NearestPortGapMm(snap, kind, Quaternion.identity, out int port);
            if (!snap.snapped || gap > PipeJoint.JoinToleranceMm)
                missed.AppendLine($"{kind}: snapped={snap.snapped}, устье {port} в {gap:F2} мм");
        }

        Assert.AreEqual(string.Empty, missed.ToString(),
            "поднесённый под торец трубы фитинг обязан сесть УСТЬЕМ НА ТОРЕЦ — любой из "
            + "шести, а не только те четыре, у которых устье случайно попадало в центр "
            + "грани коробки. Раньше здесь проверялось совсем другое: что верх коробки "
            + "фитинга встал заподлицо с низом коробки трубы. Это было правдой всегда и "
            + "про стык не говорило ничего");
    }

    [Test]
    public void CouplingCapSupplyReturn_SnappedHeadOnToAPipeEnd_PortsMeetWithinContactTolerance()
    {
        var report = new StringBuilder();
        foreach (var kind in PortCentredOnTheBoxFace)
        {
            var snap = BringFittingUnderThePipe(kind, 0f, Quaternion.identity);
            float gap = NearestPortGapMm(snap, kind, Quaternion.identity, out int port);
            bool joined = AnyPortConnects(snap, kind, Quaternion.identity);
            if (!joined || gap > PipeJoint.JoinToleranceMm)
                report.AppendLine($"{kind}: ближайший порт {port} в {gap:F2} мм, стык={joined}");
        }

        Assert.AreEqual(string.Empty, report.ToString(),
            "эти четыре стыковались и раньше — но ПОПУТНО, потому что их устье лежит в "
            + "центре грани коробки, и расхождение 0,00–0,20 мм было остатком округления "
            + "DerivedDimensionsMM до целых мм. Теперь они стыкуются по правилу, и тест "
            + "остаётся как отрицательный контроль: если посадка по устьям сломается так, "
            + "что вернётся выравнивание коробок, эти четыре снова станут зелёными по "
            + "случайности — красным окажется соседний тест про уголок");
    }

    [Test]
    public void Elbow_AtEveryAxialRotation_MeetsThePipeEndByOneOfItsMouths()
    {
        var report = new StringBuilder();
        var connected = new List<float>();
        foreach (float degrees in new[] { 0f, 90f, 180f, 270f })
        {
            var rotation = ManagedRotation.RotZ(degrees);
            var snap = BringFittingUnderThePipe(PipeNodeKind.Elbow, 0f, rotation);
            float gap = NearestPortGapMm(snap, PipeNodeKind.Elbow, rotation, out int port);
            if (gap > PipeJoint.JoinToleranceMm)
                report.AppendLine($"rotZ={degrees}: ближайшее устье {port} в {gap:F2} мм");
            if (AnyPortConnects(snap, PipeNodeKind.Elbow, rotation)) connected.Add(degrees);
        }

        Assert.AreEqual(string.Empty, report.ToString(),
            "уголок не стыковался НИ ПРИ ОДНОМ из четырёх осевых поворотов, и дело было не "
            + "в повороте: поворот двигает устье ВМЕСТЕ с коробкой, а устье смещено вдоль "
            + "своей грани на 11,7 мм при допуске 0,5 мм. Пока посадка шла по граням "
            + "коробки, никакой поворот этого исправить не мог. Теперь устье садится на "
            + "торец при любом повороте");
        Assert.AreEqual(new List<float> { 90f, 180f }, connected,
            "а СТЫК — не то же самое, что посадка, и тест обязан это разделять: у уголка "
            + "плечи смотрят вниз и вбок, поэтому навстречу торцу трубы (который смотрит "
            + "вниз) устье оказывается только при повороте 90° и 180°. В остальных двух "
            + "положениях деталь встаёт устьем на торец, но PipeJoint такую пару "
            + "соединённой не считает — и правильно делает: труба входила бы в уголок "
            + "поперёк. Это и есть «поднёс — повернулось — соединилось»: посадка держит "
            + "устье на месте, поворот доводит ось");
    }

    [Test]
    public void Tee_MeetsThePipeEnd_ByItsBranchPortAndByItsStraightPortsAlike()
    {
        var branchUp = ManagedRotation.RotZ(90f);
        var branchSnap = BringFittingUnderThePipe(PipeNodeKind.Tee, 0f, branchUp);

        Assert.IsTrue(AnyPortConnects(branchSnap, PipeNodeKind.Tee, branchUp),
            "боковой отвод тройника лежит в центре боковой грани коробки, поэтому "
            + "тройник отводом вверх стыковался и раньше — единственный тогда рабочий "
            + "способ посадить тройник, и найти его можно было только перебором поворотов");

        var straightUp = Quaternion.identity;
        var straightSnap = BringFittingUnderThePipe(PipeNodeKind.Tee, 0f, straightUp);
        float gap = NearestPortGapMm(straightSnap, PipeNodeKind.Tee, straightUp, out _);

        Assert.IsTrue(AnyPortConnects(straightSnap, PipeNodeKind.Tee, straightUp),
            "у тройника, в отличие от уголка, есть прямой порт вверх, поэтому «как труба» "
            + "он теперь стыкуется без перебора поворотов");
        Assert.LessOrEqual(gap, PipeJoint.JoinToleranceMm,
            "а два прямых порта того же тройника были смещены поперёк оси на те же 11,7 мм, "
            + "что и у уголка: развёрнутый «как труба» тройник прилипал и не соединялся. "
            + "Теперь перебирать повороты не нужно — садится любой порт, который смотрит "
            + "навстречу");
    }

    [Test]
    public void Elbow_PortOffsetAlongItsBoxFace_IsHalfTheLegMinusTheBodyRadius()
    {
        float leg = PipeFittingSpec.LegLengthMm(PipeSpec.DEFAULT_SIZE);
        float radius = PipeFittingSpec.BodyDiameterMm(PipeSpec.DEFAULT_SIZE) * 0.5f;
        float expected = (leg - radius) * 0.5f;

        var port = PipeFittingSpec.PortOffsetMm(PipeNodeKind.Elbow, PipeSpec.DEFAULT_SIZE, 0);

        Assert.AreEqual(expected, Mathf.Abs(port.XMm), 0.01f,
            "смещение устья вдоль грани — не ошибка округления, а следствие формы коробки: "
            + "ступица стоит посередине между дальним концом плеча и дальним боком "
            + "перпендикулярного плеча, то есть уезжает от оси плеча ровно на половину "
            + "их разницы");
        Assert.Greater(Mathf.Abs(port.XMm), PipeJoint.JoinToleranceMm,
            "и оно НЕ исчезло: коробка по-прежнему несимметрична относительно оси плеча. "
            + "Это и есть доказательство, что уголок теперь стыкуется не потому, что "
            + "геометрия стала удобной, а потому, что посадка перестала зависеть от коробки");
    }

    [Test]
    public void Coupling_BroughtInOffTheAxis_IsPulledOntoTheAxis_NotOntoTheBoxEdge()
    {
        var snap = BringFittingUnderThePipe(PipeNodeKind.Coupling, 8f, Quaternion.identity);
        float landedXMm = snap.position.x / ToU;
        float halfOversizeMm =
            (FittingBoxUnits(PipeNodeKind.Coupling).x - PipeBoxUnits.x) * 0.5f / ToU;

        Assert.AreEqual(0f, landedXMm, 0.01f,
            $"поднесённая мимо оси на 8 мм муфта обязана встать НА ОСЬ трубы. Раньше она "
            + $"вставала на выравнивание КРОМОК: EdgeDetents.NearestDetentDelta знает три "
            + $"засечки — кромка-, кромка+, центр — и при 8 мм ближайшей оказывалась "
            + $"кромка, так что ось муфты уезжала на полразницы ширин коробок "
            + $"({halfOversizeMm:F2} мм)");
        Assert.IsTrue(AnyPortConnects(snap, PipeNodeKind.Coupling, Quaternion.identity),
            "и порты при этом сходятся: деталь не просто выглядит прикреплённой — PIP-01 "
            + "перестаёт считать этот конец трубы открытым");
    }

    [Test]
    public void Coupling_AxisCaptureBand_IsTheWholeSnapThreshold_NotHalfTheBoxOversize()
    {
        float halfOversizeMm =
            (FittingBoxUnits(PipeNodeKind.Coupling).x - PipeBoxUnits.x) * 0.5f / ToU;
        float mouthDropMm =
            -PortMm(Vector3.zero, PipeNodeKind.Coupling, 1, Quaternion.identity).YMm
            + FittingBoxUnits(PipeNodeKind.Coupling).y * 0.5f / ToU + 10f;
        float expectedBandMm = Mathf.Sqrt(
            KitchenSettings.SNAP_THRESHOLD_DEFAULT_MM * KitchenSettings.SNAP_THRESHOLD_DEFAULT_MM
            - mouthDropMm * mouthDropMm);

        float lastJoinedMm = -1f;
        float firstMissedMm = -1f;
        for (int step = 0; step <= 600; step++)
        {
            float offsetMm = step * 0.1f;
            var snap = BringFittingUnderThePipe(PipeNodeKind.Coupling, offsetMm, Quaternion.identity);
            if (AnyPortConnects(snap, PipeNodeKind.Coupling, Quaternion.identity))
                lastJoinedMm = offsetMm;
            else if (firstMissedMm < 0f) firstMissedMm = offsetMm;
        }

        Assert.Greater(firstMissedMm, halfOversizeMm * 0.5f + 1f,
            $"полоса захвата на ось кончалась там, где засечка «центр» проигрывала засечке "
            + $"«кромка», то есть на половине полуразницы ширин — ±{halfOversizeMm * 0.5f:F2} мм "
            + "для муфты DN20, и за краем был ОБРЫВ на всю полуразницу, а не плавная "
            + "деградация. Полоса не должна больше зависеть от ширины коробки");
        Assert.AreEqual(expectedBandMm, firstMissedMm, 0.2f,
            $"теперь полоса — это сам порог снэпа ({KitchenSettings.SNAP_THRESHOLD_DEFAULT_MM} мм) "
            + "по прямой от устья до устья: за вычетом просвета по вертикали остаётся "
            + $"±{expectedBandMm:F1} мм поперёк. Число выведено из порога, а не подобрано");
        Assert.AreEqual(lastJoinedMm + 0.1f, firstMissedMm, 0.001f,
            "и внутри полосы стык непрерывен: первый промах идёт сразу за последним "
            + "попаданием, дырок в середине нет");
    }

    [Test]
    public void Pipe_BroughtToAStandingCoupling_SnapsAndItsEndPortMeetsTheCouplingMouth()
    {
        var box = FittingBoxUnits(PipeNodeKind.Coupling);
        var couplingCentre = new Vector3(0f, -(box.y * 0.5f), 0f);
        var coupling = ElementGeometry.Box("Fit", couplingCentre, box, Quaternion.identity,
            false, default, 0f,
            PipeSnapPorts.OfFitting(PipeNodeKind.Coupling, Quaternion.identity, couplingCentre));

        var pipeStart = new Vector3(0f, (PipeLengthMm * 0.5f + 10f) * ToU, 0f);
        var snap = SnapCore.TrySnap(new PosedPipe(PipeLengthMm),
            new List<ElementGeometry> { coupling }, pipeStart, Threshold);

        Assert.IsTrue(snap.snapped, "труба к стоящей муфте тоже прилипает — снэп симметричен");

        var lowEnd = snap.position - new Vector3(0f, PipeLengthMm * 0.5f * ToU, 0f);
        var pipePort = new PipePort("Run", PipeNodeKind.Pipe, 0,
            new PointMm(lowEnd.x / ToU, lowEnd.y / ToU, lowEnd.z / ToU),
            PipeAxis.Down, PipeSpec.DEFAULT_SIZE);
        var mouth = couplingCentre + PortLocalUnits(PipeNodeKind.Coupling, 1);
        var couplingPort = new PipePort("Fit", PipeNodeKind.Coupling, 1,
            new PointMm(mouth.x / ToU, mouth.y / ToU, mouth.z / ToU), PipeAxis.Up);

        Assert.IsTrue(PipeJoint.Connects(pipePort, couplingPort),
            "обратное направление даёт тот же результат и по той же причине: устье трубы "
            + "садится на устье муфты. Раньше это работало из-за формы коробки муфты и "
            + "разваливалось на уголке");
    }

    [Test]
    public void PipeNetwork_WithAnElbowSnappedToAPipeEnd_ReportsThatEndClosed()
    {
        var rotation = ManagedRotation.RotZ(180f);
        var snap = BringFittingUnderThePipe(PipeNodeKind.Elbow, 0f, rotation);

        var scene = new PipeTestScene()
            .Pipe("Run", PipeTestScene.At(0f, 0f, 0f), PipeTestScene.At(0f, PipeLengthMm, 0f),
                PipeSpec.DEFAULT_SIZE)
            .Fitting("Elbow", PipeNodeKind.Elbow,
                (PortMm(snap.position, PipeNodeKind.Elbow, 0, rotation),
                    PortAxisAfter(PipeNodeKind.Elbow, 0, rotation)),
                (PortMm(snap.position, PipeNodeKind.Elbow, 1, rotation),
                    PortAxisAfter(PipeNodeKind.Elbow, 1, rotation)));

        int openEnds = 0;
        foreach (var f in PipeRules.Collect(scene))
            if (f.Code == PipeIssueCatalog.CodeOpenEnd) openEnds++;

        Assert.AreEqual(2, openEnds,
            "вот цена вопроса. Раньше ядро видело ЧЕТЫРЕ свободных конца — оба конца трубы "
            + "и оба порта уголка — при том, что уголок стоял прижатым вплотную и выглядел "
            + "собранным. Теперь свободны только два: дальний конец трубы и второй порт "
            + "уголка, а стык под ним PIP-01 больше не считает открытым");
    }
}
