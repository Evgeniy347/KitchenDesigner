using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;
using KitchenDesigner.Core.Plumbing;
using KitchenDesigner.Tests.Geometry;

/// <summary>Замер стыковки ФИТИНГ ↔ ФИТИНГ: отдельно «прилипло по коробкам» и
/// отдельно «сошлись порты».
///
/// Пользователь сказал дословно: «не могу уголок подсоединить к подаче — не
/// стыкуется, не прилипает». Здесь это воспроизводится числами, а не на глаз.
///
/// Две вещи, которые легко спутать и которые здесь РАЗДЕЛЕНЫ:
///   * визуально прижаться — `SnapCore.TrySnap` двигает КОРОБКУ детали к грани
///     соседа, и человек видит, что деталь «прилипла»;
///   * логически соединиться — `PipeJoint.Connects` требует, чтобы устья портов
///     сошлись в пределах `Tolerance.ContactMm` (0,5 мм) и смотрели навстречу.
/// Пока второе не выполнено, `PIP-01` считает конец трассы открытым на собранной
/// НА ВИД трассе, и никакой красноты в сцене при этом не видно.
///
/// Геометрия, из-за которой это вообще расходится: у фитинга устье порта лежит
/// на КОНЦЕ ножки, а коробка — это габарит всей детали. У симметричных деталей
/// (муфта, заглушка, подача, обратка) устье попадает в ЦЕНТР грани коробки, а у
/// уголка и тройника — нет: ножек две-три и разной направленности, поэтому центр
/// габарита смещён относительно оси каждой ножки. Снэп же знает только грани и
/// «детенты» кромок и центра — про ось трубы он не знает ничего.
///
/// Замер идёт от ИДЕАЛЬНОЙ позиции: деталь B ставится ровно так, чтобы её порт
/// совпал с портом детали A (это и есть «соединено»), и дальше спрашивается, что
/// с этой позицией сделает снэп. Ответ «сдвинул» — это и есть отказ: снэп уводит
/// деталь С правильного места.
///
/// ЧТО ИЗМЕНИЛОСЬ. Замер 2026-09-08 давал: коробки прилипают в 100 парах из 100,
/// а портами сходится 31, и эта 31 — случайность округления габарита. Теперь устье
/// порта — полноправная геометрия элемента (`ElementGeometry.Ports`, источник —
/// `ISnapPorts` у самого элемента), и отбор кандидатов сажает деталь устьем в устье
/// ДО того, как посмотрит на грани (`SnapPortSeat` в `SnapCore.TrySnap`). Числа
/// приёмки: разомкнутых 0 из 100, соединённых 100, худший зазор устьев — доли
/// микрона вместо 0,46 мм. Форма коробки в стык больше не входит вообще.
///
/// `AGENTS.md` → «Prove the harness before you trust what it measures»:
/// `Harness_...` ниже доказывает, что идеальная позиция действительно даёт
/// соединение, иначе все остальные числа были бы про ошибку стенда.</summary>
public class PipeFittingPairSnapTests
{
    private const float ToU = AppConstants.MM_TO_UNITS;

    private const float ThresholdUnits = KitchenSettings.SNAP_THRESHOLD_DEFAULT_MM * ToU;

    private static readonly PipeNodeKind[] Fittings =
    {
        PipeNodeKind.Elbow,
        PipeNodeKind.Coupling,
        PipeNodeKind.Tee,
        PipeNodeKind.Cap,
        PipeNodeKind.Supply,
        PipeNodeKind.Return,
    };

    private static readonly (string Name, Quaternion Q)[] Orientations =
    {
        ("0", Quaternion.identity),
        ("Z90", ManagedRotation.RotZ(90f)),
        ("Z-90", ManagedRotation.RotZ(-90f)),
        ("Z180", ManagedRotation.RotZ(180f)),
        ("X90", ManagedRotation.RotX(90f)),
        ("X-90", ManagedRotation.RotX(-90f)),
        ("X180", ManagedRotation.RotX(180f)),
        ("Y90", ManagedRotation.RotY(90f)),
        ("Y-90", ManagedRotation.RotY(-90f)),
        ("Y180", ManagedRotation.RotY(180f)),
    };

    private sealed class PosedFitting : IPosedGeometry
    {
        private readonly string _name;
        private readonly PipeNodeKind _kind;
        private readonly Quaternion _rotation;

        public PosedFitting(string name, PipeNodeKind kind, Quaternion rotation)
        {
            _name = name;
            _kind = kind;
            _rotation = rotation;
        }

        public ElementGeometry At(Vector3 position) => GeometryOf(_name, _kind, _rotation, position);
    }

    private readonly struct PairMeasurement
    {
        public readonly string Pair;
        public readonly string Orientation;
        public readonly float PortGapAtIdealMm;
        public readonly bool SnapMovedFromIdealMm;
        public readonly float MovedFromIdealMm;
        public readonly bool SnappedFromDrift;
        public readonly float PortGapAfterDriftMm;
        public readonly float BoxPlaneGapAfterDriftMm;
        public readonly bool JointAfterDrift;

        public PairMeasurement(string pair, string orientation, float portGapAtIdealMm,
            bool snapMovedFromIdeal, float movedFromIdealMm, bool snappedFromDrift,
            float portGapAfterDriftMm, float boxPlaneGapAfterDriftMm, bool jointAfterDrift)
        {
            Pair = pair;
            Orientation = orientation;
            PortGapAtIdealMm = portGapAtIdealMm;
            SnapMovedFromIdealMm = snapMovedFromIdeal;
            MovedFromIdealMm = movedFromIdealMm;
            SnappedFromDrift = snappedFromDrift;
            PortGapAfterDriftMm = portGapAfterDriftMm;
            BoxPlaneGapAfterDriftMm = boxPlaneGapAfterDriftMm;
            JointAfterDrift = jointAfterDrift;
        }
    }

    [Test]
    public void Harness_PortsPlacedMouthToMouth_PipeJointCallsThemConnected()
    {
        foreach (var kindA in Fittings)
        {
            for (int portA = 0; portA < PipeFittingSpec.PortCount(kindA); portA++)
            {
                foreach (var kindB in Fittings)
                {
                    for (int portB = 0; portB < PipeFittingSpec.PortCount(kindB); portB++)
                    {
                        if (!TryFaceTowards(kindA, portA, kindB, portB, out var rot, out _)) continue;

                        var mouthA = PortWorldUnits(kindA, Quaternion.identity, Vector3.zero, portA);
                        var ideal = mouthA - rot * PortLocalUnits(kindB, portB);
                        var mouthB = PortWorldUnits(kindB, rot, ideal, portB);

                        var a = PortOf("A", kindA, Quaternion.identity, Vector3.zero, portA);
                        var b = PortOf("B", kindB, rot, ideal, portB);

                        Assert.That(mouthA.MmDistanceTo(mouthB), Is.LessThanOrEqualTo(0.01f),
                            $"стенд обязан ставить деталь устье в устье: {kindA}.{portA} ↔ "
                            + $"{kindB}.{portB}; иначе все замеры ниже — про ошибку стенда, "
                            + "а не про снэп");
                        Assert.That(PipeJoint.Connects(a, b), Is.True,
                            $"устья совпали, оси встречные — ядро обязано считать пару "
                            + $"соединённой: {kindA}.{portA} ↔ {kindB}.{portB}");
                    }
                }
            }
        }
    }

    [Test]
    public void FittingPairs_SnapFromIdealMouthToMouth_MeasuredAndWrittenToFile()
    {
        var rows = Measure();

        var report = new StringBuilder();
        report.AppendLine("пара; ориентация B; зазор портов в идеале, мм; снэп сдвинул с идеала; "
            + "сдвиг, мм; прилипло из-под сноса; зазор портов после, мм; зазор коробок после, мм; "
            + "PipeJoint");
        foreach (var r in rows)
            report.AppendLine(string.Join("; ", new[]
            {
                r.Pair,
                r.Orientation,
                Mm(r.PortGapAtIdealMm),
                r.SnapMovedFromIdealMm ? "да" : "нет",
                Mm(r.MovedFromIdealMm),
                r.SnappedFromDrift ? "да" : "нет",
                Mm(r.PortGapAfterDriftMm),
                Mm(r.BoxPlaneGapAfterDriftMm),
                r.JointAfterDrift ? "соединено" : "РАЗОМКНУТО",
            }));

        int stuck = 0;
        int joined = 0;
        int pushedOff = 0;
        foreach (var r in rows)
        {
            if (r.SnappedFromDrift) stuck++;
            if (r.JointAfterDrift) joined++;
            if (r.SnapMovedFromIdealMm) pushedOff++;
        }

        report.AppendLine();
        report.AppendLine($"пар всего: {rows.Count}; прилипло по коробкам: {stuck}; "
            + $"порты сошлись (PipeJoint): {joined}; снэп увёл с идеальной позиции: {pushedOff}");

        var dir = RepoPaths.Subdir("test-results");
        File.WriteAllText(Path.Combine(dir, "pipe-fitting-pairs.txt"), report.ToString());

        Assert.That(rows.Count, Is.GreaterThan(0),
            "ни одной осмысленной пары фитингов не набралось — сломан подбор ориентации");
    }

    [Test]
    public void FittingPairs_EveryPair_SticksByItsBoxes()
    {
        var rows = Measure();

        var loose = new List<PairMeasurement>();
        foreach (var r in rows)
            if (!r.SnappedFromDrift) loose.Add(r);

        Assert.That(loose.Count, Is.EqualTo(0),
            "жалоба «не прилипает» буквально неверна: снесённая на 9/7/5 мм деталь "
            + "притягивается к соседу во ВСЕХ парах — коробки встают заподлицо. "
            + "Не прилипает не деталь, а ось: " + Summary(loose));
    }

    [Test]
    public void FittingPairs_EveryPair_ClosesItsJointAfterTheSnap()
    {
        var rows = Measure();

        var stuckButOpen = new List<PairMeasurement>();
        foreach (var r in rows)
            if (r.SnappedFromDrift && !r.JointAfterDrift) stuckButOpen.Add(r);

        Assert.That(rows.Count, Is.EqualTo(100),
            "шесть фитингов дают 100 встречных пар «порт ↔ порт»; изменилось число — "
            + "изменился состав фитингов или их портов, и таблицу замера надо перечитать");
        Assert.That(stuckButOpen.Count, Is.EqualTo(0),
            "было 69 разомкнутых из 100: снэп ровнял КОРОБКИ, и устья сходились только там, "
            + "где порт случайно попадал в центр грани. Теперь отбор кандидатов знает про "
            + "устья (SnapPortSeat) и сажает деталь точкой в точку, поэтому разомкнутых "
            + "не остаётся ни одной. Красный здесь означает, что посадка по устьям снова "
            + "проиграла граневому детенту: " + Summary(stuckButOpen));
    }

    [Test]
    public void FittingPairs_ThatJoin_KeepAlmostTheWholeContactTolerance()
    {
        var rows = Measure();

        float worstJoined = 0f;
        int joined = 0;
        foreach (var r in rows)
        {
            if (!r.JointAfterDrift) continue;
            joined++;
            if (r.PortGapAfterDriftMm > worstJoined) worstJoined = r.PortGapAfterDriftMm;
        }

        Assert.That(joined, Is.EqualTo(100),
            "сходятся все 100 пар, и сходятся ПО ПРАВИЛУ: посадка совмещает устья, а не "
            + "центры граней коробок");
        Assert.That(worstJoined, Is.LessThan(Tolerance.ContactMm * 0.02f),
            $"худшая из соединённых пар держится на {worstJoined:F4} мм при допуске "
            + $"{Tolerance.ContactMm} мм. Это и есть цена вопроса про округление: "
            + "PipeFittingSpec.RoundedMm округляет ГАБАРИТ, а посадка идёт по устью, "
            + "которое считается по PortOffsetMm без округления — округление вышло из "
            + "бюджета допуска целиком. Раньше здесь оставалось 0,04 мм запаса из 0,5, и "
            + "смена диаметра выбила бы пары из соединения молча");
    }

    [Test]
    public void ElbowOnSupply_BroughtInBesideTheSupplyMouth_IsPulledOntoIt()
    {
        Assert.That(TryFaceTowards(PipeNodeKind.Supply, 0, PipeNodeKind.Elbow, 0,
            out var rot, out _), Is.True);

        var mouth = PortWorldUnits(PipeNodeKind.Supply, Quaternion.identity, Vector3.zero, 0);
        var ideal = mouth - rot * PortLocalUnits(PipeNodeKind.Elbow, 0);
        var supply = GeometryOf("Подача", PipeNodeKind.Supply, Quaternion.identity, Vector3.zero);
        var elbow = new PosedFitting("Уголок", PipeNodeKind.Elbow, rot);
        var start = ideal + new Vector3(9f * ToU, 7f * ToU, 5f * ToU);

        var snap = SnapCore.TrySnap(elbow, new List<ElementGeometry> { supply }, start,
            ThresholdUnits);
        var portAfter = PortWorldUnits(PipeNodeKind.Elbow, rot, snap.position, 0);

        Assert.That(snap.snapped, Is.True,
            "жалоба пользователя буквально: «не могу уголок подсоединить к подаче». "
            + "Уголок поднесён мимо на 12,4 мм — снэп обязан сработать");
        Assert.That(mouth.MmDistanceTo(portAfter), Is.LessThanOrEqualTo(Tolerance.ContactMm),
            $"и обязан вывести устье уголка НА устье подачи: сейчас "
            + $"{mouth.MmDistanceTo(portAfter):F3} мм при допуске {Tolerance.ContactMm} мм. "
            + "Раньше побеждал детент кромки коробки, и устья расходились на 6,7 мм");
        Assert.That(Vector3.Distance(snap.position, ideal) / ToU,
            Is.LessThanOrEqualTo(Tolerance.ContactMm),
            "деталь встаёт ровно в ту позицию, которую даёт совмещение устьев, а не рядом "
            + "с ней");
    }

    [Test]
    public void ElbowOnSupply_PlacedMouthToMouth_IsLeftExactlyWhereItIs()
    {
        Assert.That(TryFaceTowards(PipeNodeKind.Supply, 0, PipeNodeKind.Elbow, 0,
            out var rot, out string orientation), Is.True,
            "порт подачи смотрит вверх, порт 0 уголка — вниз: пара обязана быть встречной "
            + "без поворота");
        Assert.That(orientation, Is.EqualTo("0"),
            "названный пользователем случай — уголок БЕЗ поворота поверх подачи");

        var mouth = PortWorldUnits(PipeNodeKind.Supply, Quaternion.identity, Vector3.zero, 0);
        var ideal = mouth - rot * PortLocalUnits(PipeNodeKind.Elbow, 0);
        var supply = GeometryOf("Подача", PipeNodeKind.Supply, Quaternion.identity, Vector3.zero);
        var elbow = new PosedFitting("Уголок", PipeNodeKind.Elbow, rot);

        var snap = SnapCore.TrySnap(elbow, new List<ElementGeometry> { supply }, ideal,
            ThresholdUnits);

        float movedMm = Vector3.Distance(snap.position, ideal) / ToU;
        var portAfter = PortWorldUnits(PipeNodeKind.Elbow, rot, snap.position, 0);

        Assert.That(snap.snapped, Is.True,
            "снэп срабатывает — но теперь потому, что нашёл устье, а не грань коробки");
        Assert.That(movedMm, Is.LessThanOrEqualTo(Tolerance.ContactMm),
            $"уголок поставлен УСТЬЕМ В УСТЬЕ подачи, и снэп обязан оставить его на месте: "
            + $"сейчас сдвиг {movedMm:F3} мм. Раньше детент кромки коробки утаскивал деталь "
            + "с правильной позиции на 6,73 мм — это и было «не стыкуется»");
        Assert.That(mouth.MmDistanceTo(portAfter), Is.LessThanOrEqualTo(Tolerance.ContactMm),
            $"устья остаются совмещёнными: {mouth.MmDistanceTo(portAfter):F3} мм при допуске "
            + $"{Tolerance.ContactMm} мм — PipeJoint считает такую пару соединённой");
    }

    [Test]
    public void ElbowPortZero_IsOffTheCentreOfItsOwnBoxFace_UnlikeSupply()
    {
        var elbowPort = PortLocalUnits(PipeNodeKind.Elbow, 0) / ToU;
        var supplyPort = PortLocalUnits(PipeNodeKind.Supply, 0) / ToU;

        Assert.That(Mathf.Abs(supplyPort.x), Is.LessThanOrEqualTo(Tolerance.ContactMm),
            "у подачи одна ножка и симметричный фланец, поэтому устье попадает ровно в центр "
            + "верхней грани коробки — по такой детали снэп по граням случайно совпадает с осью");
        Assert.That(Mathf.Abs(elbowPort.x), Is.GreaterThan(Tolerance.ContactMm),
            $"у уголка две ножки разной направленности, центр габарита смещён, и устье порта 0 "
            + $"отстоит от центра нижней грани на {Mathf.Abs(elbowPort.x):F2} мм. "
            + "Снэп по граням выравнивает КОРОБКИ, а не оси — отсюда и расхождение портов");
    }

    [Test]
    public void CentresOnTarget_IsGrantedToScrewLegOnly_SoFittingsNeverCentre()
    {
        var elbow = GeometryOf("Уголок", PipeNodeKind.Elbow, Quaternion.identity, Vector3.zero);

        Assert.That(elbow.CentresOnTarget, Is.False,
            "механизм центровки на цели (ElementGeometry.CentresOnTarget + SnapMountSeat) "
            + "фитингу не нужен и не выдаётся: ось крепления объявляет сам элемент через "
            + "IMountsOnTarget, и её объявляет только винтовая опора. У фитинга своя "
            + "геометрия посадки — устье порта");
        Assert.That(elbow.MountEdgeDetentUnits, Is.EqualTo(0f),
            "и отступа от кромки у фитинга тоже нет");
        Assert.That(elbow.HasPorts, Is.True,
            "зато у него есть устья — та самая геометрия, по которой его теперь сажают. "
            + "Положительный контроль к двум отрицаниям выше: если бы порты не доехали "
            + "до снимка, оба Is.False остались бы зелёными на полностью нерабочей посадке");
    }

    private static List<PairMeasurement> Measure()
    {
        var rows = new List<PairMeasurement>();
        var drift = new Vector3(9f * ToU, 7f * ToU, 5f * ToU);

        foreach (var kindA in Fittings)
        {
            for (int portA = 0; portA < PipeFittingSpec.PortCount(kindA); portA++)
            {
                foreach (var kindB in Fittings)
                {
                    for (int portB = 0; portB < PipeFittingSpec.PortCount(kindB); portB++)
                    {
                        if (!TryFaceTowards(kindA, portA, kindB, portB, out var rot,
                                out string orientation)) continue;

                        rows.Add(MeasureOne(kindA, portA, kindB, portB, rot, orientation, drift));
                    }
                }
            }
        }

        return rows;
    }

    private static PairMeasurement MeasureOne(PipeNodeKind kindA, int portA, PipeNodeKind kindB,
        int portB, Quaternion rot, string orientation, Vector3 drift)
    {
        var host = GeometryOf("A", kindA, Quaternion.identity, Vector3.zero);
        var moved = new PosedFitting("B", kindB, rot);

        var mouthA = PortWorldUnits(kindA, Quaternion.identity, Vector3.zero, portA);
        var ideal = mouthA - rot * PortLocalUnits(kindB, portB);
        var others = new List<ElementGeometry> { host };

        var fromIdeal = SnapCore.TrySnap(moved, others, ideal, ThresholdUnits);
        float movedFromIdealMm = fromIdeal.snapped
            ? Vector3.Distance(fromIdeal.position, ideal) / ToU
            : 0f;

        var start = ideal + drift;
        var fromDrift = SnapCore.TrySnap(moved, others, start, ThresholdUnits);
        var landed = fromDrift.snapped ? fromDrift.position : start;

        var mouthB = PortWorldUnits(kindB, rot, landed, portB);
        var jointA = PortOf("A", kindA, Quaternion.identity, Vector3.zero, portA);
        var jointB = PortOf("B", kindB, rot, landed, portB);

        return new PairMeasurement(
            $"{PipeFittingNames.Title(kindA)}.{portA} ↔ {PipeFittingNames.Title(kindB)}.{portB}",
            orientation,
            mouthA.MmDistanceTo(PortWorldUnits(kindB, rot, ideal, portB)),
            movedFromIdealMm > Tolerance.ContactMm,
            movedFromIdealMm,
            fromDrift.snapped,
            mouthA.MmDistanceTo(mouthB),
            BoxPlaneGapMm(host, GeometryOf("B", kindB, rot, landed)),
            PipeJoint.Connects(jointA, jointB));
    }

    private static bool TryFaceTowards(PipeNodeKind kindA, int portA, PipeNodeKind kindB,
        int portB, out Quaternion rotation, out string orientation)
    {
        var axisA = PortAxisLocal(kindA, portA);
        var wanted = -axisA;

        foreach (var candidate in Orientations)
        {
            var axisB = candidate.Q * PortAxisLocal(kindB, portB);
            if (Vector3.Dot(axisB, wanted) < Tolerance.ParallelDot) continue;
            rotation = candidate.Q;
            orientation = candidate.Name;
            return true;
        }

        rotation = Quaternion.identity;
        orientation = string.Empty;
        return false;
    }

    private static ElementGeometry GeometryOf(string name, PipeNodeKind kind, Quaternion rotation,
        Vector3 position)
    {
        var size = BoxUnits(kind);
        var gaps = default(BoxGaps);
        var faces = GappedBox.Faces(size, gaps, position, rotation);
        ElementGeometry.BoundsOf(GappedBox.Vertices(size, gaps, position, rotation),
            out var min, out var max);
        var empty = Array.Empty<Face>();
        return new ElementGeometry(name.GetHashCode(), name, faces, empty, empty, min, max, false,
            default, 0f, PipeSnapPorts.OfFitting(kind, rotation, position));
    }

    private static Vector3 BoxUnits(PipeNodeKind kind)
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

    private static Vector3 PortAxisLocal(PipeNodeKind kind, int port)
    {
        var axis = PipeFittingSpec.PortAxis(kind, port);
        return new Vector3(axis.X, axis.Y, axis.Z);
    }

    private static Vector3 PortWorldUnits(PipeNodeKind kind, Quaternion rotation, Vector3 position,
        int port) => position + rotation * PortLocalUnits(kind, port);

    private static PipePort PortOf(string id, PipeNodeKind kind, Quaternion rotation,
        Vector3 position, int port)
    {
        var mouth = PortWorldUnits(kind, rotation, position, port);
        var axis = rotation * PortAxisLocal(kind, port);
        return new PipePort(id, kind, port,
            new PointMm(mouth.x / ToU, mouth.y / ToU, mouth.z / ToU),
            new PipeAxis(axis.x, axis.y, axis.z));
    }

    private static float BoxPlaneGapMm(in ElementGeometry host, in ElementGeometry guest)
    {
        float best = float.MaxValue;
        foreach (var hf in host.Faces)
        {
            foreach (var gf in guest.Faces)
            {
                if (Vector3.Dot(hf.normal, gf.normal) > -Tolerance.ParallelDot) continue;
                float gap = Mathf.Abs(Vector3.Dot(gf.center - hf.center, hf.normal)) / ToU;
                if (gap < best) best = gap;
            }
        }

        return best == float.MaxValue ? -1f : best;
    }

    private static string Summary(List<PairMeasurement> rows)
    {
        var text = new StringBuilder();
        int shown = 0;
        foreach (var r in rows)
        {
            if (shown++ >= 6) break;
            text.Append($" | {r.Pair} [{r.Orientation}]: порты {r.PortGapAfterDriftMm:F1} мм, "
                + $"коробки {r.BoxPlaneGapAfterDriftMm:F2} мм");
        }

        return text.ToString() + $" | всего {rows.Count}";
    }

    private static string Mm(float value) => value.ToString("F2", CultureInfo.InvariantCulture);
}

internal static class PipeFittingPairSnapVectors
{
    public static float MmDistanceTo(this Vector3 a, Vector3 b) =>
        Vector3.Distance(a, b) / AppConstants.MM_TO_UNITS;
}
