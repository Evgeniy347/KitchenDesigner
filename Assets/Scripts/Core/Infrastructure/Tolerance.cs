using UnityEngine;

namespace KitchenDesigner.Core
{
    public static class Tolerance
    {
        /// <summary>Контакт деталей: |зазор| меньше — «касаются» (0.5 мм).
        /// Тот же порог, что ContactDistMM / GapEpsilonMm / MinOverlapMm.</summary>
        public const float ContactMm = 0.5f;

        /// <summary>Геометрический шум float в юнитах (= 0.1 мм). Меньше этого —
        /// координаты считаются равными.</summary>
        public const float EpsilonUnits = 1e-4f;

        /// <summary>Порог параллельности нормалей: |dot| >= этого — грани параллельны.</summary>
        public const float ParallelDot = 0.999f;

        /// <summary>Инклюзивный порог снэпа (1e-5 = 0.01 мм). Был ThresholdEpsilon
        /// в SnapSystem и ResizeSnap, OverlapEpsilon в FacesOverlap — чтобы снэп
        /// срабатывал и ровно на границе порога, несмотря на float-погрешность.</summary>
        public const float SnapEpsilon = 1e-5f;

        /// <summary>Квадрат минимальной значимой длины вектора. Был 1e-6f
        /// в ResizeHandleManager для проверки sqrMagnitude перед normalize.</summary>
        public const float EpsilonSqr = 1e-6f;

        /// <summary>Порог коллинеарности с Vector3.up: |dot| > этого → использовать
        /// запасной up. Был 0.99f в ResizeHandleManager для Quaternion.LookRotation.</summary>
        public const float UpDotThreshold = 0.99f;

        /// <summary>Допуск зазора в мм для допускового контроля (0.1 мм). Был
        /// зашит в DrawerValidator как ±ε рядом с MIN/MAX_CLEARANCE_PER_SIDE_MM.</summary>
        public const float ClearanceMm = 0.1f;

        /// <summary>Минимальная доля перекрытия граней, при которой контакт несёт
        /// (face-to-face) И при которой срабатывает прилипание. Порог ЕДИНЫЙ:
        /// раньше снэп требовал 30%, а связность — 50%, и в полосе 30–50% деталь
        /// прилипала, но по связности «висела в воздухе» — красная подсветка
        /// сразу после сработавшего снэпа.</summary>
        public const float MinSupportOverlap = 0.3f;

        /// <summary>Минимальная доля перекрытия, при которой снэп срабатывает.
        /// Ниже чем MinSupportOverlap: снэп может притянуть деталь даже при узком
        /// перекрытии (10%), но связность потом подсветит недостаточную опору.
        /// Избегает ситуации «грани вплотную, зазор 0, перекрытие 28% — снэпа нет».</summary>
        public const float MinSnapOverlap = 0.1f;

        public static bool ApproxEqual(float a, float b) =>
            Mathf.Abs(a - b) < EpsilonUnits;

        public static bool IsNoiseMm(float mm) =>
            Mathf.Abs(mm) < ContactMm;

        public static bool IntervalsOverlap(float min1, float max1, float min2, float max2) =>
            IntervalsOverlap(min1, max1, min2, max2, EpsilonUnits);

        /// <summary>Пересечение интервалов с явным запасом (в юнитах). Для проверки
        /// пересечения ТВЁРДЫХ ТЕЛ передавайте ContactMm-в-юнитах: перекрытие мельче
        /// порога контакта — детали стоят вплотную (касание), а не сталкиваются.
        /// Версия без margin (EpsilonUnits) ловит только float-шум footprint-гейтов.</summary>
        public static bool IntervalsOverlap(float min1, float max1, float min2, float max2, float margin) =>
            min1 < max2 - margin && max1 > min2 + margin;

        public static bool IsParallel(float dot) =>
            Mathf.Abs(dot) >= ParallelDot;
    }
}
