using System.Collections.Generic;
using UnityEngine;

namespace KitchenDesigner.Core
{
    [System.Serializable]
    public class SnapNeighborReport
    {
        public string name = string.Empty;
        public float centerDistanceMM;
        public bool intersects;
        public bool hasFacingFaces;
        public float bestDot;
        public int movedFaceIndex = -1;
        public int otherFaceIndex = -1;
        public float gapMM = -1f;
        public float overlapRatio = -1f;
        public bool withinThreshold;
        public bool overlapEnough;
        public bool wouldSnap;
        public bool bothCarryPorts;
        public float portGapMM = -1f;
        public float portSeatShiftMM = -1f;
        public bool portsFaceEachOther;
        public int movedPortIndex = -1;
        public int otherPortIndex = -1;
        public bool portSeatOffered;
        public bool alignmentLandsInsideNeighbour;
        public bool dockOffered;
        public float dockRotationDegrees = -1f;
        public float dockCursorDistanceMM = -1f;
        public string verdict = string.Empty;
    }

    [System.Serializable]
    public class SnapDiagnosis
    {
        public bool snapEnabled;
        public float thresholdMM;
        public bool wouldSnap;
        public string? snapTarget;
        public bool portSeatWins;
        public string? portSeatTarget;
        public float portSeatShiftMM = -1f;
        public int portSeatMovedPortIndex = -1;
        public int portSeatOtherPortIndex = -1;
        public bool cursorKnown;
        public bool dockWins;
        public string? dockTarget;
        public int dockMovedPortIndex = -1;
        public int dockOtherPortIndex = -1;
        public float dockRotationDegrees = -1f;
        public float dockCursorDistanceMM = -1f;
        public List<SnapNeighborReport> neighbors = new List<SnapNeighborReport>();
    }

    public static class SnapSystem
    {
        private const float ThresholdEpsilon = Tolerance.SnapEpsilon;

        public static bool VerboseLog = false;

        internal sealed class PosedElement : IPosedGeometry
        {
            private readonly KitchenElement _element;
            public PosedElement(KitchenElement element) => _element = element;
            public ElementGeometry At(Vector3 position) => _element.ToGeometryAt(position);
        }

        public static SnapResult TrySnap(KitchenElement moved, List<KitchenElement> others, Vector3 testPosition)
        {
            if (moved == null || others == null) return default;
            if (!KitchenSettings.Instance.SnapEnabled) return default;
            if (!moved.gameObject.activeInHierarchy) return default;

            using var _ = PerfMarkers.SnapTrySnap.Auto();
            return TrySnap(moved, others.ToGeometryFor(moved), testPosition);
        }

        public static SnapResult TrySnap(KitchenElement moved, IReadOnlyList<ElementGeometry> others,
            Vector3 testPosition)
        {
            if (moved == null || others == null) return default;
            if (!KitchenSettings.Instance.SnapEnabled) return default;
            if (!moved.gameObject.activeInHierarchy) return default;

            float threshold = KitchenSettings.Instance.SnapThreshold * AppConstants.MM_TO_UNITS;

            return SnapCore.TrySnap(new PosedElement(moved), others, testPosition,
                threshold, VerboseLog ? Debug.Log : (System.Action<string>?)null);
        }

        public static SnapDiagnosis Diagnose(KitchenElement moved, List<KitchenElement> others,
            Vector3 testPosition, int maxNeighbors = 5)
            => Diagnose(moved, others, testPosition, null, maxNeighbors, SnapCursor.None);

        public static SnapDiagnosis Diagnose(KitchenElement moved, List<KitchenElement> others,
            Vector3 testPosition, SnapResult? knownSnap, int maxNeighbors = 5)
            => Diagnose(moved, others, testPosition, knownSnap, maxNeighbors, SnapCursor.None);

        public static SnapDiagnosis Diagnose(KitchenElement moved, List<KitchenElement> others,
            Vector3 testPosition, SnapResult? knownSnap, int maxNeighbors, SnapCursor cursor)
        {
            var settings = KitchenSettings.Instance;
            var report = new SnapDiagnosis
            {
                snapEnabled = settings != null && settings.SnapEnabled,
                thresholdMM = settings != null ? settings.SnapThreshold : 0f,
            };
            if (moved == null || others == null) return report;

            var neighbours = new List<KitchenElement>(others.Count);
            var scene = new List<ElementGeometry>(others.Count);
            foreach (var other in others)
            {
                if (other == null || !other.gameObject.activeInHierarchy) continue;
                neighbours.Add(other);
                scene.Add(PipeDocking.MaySeatOn(moved, other)
                    ? other.ToGeometry()
                    : other.ToGeometry().WithoutPorts());
            }

            var snap = knownSnap ?? TrySnap(moved, scene, testPosition);
            report.wouldSnap = snap.snapped;
            report.snapTarget = snap.snapped ? snap.targetName : null;

            float maxDist = report.thresholdMM * AppConstants.MM_TO_UNITS + ThresholdEpsilon;

            ElementGeometry movedGeo = moved.ToGeometryAt(testPosition);
            Vector3[] movedVerts = moved.GetVerticesAt(testPosition);

            var winner = SnapPortSeat.BestOf(movedGeo, scene, maxDist);
            report.portSeatWins = winner.taken;
            report.portSeatTarget = winner.taken ? winner.targetName : null;
            report.portSeatShiftMM = winner.taken
                ? winner.delta.magnitude / AppConstants.MM_TO_UNITS
                : -1f;
            report.portSeatMovedPortIndex = winner.movedPort;
            report.portSeatOtherPortIndex = winner.otherPort;

            var movedPorted = PortedPart.Of(movedGeo);
            var scenePorted = new List<PortedPart>(scene.Count);
            foreach (var geometry in scene) scenePorted.Add(PortedPart.Of(geometry));

            var dock = SnapPortDock.Best(movedPorted, scenePorted, maxDist, cursor);
            report.cursorKnown = cursor.Present;
            report.dockWins = dock.taken;
            report.dockTarget = dock.taken ? dock.targetName : null;
            report.dockMovedPortIndex = dock.movedPort;
            report.dockOtherPortIndex = dock.otherPort;
            report.dockRotationDegrees = dock.taken ? dock.rotationDegrees : -1f;
            report.dockCursorDistanceMM = dock.taken && cursor.Present
                ? dock.cursorDistanceUnits / AppConstants.MM_TO_UNITS
                : -1f;

            for (int index = 0; index < neighbours.Count; index++)
            {
                var other = neighbours[index];
                if (other == moved) continue;

                var facts = SnapNeighbourFacts.Of(movedGeo, testPosition, scene[index], maxDist);

                var n = new SnapNeighborReport
                {
                    name = other.PartName,
                    centerDistanceMM = Vector3.Distance(testPosition, other.transform.position) / AppConstants.MM_TO_UNITS,
                    intersects = ElementsIntersectAt(moved, movedVerts, other),
                    bestDot = facts.bestDot,
                    hasFacingFaces = facts.hasFacingFaces,
                    movedFaceIndex = facts.movedFaceIndex,
                    otherFaceIndex = facts.otherFaceIndex,
                    gapMM = facts.distanceUnits >= 0f
                        ? facts.distanceUnits / AppConstants.MM_TO_UNITS
                        : -1f,
                    overlapRatio = facts.overlapRatio,
                    withinThreshold = facts.withinThreshold,
                    overlapEnough = facts.overlapEnough,
                    wouldSnap = facts.wouldSnap,
                    bothCarryPorts = facts.portSeat.bothSidesCarryPorts,
                    portSeatOffered = facts.portSeat.taken,
                    portGapMM = facts.portSeat.mouthGapUnits >= 0f
                        ? facts.portSeat.mouthGapUnits / AppConstants.MM_TO_UNITS
                        : -1f,
                    portSeatShiftMM = facts.portSeat.taken
                        ? facts.portSeat.delta.magnitude / AppConstants.MM_TO_UNITS
                        : -1f,
                    portsFaceEachOther = facts.portSeat.opposed,
                    movedPortIndex = facts.portSeat.movedPort,
                    otherPortIndex = facts.portSeat.otherPort,
                    alignmentLandsInsideNeighbour = facts.alignmentLandsInsideNeighbour,
                };
                var pairDock = SnapPortDock.For(movedPorted, scenePorted[index], maxDist, cursor);
                n.dockOffered = pairDock.taken;
                n.dockRotationDegrees = pairDock.taken ? pairDock.rotationDegrees : -1f;
                n.dockCursorDistanceMM = pairDock.taken && cursor.Present
                    ? pairDock.cursorDistanceUnits / AppConstants.MM_TO_UNITS
                    : -1f;
                if (winner.taken) n.wouldSnap = IsTheWinner(n, winner);
                n.verdict = Verdict(n, facts, report.thresholdMM, winner)
                    + DockTail(n, pairDock, dock, cursor.Present);

                report.neighbors.Add(n);
            }

            report.neighbors.Sort((a, b) => SortKeyMM(a).CompareTo(SortKeyMM(b)));
            if (report.neighbors.Count > maxNeighbors)
                report.neighbors.RemoveRange(maxNeighbors, report.neighbors.Count - maxNeighbors);

            return report;
        }

        private static string Verdict(SnapNeighborReport n, in SnapNeighbourFacts facts,
            float thresholdMM, in SnapPortSeat winner)
        {
            if (facts.portSeat.taken && IsTheWinner(n, winner))
                return facts.portSeat.alreadySeated
                    ? $"OK — устья {n.movedPortIndex} и {n.otherPortIndex} уже совмещены, "
                      + "сдвига не будет"
                    : $"OK — сядет устьем {n.movedPortIndex} на устье {n.otherPortIndex}, "
                      + $"сдвиг {n.portSeatShiftMM:F2} мм"
                      + (n.portsFaceEachOther ? string.Empty : " (оси устьев не встречные)");

            if (facts.portSeat.taken)
                return $"устья сходятся за {n.portSeatShiftMM:F2} мм, но посадку заберёт "
                       + $"{winner.targetName}: там устье в устье ближе";

            if (winner.taken)
                return $"снэп сядет устьем на устье с {winner.targetName}, грани этой пары "
                       + "в отборе не участвуют" + PortTail(n);

            if (!n.hasFacingFaces)
                return $"нет встречных параллельных граней (лучший dot={n.bestDot:F3}) — деталь повёрнута?" + PortTail(n);
            if (!facts.hasCandidateFaces)
                return NotConsidered(facts.exclusion) + PortTail(n);
            if (!n.withinThreshold)
                return $"зазор {n.gapMM:F1} мм больше порога {thresholdMM:F0} мм" + PortTail(n);
            if (!n.overlapEnough)
                return $"перекрытие граней {n.overlapRatio:P0} меньше минимума 30%" + PortTail(n);
            if (!n.wouldSnap)
                return Blocked(facts) + PortTail(n);
            if (n.intersects)
                return "AABB пересекаются из-за поворота, но снэп сработает (разведёт детали заподлицо)" + PortTail(n);
            return "OK — прилипнет" + PortTail(n);
        }

        private static string DockTail(SnapNeighborReport n, in SnapPortDock pair,
            in SnapPortDock winner, bool cursorKnown)
        {
            if (!pair.taken) return string.Empty;

            if (winner.taken && winner.targetId == pair.targetId
                && winner.movedPort == pair.movedPort && winner.otherPort == pair.otherPort)
                return $" | при отпускании кнопки деталь довернётся на "
                       + $"{pair.rotationDegrees:F0}° и сядет устьем {pair.movedPort} на устье "
                       + $"{pair.otherPort}" + CursorTail(cursorKnown, n.dockCursorDistanceMM);

            return $" | доворотом сюда деталь тоже села бы, но посадку заберёт "
                   + $"{winner.targetName}" + CursorTail(cursorKnown, n.dockCursorDistanceMM);
        }

        private static string CursorTail(bool cursorKnown, float cursorDistanceMM) =>
            cursorKnown
                ? $" (устье в {cursorDistanceMM:F0} мм от луча курсора)"
                : " (курсор не передан: конкуренцию за доворот решил зазор между устьями, "
                  + "а в приложении её решает ближайшая к указателю мыши деталь)";

        private static bool IsTheWinner(SnapNeighborReport n, in SnapPortSeat winner) =>
            winner.taken && winner.targetName == n.name
            && winner.movedPort == n.movedPortIndex && winner.otherPort == n.otherPortIndex;

        private static string PortTail(SnapNeighborReport n) =>
            !n.bothCarryPorts
                ? string.Empty
                : $" | устья: ближайшие в {n.portGapMM:F2} мм, посадки по устьям не будет";

        private static string NotConsidered(SnapPairRejection why) => why switch
        {
            SnapPairRejection.OffTheMountAxis =>
                "встречные грани есть, но у центрующейся детали отбор берёт только грани оси "
                + "крепления — по этой паре снэпа не будет",
            SnapPairRejection.SupersededByGrooveSeat =>
                "грань перекрыта посадочной гранью паза — снэп идёт по пазу, а не по ней",
            SnapPairRejection.CoDirectionalMount =>
                "сонаправленная грань у центрующейся детали в отборе не участвует",
            SnapPairRejection.CoDirectionalGrooveSeat =>
                "сонаправленная грань паза в отборе не участвует",
            _ => "встречные грани есть, но отбор кандидатов их не рассматривает",
        };

        private static string Blocked(in SnapNeighbourFacts facts) => facts.rejection switch
        {
            SnapPairRejection.LandsInsideNeighbour =>
                "снэп загнал бы деталь внутрь соседа — такой кандидат отбрасывается",
            SnapPairRejection.AlreadyInPlace => facts.role == SnapPairRole.Centring
                ? "посадка уже выполнена: деталь стоит по центру, сдвига не будет"
                : facts.alignmentLandsInsideNeighbour
                    ? "плоскости совпадают, но габариты деталей вложены друг в друга — "
                      + "выравнивание по этой грани загнало бы деталь внутрь соседа, "
                      + "поэтому снэпа по ней не будет ни сейчас, ни после сдвига"
                    : "деталь уже стоит заподлицо по этой грани — сдвига не будет",
            _ => "отбор кандидатов эту пару не принял",
        };

        private static float SortKeyMM(SnapNeighborReport n)
            => n.portSeatOffered ? n.portSeatShiftMM
                : n.gapMM >= 0 ? n.gapMM : n.centerDistanceMM;

        public static bool ElementsIntersect(KitchenElement a, KitchenElement b)
            => ElementsIntersectAt(a, a.GetVertices(), b);

        public static bool ElementsIntersectAt(KitchenElement a, Vector3[] va, KitchenElement b)
        {
            Vector3[] vb = b.GetVertices();

            float aMinX = va[0].x, aMaxX = va[0].x;
            float aMinY = va[0].y, aMaxY = va[0].y;
            float aMinZ = va[0].z, aMaxZ = va[0].z;
            for (int i = 1; i < 8; i++)
            {
                aMinX = Mathf.Min(aMinX, va[i].x); aMaxX = Mathf.Max(aMaxX, va[i].x);
                aMinY = Mathf.Min(aMinY, va[i].y); aMaxY = Mathf.Max(aMaxY, va[i].y);
                aMinZ = Mathf.Min(aMinZ, va[i].z); aMaxZ = Mathf.Max(aMaxZ, va[i].z);
            }

            float bMinX = vb[0].x, bMaxX = vb[0].x;
            float bMinY = vb[0].y, bMaxY = vb[0].y;
            float bMinZ = vb[0].z, bMaxZ = vb[0].z;
            for (int i = 1; i < 8; i++)
            {
                bMinX = Mathf.Min(bMinX, vb[i].x); bMaxX = Mathf.Max(bMaxX, vb[i].x);
                bMinY = Mathf.Min(bMinY, vb[i].y); bMaxY = Mathf.Max(bMaxY, vb[i].y);
                bMinZ = Mathf.Min(bMinZ, vb[i].z); bMaxZ = Mathf.Max(bMaxZ, vb[i].z);
            }

            return Tolerance.IntervalsOverlap(aMinX, aMaxX, bMinX, bMaxX) &&
                   Tolerance.IntervalsOverlap(aMinY, aMaxY, bMinY, bMaxY) &&
                   Tolerance.IntervalsOverlap(aMinZ, aMaxZ, bMinZ, bMaxZ);
        }
    }
}
