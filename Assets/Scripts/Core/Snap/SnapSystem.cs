using System.Collections.Generic;
using UnityEngine;

namespace KitchenDesigner.Core
{
    /// <summary>Диагностика прилипания к одному соседу: какая пара граней ближайшая
    /// и по какой причине снэп прошёл/не прошёл. Все расстояния — в мм.</summary>
    [System.Serializable]
    public class SnapNeighborReport
    {
        public string name = string.Empty;
        public float centerDistanceMM;      // расстояние между центрами деталей
        public bool intersects;             // AABB-пересечение — снэп с этой деталью невозможен
        public bool hasFacingFaces;         // есть ли встречные параллельные грани (dot≈-1)
        public float bestDot;               // самый «встречный» dot нормалей (-1 = идеально)
        public int movedFaceIndex = -1;     // лучшая пара граней (движимая/цель)
        public int otherFaceIndex = -1;
        public float gapMM = -1f;           // зазор по нормали у лучшей пары
        public float overlapRatio = -1f;    // перекрытие граней у лучшей пары (0..1+)
        public bool withinThreshold;
        public bool overlapEnough;
        public bool wouldSnap;              // все условия для ЭТОЙ детали выполнены
        public string verdict = string.Empty;              // человекочитаемая причина
    }

    /// <summary>Итог диагностики: общие настройки, результат TrySnap и разбор по соседям.</summary>
    [System.Serializable]
    public class SnapDiagnosis
    {
        public bool snapEnabled;
        public float thresholdMM;
        public bool wouldSnap;
        public string? snapTarget;
        public List<SnapNeighborReport> neighbors = new List<SnapNeighborReport>();
    }

    /// <summary>Прилипание со стороны СЦЕНЫ: собирает снимки геометрии и отдаёт
    /// расчёт в <see cref="SnapCore"/>, а сам знает про KitchenElement, настройки
    /// и логи. Сама математика живёт в KitchenDesigner.Geometry и исполняется
    /// без Unity — см. docs/GEOMETRY-EXTRACTION-PLAN.md.</summary>
    public static class SnapSystem
    {
        private const float ThresholdEpsilon = Tolerance.SnapEpsilon;

        /// <summary>Логировать выбор снэпа (для отладки прилипания). По умолчанию выкл.</summary>
        public static bool VerboseLog = false;

        /// <summary>Деталь как источник геометрии для произвольной позиции.
        /// Класс, а не структура: ядро держит его по интерфейсу, и упаковка
        /// структуры всё равно случилась бы — только на каждом вызове.</summary>
        private sealed class PosedElement : IPosedGeometry
        {
            private readonly KitchenElement _element;
            public PosedElement(KitchenElement element) => _element = element;
            public ElementGeometry At(Vector3 position) => _element.ToGeometryAt(position);
        }

        /// <summary>Прилипание детали, примеряемой в testPosition. Сцену не меняет:
        /// геометрия для примерки считается аналитически.</summary>
        public static SnapResult TrySnap(KitchenElement moved, List<KitchenElement> others, Vector3 testPosition)
        {
            if (moved == null || others == null) return default;
            if (!KitchenSettings.Instance.SnapEnabled) return default;
            if (!moved.gameObject.activeInHierarchy) return default;

            return TrySnap(moved, others.ToGeometry(), testPosition);
        }

        /// <summary>То же, но по ГОТОВЫМ снимкам соседей. Для горячих путей, где
        /// соседи между вызовами не двигаются: пересборка снимков на каждый вызов
        /// стоит дороже самого расчёта прилипания.</summary>
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


        /// <summary>
        /// Диагностика: почему деталь прилипает/не прилипает из позиции testPosition.
        /// Прогоняет ту же геометрию, что и <see cref="TrySnap"/>, но вместо раннего
        /// отсева собирает по каждому соседу лучшую пару граней и причину отказа:
        /// пересечение AABB, нет встречных граней, зазор больше порога, перекрытие &lt;30%.
        /// Сцену не меняет (позиция восстанавливается). Соседи отсортированы по зазору.
        /// </summary>
        public static SnapDiagnosis Diagnose(KitchenElement moved, List<KitchenElement> others,
            Vector3 testPosition, int maxNeighbors = 5)
            => Diagnose(moved, others, testPosition, null, maxNeighbors);

        /// <summary>То же, но с УЖЕ вычисленным результатом <see cref="TrySnap"/>
        /// для той же тройки (moved, others, testPosition). Прилипание — чистая
        /// функция от неё, поэтому передать готовый результат и посчитать заново —
        /// одно и то же; свип, которому разбор нужен лишь на сработавших шагах,
        /// экономит на этом второй полный проход снэпа.</summary>
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

            // Геометрия примеряемой позиции считается аналитически — сцена не
            // трогается (см. Collect).
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

                // Те же грани, что видит TrySnap: шесть габаритных плюс дно каждого
                // паза (только для вкладной панели). Без дна паза диагностика
                // сообщала «не прилипнет» там, где TrySnap сажает панель в паз.
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
                        // Над пазом материала нет — как и в Collect.
                        if (!isGroove && GrooveSeating.SeatSupersedesFace(mf, of, seatFaces)) continue;
                        n.hasFacingFaces = true;

                        float gap = Mathf.Abs(Vector3.Dot(of.center - mf.center, mf.normal));
                        bool hasOverlap = FaceContacts.OverlapAllowingEdgeTouch(mf, of, out float ratio, out _);
                        bool within = gap <= maxDist;
                        bool enough = hasOverlap && ratio >= Tolerance.MinSupportOverlap;

                        // Ранг важнее зазора: пара, по которой снэп РЕАЛЬНО возможен,
                        // всегда лучше более близкой, но негодной. Раньше отбор шёл по
                        // одному зазору, и вердикт писался по случайной ближней паре с
                        // перекрытием 10% — Diagnose говорил «не прилипнет», а TrySnap
                        // прилипал по другой, полноценной паре того же соседа.
                        int rank = (within && enough) ? 0 : (hasOverlap ? 1 : 2);
                        if (rank < bestRank || (rank == bestRank && gap < bestGap))
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

            // Ближние — первыми; у деталей без встречных граней зазора нет,
            // ранжируем их по расстоянию между центрами.
            report.neighbors.Sort((a, b) =>
                (a.gapMM >= 0 ? a.gapMM : a.centerDistanceMM)
                .CompareTo(b.gapMM >= 0 ? b.gapMM : b.centerDistanceMM));
            if (report.neighbors.Count > maxNeighbors)
                report.neighbors.RemoveRange(maxNeighbors, report.neighbors.Count - maxNeighbors);

            return report;
        }

        public static bool ElementsIntersect(KitchenElement a, KitchenElement b)
            => ElementsIntersectAt(a, a.GetVertices(), b);

        /// <summary>То же, но габарит A берётся по ГОТОВЫМ вершинам — например,
        /// посчитанным для примеряемой позиции. Раньше для этого деталь двигали
        /// записью в transform.position.</summary>
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
