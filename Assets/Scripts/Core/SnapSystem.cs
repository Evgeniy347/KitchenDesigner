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

    public static class SnapSystem
    {
        // Допуск к порогу (0.01 мм): прилипание срабатывает и ровно на границе
        // порога, несмотря на ошибку округления float при вычислении зазора.
        private const float ThresholdEpsilon = 1e-5f;

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

            SnapResult best = default;
            float bestDist = float.MaxValue;
            string bestLog = null;

            foreach (var other in others)
            {
                if (other == moved || other == null) continue;
                if (!other.gameObject.activeInHierarchy) continue;
                if (ElementsIntersect(moved, other)) continue;

                KitchenElement.Face[] otherFaces = other.GetFaces();

                for (int i = 0; i < 6; i++)
                {
                    for (int j = 0; j < 6; j++)
                    {
                        // Контакт возможен только между гранями, смотрящими навстречу
                        // друг другу (нормали противоположны, dot≈-1). Со-направленные
                        // грани (dot≈+1) не образуют стык — иначе доска липла бы «не с той стороны».
                        float dot = Vector3.Dot(movedFaces[i].normal, otherFaces[j].normal);
                        if (dot > -0.999f) continue;

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
                        // (а не принудительно по центру — иначе мелкая доска центрируется).
                        Vector3 u = mf.rightAxis;
                        Vector3 v = mf.upAxis;
                        Rect mRect = GetFaceRect(mf, u, v);
                        Rect oRect = GetFaceRect(of, u, v);
                        float du = BestEdgeDelta(mRect.xMin, mRect.xMax, oRect.xMin, oRect.xMax, maxDist, out string labelU);
                        float dv = BestEdgeDelta(mRect.yMin, mRect.yMax, oRect.yMin, oRect.yMax, maxDist, out string labelV);

                        // Точное выравнивание заподлицо. Сетку НЕ применяем: при крупном
                        // шаге она сдвинула бы доску с плоскости контакта и разорвала стык.
                        Vector3 snapPos = testPosition + planeShift * mf.normal + du * u + dv * v;

                        float dist = Vector3.Distance(snapPos, testPosition);
                        if (dist < bestDist)
                        {
                            bestDist = dist;
                            best = new SnapResult
                            {
                                snapped = true,
                                position = snapPos,
                                targetName = other.BoardName,
                                faceIndex = j,
                                snapPoint = mf.center,
                                targetPoint = of.center
                            };
                            if (VerboseLog)
                                bestLog = $"[Snap] {moved.Describe()} → {other.Describe()} | грань m{i}/o{j} " +
                                          $"зазор={planeDist * 1000f:F2}мм перекр={overlapRatio:P0} " +
                                          $"оси[u:{labelU} v:{labelV}] → поз {snapPos.x:F3},{snapPos.y:F3},{snapPos.z:F3}";
                        }
                    }
                }
            }

            moved.transform.position = prevPos;

            if (VerboseLog && bestLog != null) Debug.Log(bestLog);
            return best;
        }

        /// <summary>Логировать выбор снэпа (для отладки прилипания). По умолчанию выкл.</summary>
        public static bool VerboseLog = false;

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

            if (interLeft >= interRight || interBottom >= interTop)
            {
                overlapRatio = 0;
                return false;
            }

            float overlapArea = (interRight - interLeft) * (interTop - interBottom);
            float minArea = Mathf.Min(aRect.width * aRect.height, bRect.width * bRect.height);
            overlapRatio = minArea > 0 ? overlapArea / minArea : 0;
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

        // Допуск (0.1 мм) для AABB-пересечения: доски, стоящие вплотную гранями,
        // из-за погрешности float могут давать ничтожное (~1e-9 м) перекрытие.
        // Без допуска такой контакт ошибочно считался бы пересечением и
        // пропускался валидатором/снэпом. 0.1 мм заметно меньше порога контакта 0.5 мм.
        private const float IntersectEpsilon = 1e-4f;

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

            return aMinX < bMaxX - IntersectEpsilon && aMaxX > bMinX + IntersectEpsilon &&
                   aMinY < bMaxY - IntersectEpsilon && aMaxY > bMinY + IntersectEpsilon &&
                   aMinZ < bMaxZ - IntersectEpsilon && aMaxZ > bMinZ + IntersectEpsilon;
        }
    }
}
