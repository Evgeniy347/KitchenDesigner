using System.Collections.Generic;
using UnityEngine;

namespace KitchenDesigner.Core
{
    public struct SnapResult
    {
        public bool snapped;
        public Vector3 position;
        public string targetName;
        public int faceIndex;
        public Vector3 snapPoint;
        public Vector3 targetPoint;
    }

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

    public static class SnapSystem
    {
        // Допуск к порогу: прилипание срабатывает и ровно на границе порога,
        // несмотря на ошибку округления float при вычислении зазора.
        private const float ThresholdEpsilon = Tolerance.SnapEpsilon;

        // «Нулевое» смещение (0.1 мм): кандидат, чей сдвиг меньше, — это уже
        // существующий контакт (деталь и так заподлицо), а не новое прилипание.
        // Такой кандидат не должен побеждать содержательные снэпы — иначе деталь,
        // скользящая по грани соседа (или стоящая на полу), никогда не прилипнет
        // к стене: подтверждение текущего контакта (сдвиг 0) всегда «ближе».
        private const float ZeroShiftEpsilon = Tolerance.EpsilonUnits;

        // Прилипание — чистая детерминированная функция от (moved, others, testPos).
        // Без скрытого статического состояния: одинаковый вход → одинаковый выход,
        // что критично для предсказуемости в рантайме и для повторяемости тестов.
        public static SnapResult TrySnap(KitchenElement moved, List<KitchenElement> others, Vector3 testPosition)
        {
            if (moved == null || others == null) return default;
            if (!KitchenSettings.Instance.SnapEnabled) return default;
            if (!moved.gameObject.activeInHierarchy) return default;

            float threshold = KitchenSettings.Instance.SnapThreshold * AppConstants.MM_TO_UNITS;
            float maxDist = threshold + ThresholdEpsilon;
            Vector3 prevPos = moved.transform.position;
            moved.transform.position = testPosition;
            KitchenElement.Face[] movedFaces = moved.GetFaces();

            // Кандидаты делятся на два сорта:
            //  - «нулевые» (сдвиг ≈ 0) — деталь УЖЕ заподлицо с этой гранью; это
            //    подтверждение текущего контакта, а не новое прилипание;
            //  - содержательные — реальное притяжение к новой грани.
            // Содержательный снэп предпочтительнее нулевого, но не должен рвать
            // существующие контакты: его сдвиг обязан быть ⊥ нормалям нулевых пар.
            SnapResult bestZero = default;
            string? bestZeroLog = null;
            var zeroNormals = new List<Vector3>();
            var candidates = new List<(float dist, SnapResult result, string? log)>();

            foreach (var other in others)
            {
                if (other == moved || other == null) continue;
                if (!other.gameObject.activeInHierarchy) continue;
                // AABB-пересечение НЕ отсеиваем: для повёрнутых деталей AABB может
                // быть избыточно большим и ложно блокировать снэп перпендикулярных
                // кромок. Face-pair loop ниже сам отфильтрует глубокие пересечения
                // по planeDist > maxDist.

                KitchenElement.Face[] otherFaces = other.GetFaces();

                for (int i = 0; i < 6; i++)
                {
                    for (int j = 0; j < 6; j++)
                    {
                        // Контакт возможен только между гранями, смотрящими навстречу
                        // друг другу (нормали противоположны, dot≈-1). Со-направленные
                        // грани (dot≈+1) не образуют стык — иначе деталь липла бы «не с той стороны».
                        float dot = Vector3.Dot(movedFaces[i].normal, otherFaces[j].normal);
                        if (!Tolerance.IsParallel(dot) || dot > 0) continue;

                        var mf = movedFaces[i];
                        var of = otherFaces[j];

                        Vector3 offset = of.center - mf.center;
                        float planeDist = Mathf.Abs(Vector3.Dot(offset, mf.normal));
                        if (planeDist > maxDist) continue;

                        if (!FacesOverlap(mf, of, out float overlapRatio))
                            continue;
                        if (overlapRatio < 0.3f) continue;

                        // Сдвиг вдоль нормали: плоскости становятся заподлицо.
                        float planeShift = Vector3.Dot(offset, mf.normal);

                        // Сдвиг в плоскости грани: выравнивание по ближайшей кромке/центру
                        // (а не принудительно по центру — иначе мелкая деталь центрируется).
                        Vector3 u = mf.rightAxis;
                        Vector3 v = mf.upAxis;
                        Rect mRect = GetFaceRect(mf, u, v);
                        Rect oRect = GetFaceRect(of, u, v);
                        float du = BestEdgeDelta(mRect.xMin, mRect.xMax, oRect.xMin, oRect.xMax, maxDist, out string labelU);
                        float dv = BestEdgeDelta(mRect.yMin, mRect.yMax, oRect.yMin, oRect.yMax, maxDist, out string labelV);

                        // Точное выравнивание заподлицо. Сетку НЕ применяем: при крупном
                        // шаге она сдвинула бы деталь с плоскости контакта и разорвала стык.
                        Vector3 snapPos = testPosition + planeShift * mf.normal + du * u + dv * v;

                        float dist = Vector3.Distance(snapPos, testPosition);
                        var result = new SnapResult
                        {
                            snapped = true,
                            position = snapPos,
                            targetName = other.PartName,
                            faceIndex = j,
                            snapPoint = mf.center,
                            targetPoint = of.center
                        };
                        string? log = VerboseLog
                            ? $"[Snap] {moved.Describe()} → {other.Describe()} | грань m{i}/o{j} " +
                              $"зазор={planeDist * 1000f:F2}мм перекр={overlapRatio:P0} " +
                              $"оси[u:{labelU} v:{labelV}] → поз {snapPos.x:F3},{snapPos.y:F3},{snapPos.z:F3}"
                            : null;

                        if (dist <= ZeroShiftEpsilon)
                        {
                            zeroNormals.Add(mf.normal);
                            if (!bestZero.snapped) { bestZero = result; bestZeroLog = log; }
                        }
                        else
                        {
                            candidates.Add((dist, result, log));
                        }
                    }
                }
            }

            moved.transform.position = prevPos;

            // Лучший содержательный кандидат, не отрывающий деталь от существующих
            // контактов (сдвиг перпендикулярен их нормалям).
            candidates.Sort((a, b) => a.dist.CompareTo(b.dist));
            foreach (var c in candidates)
            {
                Vector3 shift = c.result.position - testPosition;
                bool breaksContact = false;
                foreach (var n in zeroNormals)
                {
                    if (Mathf.Abs(Vector3.Dot(shift, n)) > ZeroShiftEpsilon) { breaksContact = true; break; }
                }
                if (breaksContact) continue;

                if (VerboseLog && c.log != null) Debug.Log(c.log);
                return c.result;
            }

            // Содержательных нет (или все рвут контакты) — подтверждаем текущий
            // контакт (прежнее поведение: снэп «на месте»).
            if (VerboseLog && bestZeroLog != null) Debug.Log(bestZeroLog);
            return bestZero;
        }

        /// <summary>Логировать выбор снэпа (для отладки прилипания). По умолчанию выкл.</summary>
        public static bool VerboseLog = false;

        /// <summary>
        /// Диагностика: почему деталь прилипает/не прилипает из позиции testPosition.
        /// Прогоняет ту же геометрию, что и <see cref="TrySnap"/>, но вместо раннего
        /// отсева собирает по каждому соседу лучшую пару граней и причину отказа:
        /// пересечение AABB, нет встречных граней, зазор больше порога, перекрытие &lt;30%.
        /// Сцену не меняет (позиция восстанавливается). Соседи отсортированы по зазору.
        /// </summary>
        public static SnapDiagnosis Diagnose(KitchenElement moved, List<KitchenElement> others,
            Vector3 testPosition, int maxNeighbors = 5)
        {
            var settings = KitchenSettings.Instance;
            var report = new SnapDiagnosis
            {
                snapEnabled = settings != null && settings.SnapEnabled,
                thresholdMM = settings != null ? settings.SnapThreshold : 0f,
            };
            if (moved == null || others == null) return report;

            var snap = TrySnap(moved, others, testPosition);
            report.wouldSnap = snap.snapped;
            report.snapTarget = snap.snapped ? snap.targetName : null;

            float maxDist = report.thresholdMM * AppConstants.MM_TO_UNITS + ThresholdEpsilon;

            Vector3 prevPos = moved.transform.position;
            moved.transform.position = testPosition;
            KitchenElement.Face[] movedFaces = moved.GetFaces();

            foreach (var other in others)
            {
                if (other == moved || other == null) continue;
                if (!other.gameObject.activeInHierarchy) continue;

                var n = new SnapNeighborReport
                {
                    name = other.PartName,
                    centerDistanceMM = Vector3.Distance(testPosition, other.transform.position) / AppConstants.MM_TO_UNITS,
                    intersects = ElementsIntersect(moved, other),
                    bestDot = 1f,
                };

                KitchenElement.Face[] otherFaces = other.GetFaces();
                float bestScore = float.MaxValue;

                for (int i = 0; i < 6; i++)
                {
                    for (int j = 0; j < 6; j++)
                    {
                        float dot = Vector3.Dot(movedFaces[i].normal, otherFaces[j].normal);
                        n.bestDot = Mathf.Min(n.bestDot, dot);
                        if (!Tolerance.IsParallel(dot) || dot > 0) continue;
                        n.hasFacingFaces = true;

                        var mf = movedFaces[i];
                        var of = otherFaces[j];
                        float gap = Mathf.Abs(Vector3.Dot(of.center - mf.center, mf.normal));
                        bool hasOverlap = FacesOverlap(mf, of, out float ratio);

                        // Лучшая пара — с перекрытием и минимальным зазором;
                        // пары без перекрытия штрафуются, но остаются кандидатами.
                        float score = gap + (hasOverlap ? 0f : 1000f);
                        if (score < bestScore)
                        {
                            bestScore = score;
                            n.movedFaceIndex = i;
                            n.otherFaceIndex = j;
                            n.gapMM = gap / AppConstants.MM_TO_UNITS;
                            n.overlapRatio = hasOverlap ? ratio : 0f;
                            n.withinThreshold = gap <= maxDist;
                            n.overlapEnough = hasOverlap && ratio >= 0.3f;
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

            moved.transform.position = prevPos;

            // Ближние — первыми; у деталей без встречных граней зазора нет,
            // ранжируем их по расстоянию между центрами.
            report.neighbors.Sort((a, b) =>
                (a.gapMM >= 0 ? a.gapMM : a.centerDistanceMM)
                .CompareTo(b.gapMM >= 0 ? b.gapMM : b.centerDistanceMM));
            if (report.neighbors.Count > maxNeighbors)
                report.neighbors.RemoveRange(maxNeighbors, report.neighbors.Count - maxNeighbors);

            return report;
        }

        /// <summary>
        /// Лучшее выравнивание интервала [aMin,aMax] к [bMin,bMax] вдоль оси:
        /// кандидаты — совпадение минимумов, максимумов, центров. Возвращает
        /// наименьший по модулю сдвиг в пределах порога, иначе 0 (ось не снэпится).
        /// </summary>
        private static float BestEdgeDelta(float aMin, float aMax, float bMin, float bMax, float threshold, out string label)
        {
            float aCenter = (aMin + aMax) * 0.5f;
            float bCenter = (bMin + bMax) * 0.5f;

            var candidates = new (float d, string name)[]
            {
                (bMin - aMin, "кромка-"),
                (bMax - aMax, "кромка+"),
                (bCenter - aCenter, "центр"),
            };

            float best = 0f;
            float bestAbs = float.MaxValue;
            label = "своб";
            foreach (var c in candidates)
            {
                float abs = Mathf.Abs(c.d);
                if (abs <= threshold && abs < bestAbs)
                {
                    bestAbs = abs;
                    best = c.d;
                    label = c.name;
                }
            }
            return best;
        }

        private static bool FacesOverlap(KitchenElement.Face a, KitchenElement.Face b, out float overlapRatio)
        {
            Vector3 u = a.rightAxis;
            Vector3 v = a.upAxis;

            Rect aRect = GetFaceRect(a, u, v);
            Rect bRect = GetFaceRect(b, u, v);

            float interLeft = Mathf.Max(aRect.xMin, bRect.xMin);
            float interRight = Mathf.Min(aRect.xMax, bRect.xMax);
            float interBottom = Mathf.Max(aRect.yMin, bRect.yMin);
            float interTop = Mathf.Min(aRect.yMax, bRect.yMax);

            // Epsilon-допуск: без него грани, касающиеся ровно по кромке
            // (interLeft == interRight или interBottom == interTop), дают
            // недетерминированный результат из-за float-погрешности:
            // иногда overlapRatio ≈ 100% (ошибка), иногда 0% (правильно).
            // С допуском точное касание всегда считается нулевым перекрытием.
            if (interLeft + Tolerance.SnapEpsilon >= interRight || interBottom + Tolerance.SnapEpsilon >= interTop)
            {
                overlapRatio = 0;
                return false;
            }

            float overlapU = interRight - interLeft;
            float overlapV = interTop - interBottom;

            // Перекрытие по каждой оси относительно меньшего размера грани по этой оси.
            // В отличие от отношения площадей, произведение полуосевых отношений
            // корректно обрабатывает перпендикулярные узкие грани (18×400 и 18×1200):
            // площадь перекрытия 18×18 = 4.5% площади min-грани (7200), но по каждой
            // оси перекрытие составляет 100% от меньшего размера (18), и произведение
            // даёт 1.0 — снэп срабатывает.
            float ratioU = Mathf.Min(aRect.width, bRect.width) > 0
                ? overlapU / Mathf.Min(aRect.width, bRect.width) : 0;
            float ratioV = Mathf.Min(aRect.height, bRect.height) > 0
                ? overlapV / Mathf.Min(aRect.height, bRect.height) : 0;
            overlapRatio = ratioU * ratioV;
            return true;
        }

        private static Rect GetFaceRect(KitchenElement.Face face, Vector3 u, Vector3 v)
        {
            Vector2 center = new Vector2(
                Vector3.Dot(face.center, u),
                Vector3.Dot(face.center, v)
            );

            float halfU = Mathf.Abs(Vector3.Dot(face.rightAxis, u)) * face.size.x * 0.5f
                       + Mathf.Abs(Vector3.Dot(face.upAxis, u)) * face.size.y * 0.5f;
            float halfV = Mathf.Abs(Vector3.Dot(face.rightAxis, v)) * face.size.x * 0.5f
                       + Mathf.Abs(Vector3.Dot(face.upAxis, v)) * face.size.y * 0.5f;

            return new Rect(center.x - halfU, center.y - halfV, halfU * 2, halfV * 2);
        }

        public static bool ElementsIntersect(KitchenElement a, KitchenElement b)
        {
            Vector3[] va = a.GetVertices();
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
