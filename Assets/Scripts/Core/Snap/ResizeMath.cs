using System.Collections.Generic;
using UnityEngine;

namespace KitchenDesigner.Core
{
    /// <summary>Чистая математика вытягивания грани: по стартовой геометрии грани и
    /// сырой дельте (вдоль нормали) считает новые размеры (мм) и центр объекта, с
    /// учётом прилипания к встречным граням (ResizeSnap). Покрывается юнит-тестами,
    /// чтобы связка «прилипание + растягивание» проверялась без сцены.</summary>
    public static class ResizeMath
    {
        public static void Compute(
            Vector3Int dimsBefore, int axisIndex,
            Vector3 normal, Vector3 faceCenter0, Vector3 uAxis, Vector3 vAxis, Vector2 faceSize,
            Vector3 centerStart, float sizeStartUnits,
            float rawDelta, IList<KitchenElement> others, KitchenElement self,
            bool snapEnabled, float threshold,
            out Vector3Int newDims, out Vector3 newCenter, out bool snapped)
        {
            snapped = false;
            float finalDelta = rawDelta;

            if (snapEnabled)
            {
                // Кандидат — положение грани после сырого вытягивания (в плоскости не
                // двигается, только вдоль нормали).
                Vector3 candidate = faceCenter0 + normal * rawDelta;
                if (ResizeSnap.SnapDelta(candidate, normal, uAxis, vAxis, faceSize,
                        others, self, threshold, out float gap))
                {
                    finalDelta = rawDelta + gap;
                    snapped = true;
                }
            }

            float newSizeUnits = sizeStartUnits + finalDelta;
            int newDimMM = Mathf.Max(1, Mathf.RoundToInt(newSizeUnits / AppConstants.MM_TO_UNITS));

            // Реальная (округлённая до мм) дельта — чтобы размер и центр не разъезжались.
            float actualDelta = newDimMM * AppConstants.MM_TO_UNITS - sizeStartUnits;

            // Снэп ставит грань заподлицо в НЕцелых мм, а RoundToInt мог удлинить
            // деталь на ≤0.5 мм СКВОЗЬ плоскость соседа: перекрытие больше допуска
            // (0.1 мм) валидатор считает пересечением — деталь краснела сразу после
            // «сработавшего» снэпа, а глазом сдвиг не виден. Правило: округлённая
            // грань не заходит за снэп-плоскость; при перехлёсте укорачиваем на 1 мм
            // (микрозазор < 1 мм вместо невидимого пересечения).
            if (snapped && actualDelta - finalDelta > Tolerance.EpsilonUnits * 0.5f)
            {
                newDimMM = Mathf.Max(1, newDimMM - 1);
                actualDelta = newDimMM * AppConstants.MM_TO_UNITS - sizeStartUnits;
            }

            newDims = dimsBefore;
            if (axisIndex == 0) newDims.x = newDimMM;
            else if (axisIndex == 1) newDims.y = newDimMM;
            else newDims.z = newDimMM;

            // Противоположная грань стоит на месте → центр смещается на половину дельты.
            newCenter = centerStart + normal * (actualDelta * 0.5f);
        }

        /// <summary>Размер вдоль оси (0=X,1=Y,2=Z) из вектора габаритов в мм.</summary>
        public static int DimAlong(Vector3Int dims, int axisIndex) =>
            axisIndex == 0 ? dims.x : (axisIndex == 1 ? dims.y : dims.z);

        /// <summary>Центр детали по ФАКТИЧЕСКИ принятому размеру. Деталь вправе
        /// зажать запрошенный размер (у ноги высота ограничена 80..130 мм, сечение
        /// фиксировано), и тогда newCenter из <see cref="Compute"/> посчитан для
        /// размера, которого нет: противоположная грань уезжает, деталь висит в
        /// воздухе. Пересчёт от принятого размера оставляет её на месте.</summary>
        public static Vector3 CenterForAppliedDims(Vector3 centerStart, Vector3 normal,
            float sizeStartUnits, Vector3Int appliedDims, int axisIndex)
        {
            float appliedDelta = DimAlong(appliedDims, axisIndex) * AppConstants.MM_TO_UNITS - sizeStartUnits;
            return centerStart + normal * (appliedDelta * 0.5f);
        }
    }
}
