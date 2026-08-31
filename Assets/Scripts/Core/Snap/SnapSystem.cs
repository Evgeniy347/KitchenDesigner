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
        public string verdict = string.Empty;
    }

    [System.Serializable]
    public class SnapDiagnosis
    {
        public bool snapEnabled;
        public float thresholdMM;
        public bool wouldSnap;
        public string? snapTarget;
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

            return TrySnap(moved, others.ToGeometry(), testPosition);
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
            => Diagnose(moved, others, testPosition, null, maxNeighbors);

        public static SnapDiagnosis Diagnose(KitchenElement moved, List<KitchenElement> others,
            Vector3 testPosition, SnapResult? knownSnap, int maxNeighbors = 5)
        {
            var settings = KitchenSettings.Instance;
            var report = new SnapDiagnosis
            {
                snapEnabled = settings != null && settings.SnapEnabled,
                thresholdMM = settings != null ? settings.SnapThreshold : 0f,
            };
            if (moved == null || others == null) return report;

            var snap = knownSnap ?? TrySnap(moved, others, testPosition);
            report.wouldSnap = snap.snapped;
            report.snapTarget = snap.snapped ? snap.targetName : null;

            float maxDist = report.thresholdMM * AppConstants.MM_TO_UNITS + ThresholdEpsilon;

            Face[] movedFaces = moved.GetFacesAt(testPosition);
            Vector3[] movedVerts = moved.GetVerticesAt(testPosition);

            foreach (var other in others)
            {
                if (other == moved || other == null) continue;
                if (!other.gameObject.activeInHierarchy) continue;

                var n = new SnapNeighborReport
                {
                    name = other.PartName,
                    centerDistanceMM = Vector3.Distance(testPosition, other.transform.position) / AppConstants.MM_TO_UNITS,
                    intersects = ElementsIntersectAt(moved, movedVerts, other),
                    bestDot = 1f,
                };

                Face[] otherFaces = FaceCache.GetFaces(other);

                Face[] seatFaces = moved is PanelElement
                    ? other.GetGrooveSeatFaces()
                    : System.Array.Empty<Face>();

                int bestRank = int.MaxValue;
                float bestGap = float.MaxValue;

                for (int i = 0; i < 6; i++)
                {
                    for (int j = 0; j < 6 + seatFaces.Length; j++)
                    {
                        bool isGroove = j >= 6;
                        var of = isGroove ? seatFaces[j - 6] : otherFaces[j];

                        float dot = Vector3.Dot(movedFaces[i].normal, of.normal);
                        n.bestDot = Mathf.Min(n.bestDot, dot);
                        if (!Tolerance.IsParallel(dot) || dot > 0) continue;

                        var mf = movedFaces[i];
                        if (!isGroove && GrooveSeating.SeatSupersedesFace(mf, of, seatFaces)) continue;
                        n.hasFacingFaces = true;

                        float gap = Mathf.Abs(Vector3.Dot(of.center - mf.center, mf.normal));
                        bool hasOverlap = FaceContacts.OverlapAllowingEdgeTouch(mf, of, out float ratio, out _);
                        bool within = gap <= maxDist;
                        bool enough = hasOverlap && ratio >= Tolerance.MinSupportOverlap;

                        int rank = (within && enough) ? 0 : (hasOverlap ? 1 : 2);
                        bool betterPair = rank < bestRank || (rank == bestRank && gap < bestGap);
                        if (betterPair)
                        {
                            bestRank = rank;
                            bestGap = gap;
                            n.movedFaceIndex = i;
                            n.otherFaceIndex = j;
                            n.gapMM = gap / AppConstants.MM_TO_UNITS;
                            n.overlapRatio = hasOverlap ? ratio : 0f;
                            n.withinThreshold = within;
                            n.overlapEnough = enough;
                        }
                    }
                }

                n.wouldSnap = n.hasFacingFaces && n.withinThreshold && n.overlapEnough;
                n.verdict =
                    !n.hasFacingFaces ? $"нет встречных параллельных граней (лучший dot={n.bestDot:F3}) — деталь повёрнута?"
                    : !n.withinThreshold ? $"зазор {n.gapMM:F1} мм больше порога {report.thresholdMM:F0} мм"
                    : !n.overlapEnough ? $"перекрытие граней {n.overlapRatio:P0} меньше минимума 30%"
                    : n.intersects ? "AABB пересекаются из-за поворота, но снэп сработает (разведёт детали заподлицо)"
                    : "OK — прилипнет";

                report.neighbors.Add(n);
            }

            report.neighbors.Sort((a, b) => SortKeyMM(a).CompareTo(SortKeyMM(b)));
            if (report.neighbors.Count > maxNeighbors)
                report.neighbors.RemoveRange(maxNeighbors, report.neighbors.Count - maxNeighbors);

            return report;
        }

        private static float SortKeyMM(SnapNeighborReport n)
            => n.gapMM >= 0 ? n.gapMM : n.centerDistanceMM;

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
