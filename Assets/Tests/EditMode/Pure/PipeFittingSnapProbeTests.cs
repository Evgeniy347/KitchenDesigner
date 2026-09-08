using System.Collections.Generic;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;
using KitchenDesigner.Core.Plumbing;

/// <summary>РАЗВЕДКА: что происходит СЕГОДНЯ, когда фитинг подносят к торцу трубы.
///
/// Вопрос пользователя — «я не понимаю, как крепить муфту/уголок/тройник/заглушку».
/// Здесь он разложен на два РАЗНЫХ вопроса, которые до сих пор никто не разделял:
///
/// 1. ПРИЛИПАЕТ ли деталь визуально. Это работа снэпа, и снэп ничего не знает про
///    трубы: он ровняет ГАБАРИТНЫЕ КОРОБКИ (<c>ElementGeometry.Box</c>), потому что
///    у трубы и у фитинга нет собственных граней — <c>KitchenElement.GetFacesAt</c>
///    отдаёт коробку по <c>DimensionsMM</c>, а ни <c>PipeElement</c>, ни
///    <c>PipeFittingElement</c> её не переопределяют.
/// 2. СОЕДИНЯЮТСЯ ли порты логически. Это работа <c>PipeJoint</c>: два порта
///    считаются стыком, если их устья ближе <c>Tolerance.ContactMm</c> = 0,5 мм и
///    оси противоположны. Пока порты не сошлись, <c>PIP-01</c> продолжает считать
///    конец открытым, как бы красиво деталь ни встала.
///
/// Между этими двумя вещами нет ни одной строки кода. Прилипает ВСЁ; стыкуется —
/// не всё, и разница держится на одной величине: ГДЕ УСТЬЕ ПОРТА ЛЕЖИТ ОТНОСИТЕЛЬНО
/// ГРАНИ КОРОБКИ.
///
/// - Муфта, заглушка, подача, обратка: устье лежит на грани и в её ЦЕНТРЕ, поэтому
///   заподлицо-снэп попутно сводит и порты (расхождение 0,00–0,20 мм — округление
///   <c>DerivedDimensionsMM</c> до целых мм).
/// - Тройник: на грани и в центре лежит только БОКОВОЙ отвод. Развернув тройник
///   отводом вверх, пользователь получит настоящий стык; двумя прямыми портами —
///   не получит никогда.
/// - Уголок: НИ ОДИН из двух портов не центрован. Устье уходит вдоль грани на
///   (длина плеча − радиус тела)/2 = 11,72 мм для DN20, потому что коробка
///   <c>PipeFittingLayout.CoverSizeMM</c> симметрична относительно ступицы, а плечи
///   уголка — нет. 11,72 мм против допуска 0,5 мм.
///
/// И вторая беда, общая уже для ВСЕХ типов: снэп ровняет КРОМКИ коробок
/// (<c>EdgeDetents.NearestDetentDelta</c>), а коробка фитинга шире трубы. Стоит
/// поднести деталь мимо оси больше чем на половину полуразницы ширин — и засечка
/// «кромка» побеждает засечку «центр»: деталь встаёт заподлицо, но её ось уходит от
/// оси трубы на всю полуразницу. Полоса захвата на ось — ±1,75 мм для муфты DN20, и
/// за её краем обрыв, а не плавная деградация.
///
/// Тесты ниже фиксируют ИЗМЕРЕННОЕ поведение, а не желаемое: они зелёные и станут
/// красными, если поведение изменится в любую сторону. Требование, которого сегодня
/// нет, вынесено в <c>PipeFittingSnapTargetTests</c> и помечено там <c>[Ignore]</c>,
/// чтобы следующий агент его ВКЛЮЧИЛ, а не написал заново.
///
/// Числа берутся ИЗ КОДА, а не из головы: коробка — <c>PipeFittingLayout</c>, устье —
/// <c>PipeFittingSpec.PortOffsetMm</c>, порог — <c>KitchenSettings</c>. Тест,
/// повторяющий формулу за продакшеном, не проверяет ничего.</summary>
public class PipeFittingSnapProbeTests
{
    private const float ToU = AppConstants.MM_TO_UNITS;

    private const int PipeLengthMm = 600;

    private static readonly PipeNodeKind[] PortCentredOnTheBoxFace =
    {
        PipeNodeKind.Coupling, PipeNodeKind.Cap, PipeNodeKind.Supply, PipeNodeKind.Return,
    };

    private sealed class PosedBox : IPosedGeometry
    {
        private readonly string _name;
        private readonly Vector3 _sizeUnits;
        private readonly Quaternion _rotation;

        public PosedBox(string name, Vector3 sizeUnits, Quaternion rotation)
        {
            _name = name;
            _sizeUnits = sizeUnits;
            _rotation = rotation;
        }

        public ElementGeometry At(Vector3 position) =>
            ElementGeometry.Box(_name, position, _sizeUnits, _rotation);
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

    private static Vector3 PortLocalUnits(PipeNodeKind kind, int port)
    {
        var offset = PipeFittingSpec.PortOffsetMm(kind, PipeSpec.DEFAULT_SIZE, port);
        return new Vector3(offset.XMm * ToU, offset.YMm * ToU, offset.ZMm * ToU);
    }

    private static Vector3 PortAxisUnits(PipeNodeKind kind, int port)
    {
        var axis = PipeFittingSpec.PortAxis(kind, port);
        return new Vector3(axis.X, axis.Y, axis.Z);
    }

    private static ElementGeometry StandingPipe() =>
        ElementGeometry.Box("Run", new Vector3(0f, PipeLengthMm * 0.5f * ToU, 0f), PipeBoxUnits);

    private static SnapResult BringFittingUnderThePipe(PipeNodeKind kind, float startXMm,
        Quaternion rotation, float approachGapMm = 10f)
    {
        var box = FittingBoxUnits(kind);
        var reach = Mathf.Abs((rotation * box).y) * 0.5f;
        var start = new Vector3(startXMm * ToU, -reach - approachGapMm * ToU, 0f);
        return SnapCore.TrySnap(new PosedBox("Fit", box, rotation),
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
    public void EveryFitting_BroughtUnderAPipeEnd_SnapsFlushToTheBoundingBox()
    {
        var missed = new StringBuilder();
        foreach (var kind in PipeNodePorts.Kinds)
        {
            if (!PipeFittingSpec.IsFitting(kind)) continue;
            var snap = BringFittingUnderThePipe(kind, 0f, Quaternion.identity);
            float boxTopMm = (snap.position.y + FittingBoxUnits(kind).y * 0.5f) / ToU;
            if (!snap.snapped || Mathf.Abs(boxTopMm) > 0.001f)
                missed.AppendLine($"{kind}: snapped={snap.snapped}, верх коробки на {boxTopMm:F3} мм");
        }

        Assert.AreEqual(string.Empty, missed.ToString(),
            "визуально прилипают ВСЕ фитинги: снэп сводит верхнюю грань коробки фитинга "
            + "с нижней гранью коробки трубы. Именно это и создаёт впечатление, что деталь "
            + "«прикрепилась», — оно ничего не говорит о портах");
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
            "у этих четырёх устье порта лежит в центре грани коробки, поэтому "
            + "заподлицо-снэп ПОПУТНО сводит и порты: расхождение 0,00–0,20 мм — это "
            + "округление DerivedDimensionsMM до целых миллиметров, и оно укладывается "
            + "в 0,5 мм. Совпадение, а не механизм: поменяются множители в PipeFittingSpec "
            + "или правило округления — и стык пропадёт молча");
    }

    [Test]
    public void Elbow_PortsSitOffTheBoxAxis_SoNoAxialRotationMeetsAPipeEnd()
    {
        var report = new StringBuilder();
        foreach (float degrees in new[] { 0f, 90f, 180f, 270f })
        {
            var rotation = ManagedRotation.RotZ(degrees);
            var snap = BringFittingUnderThePipe(PipeNodeKind.Elbow, 0f, rotation);
            float gap = NearestPortGapMm(snap, PipeNodeKind.Elbow, rotation, out int port);
            if (AnyPortConnects(snap, PipeNodeKind.Elbow, rotation)
                || gap <= PipeJoint.JoinToleranceMm)
                report.AppendLine($"rotZ={degrees}: порт {port} сошёлся, {gap:F2} мм");
        }

        Assert.AreEqual(string.Empty, report.ToString(),
            "уголок не стыкуется ни при одном из четырёх осевых поворотов, и дело не в "
            + "повороте: поворот двигает устье ВМЕСТЕ с коробкой, а устье смещено вдоль "
            + "своей грани. Ближайший порт оказывается в 11,7 мм при допуске 0,5 мм");
    }

    [Test]
    public void Tee_OnlyItsBranchPortIsCentredOnItsBoxFace_TheStraightPortsAreNot()
    {
        var branchUp = ManagedRotation.RotZ(90f);
        var branchSnap = BringFittingUnderThePipe(PipeNodeKind.Tee, 0f, branchUp);

        Assert.IsTrue(AnyPortConnects(branchSnap, PipeNodeKind.Tee, branchUp),
            "боковой отвод тройника лежит в центре боковой грани коробки, поэтому "
            + "тройник, развёрнутый отводом вверх, ДЕЙСТВИТЕЛЬНО стыкуется с торцом трубы — "
            + "единственный сегодня рабочий способ посадить тройник, и найти его можно "
            + "только перебором поворотов");

        var straightUp = Quaternion.identity;
        var straightSnap = BringFittingUnderThePipe(PipeNodeKind.Tee, 0f, straightUp);
        float gap = NearestPortGapMm(straightSnap, PipeNodeKind.Tee, straightUp, out _);

        Assert.Greater(gap, PipeJoint.JoinToleranceMm,
            "а два прямых порта того же тройника смещены поперёк оси на те же 11,7 мм, "
            + "что и у уголка: развёрнутый «как труба» тройник прилипает и не соединяется");
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
            "если смещение когда-нибудь станет меньше допуска стыка, уголок заработает сам "
            + "собой — и тест обязан об этом сказать, а не остаться молча зелёным");
    }

    [Test]
    public void Coupling_BroughtInOffTheAxis_LandsOnTheEdgeDetent_AndThePortsMissEntirely()
    {
        var snap = BringFittingUnderThePipe(PipeNodeKind.Coupling, 8f, Quaternion.identity);
        float landedXMm = snap.position.x / ToU;
        float halfOversizeMm =
            (FittingBoxUnits(PipeNodeKind.Coupling).x - PipeBoxUnits.x) * 0.5f / ToU;

        Assert.AreEqual(halfOversizeMm, landedXMm, 0.01f,
            "поднесённая мимо оси муфта встаёт НЕ на ось трубы, а на выравнивание КРОМОК: "
            + "EdgeDetents.NearestDetentDelta знает три засечки — кромка-, кромка+, центр — "
            + "и при смещении 8 мм ближайшей оказывается кромка. Ось муфты уезжает "
            + "на полразницы ширин коробок");
        Assert.IsFalse(AnyPortConnects(snap, PipeNodeKind.Coupling, Quaternion.identity),
            "деталь стоит заподлицо и выглядит прикреплённой, но порты разошлись — "
            + "PIP-01 продолжит считать конец трубы открытым");
    }

    [Test]
    public void Coupling_AxisCaptureBand_EndsAtHalfTheEdgeDetentDistance()
    {
        float halfOversizeMm =
            (FittingBoxUnits(PipeNodeKind.Coupling).x - PipeBoxUnits.x) * 0.5f / ToU;

        float lastJoinedMm = -1f;
        float firstMissedMm = -1f;
        for (int step = 0; step <= 60; step++)
        {
            float offsetMm = step * 0.1f;
            var snap = BringFittingUnderThePipe(PipeNodeKind.Coupling, offsetMm, Quaternion.identity);
            if (AnyPortConnects(snap, PipeNodeKind.Coupling, Quaternion.identity))
                lastJoinedMm = offsetMm;
            else if (firstMissedMm < 0f) firstMissedMm = offsetMm;
        }

        Assert.AreEqual(halfOversizeMm * 0.5f, firstMissedMm, 0.15f,
            "полоса захвата на ось кончается там, где засечка «центр» проигрывает засечке "
            + "«кромка», то есть на половине полуразницы ширин — ±1,75 мм для муфты DN20");
        Assert.Less(lastJoinedMm, firstMissedMm,
            "и это ОБРЫВ, а не плавная деградация: до края полосы порты сходятся ТОЧНО "
            + "(деталь выводится на ось), за краем — сразу на всю полуразницу");
    }

    [Test]
    public void Pipe_BroughtToAStandingCoupling_SnapsAndItsEndPortMeetsTheCouplingMouth()
    {
        var box = FittingBoxUnits(PipeNodeKind.Coupling);
        var couplingCentre = new Vector3(0f, -(box.y * 0.5f), 0f);
        var coupling = ElementGeometry.Box("Fit", couplingCentre, box);

        var pipeStart = new Vector3(0f, (PipeLengthMm * 0.5f + 10f) * ToU, 0f);
        var snap = SnapCore.TrySnap(new PosedBox("Run", PipeBoxUnits, Quaternion.identity),
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
            "обратное направление даёт тот же результат и по той же причине: снэп ровняет "
            + "коробки, а устье муфты лежит в центре грани её коробки");
    }

    [Test]
    public void PipeNetwork_WithAnElbowSnappedToAPipeEnd_StillReportsEveryEndOpen()
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

        Assert.AreEqual(4, openEnds,
            "вот цена расхождения: уголок прижат к трубе вплотную и выглядит собранным, "
            + "а ядро видит ЧЕТЫРЕ свободных конца — оба конца трубы и оба порта уголка. "
            + "Пока устья не сошлись в пределах 0,5 мм, визуальный контакт PIP-01 не гасит");
    }
}
