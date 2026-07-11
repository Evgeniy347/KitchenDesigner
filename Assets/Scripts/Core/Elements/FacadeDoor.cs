using UnityEngine;

namespace KitchenDesigner.Core
{
    /// <summary>
    /// Режим открывания фасада: поворот вокруг одного из 12 рёбер бокса, либо
    /// выдвижение ящиком по нормали одной из 6 граней. Порядок = порядок цикла
    /// кнопкой-переключателем. Значение 0 (HingeFrontLeft) — режим по умолчанию.
    /// </summary>
    public enum DoorMode
    {
        // 4 передних ребра (грань z=−hz) — естественные двери, распахиваются наружу.
        HingeFrontLeft, HingeFrontRight, HingeFrontTop, HingeFrontBottom,
        // 4 задних ребра (грань z=+hz).
        HingeBackLeft, HingeBackRight, HingeBackTop, HingeBackBottom,
        // 4 ребра по толщине (ось Z) в углах фасада.
        HingeEdgeTopLeft, HingeEdgeTopRight, HingeEdgeBottomLeft, HingeEdgeBottomRight,
        // 6 направлений ящика (по нормали каждой грани).
        DrawerOut, DrawerIn, DrawerRight, DrawerLeft, DrawerUp, DrawerDown
    }

    /// <summary>
    /// Чистая математика открывания фасада (без Unity-состояния — покрывается
    /// юнит-тестами). Ребро: поворот вокруг ребра закрытой позы (само ребро стоит
    /// на месте, центр едет по дуге). Ящик: сдвиг по нормали грани. Скорость — по
    /// синусу (плавный старт и плавное торможение). 18 режимов заданы таблицей.
    /// </summary>
    public static class FacadeDoor
    {
        /// <summary>Угол полностью открытой дверцы (градусы).</summary>
        public const float MaxAngleDeg = 90f;

        /// <summary>Ход полностью выдвинутого ящика (метры).</summary>
        public const float DrawerSlideMeters = 0.4f;

        private readonly struct Variant
        {
            public readonly bool isDrawer;
            public readonly Vector3 pivotSigns; // ребро: pivot = Scale(pivotSigns, half)
            public readonly Vector3 dir;        // ребро: знаковая ось; ящик: направление сдвига (локально)
            public readonly string symbol;      // один знак для компактной подписи
            public readonly string name;        // читаемое название (для выпадающего списка)
            public Variant(bool isDrawer, Vector3 pivotSigns, Vector3 dir, string symbol, string name)
            {
                this.isDrawer = isDrawer; this.pivotSigns = pivotSigns; this.dir = dir;
                this.symbol = symbol; this.name = name;
            }
        }

        private static readonly Vector3 X = Vector3.right, Y = Vector3.up, Z = Vector3.forward;

        // Оси у рёбер знаковые так, чтобы поворот +90° распахивал их «наружу»
        // (передние — к −Z, задние — к +Z). Порядок строго совпадает с DoorMode.
        private static readonly Variant[] V =
        {
            // Передняя грань (z=−hz). Распахиваются наружу — к +Z.
            new Variant(false, new Vector3(-1f, 0f, -1f),  Y, "◄", "Дверь: слева"),
            new Variant(false, new Vector3( 1f, 0f, -1f), -Y, "►", "Дверь: справа"),
            new Variant(false, new Vector3( 0f, 1f, -1f),  X, "▲", "Дверь: сверху"),
            new Variant(false, new Vector3( 0f,-1f, -1f), -X, "▼", "Дверь: снизу"),
            // Задняя грань (z=+hz) — оси зеркальны передним.
            new Variant(false, new Vector3(-1f, 0f,  1f), -Y, "◁", "Сзади: слева"),
            new Variant(false, new Vector3( 1f, 0f,  1f),  Y, "▷", "Сзади: справа"),
            new Variant(false, new Vector3( 0f, 1f,  1f), -X, "△", "Сзади: сверху"),
            new Variant(false, new Vector3( 0f,-1f,  1f),  X, "▽", "Сзади: снизу"),
            // Рёбра по толщине (ось Z) в 4 углах.
            new Variant(false, new Vector3(-1f, 1f,  0f),  Z, "◤", "Угол: верх-лево"),
            new Variant(false, new Vector3( 1f, 1f,  0f),  Z, "◥", "Угол: верх-право"),
            new Variant(false, new Vector3(-1f,-1f,  0f),  Z, "◣", "Угол: низ-лево"),
            new Variant(false, new Vector3( 1f,-1f,  0f),  Z, "◢", "Угол: низ-право"),
            // Ящик — сдвиг по нормали грани. "Вперёд" = наружу = +Z.
            new Variant(true, Vector3.zero,  Z, "⊙", "Ящик: вперёд"),
            new Variant(true, Vector3.zero, -Z, "⊗", "Ящик: назад"),
            new Variant(true, Vector3.zero, -X, "→", "Ящик: вправо"),
            new Variant(true, Vector3.zero,  X, "←", "Ящик: влево"),
            new Variant(true, Vector3.zero, -Y, "↑", "Ящик: вверх"),
            new Variant(true, Vector3.zero,  Y, "↓", "Ящик: вниз"),
        };

        /// <summary>Число режимов (12 рёбер + 6 ящиков = 18).</summary>
        public static int Count => V.Length;

        /// <summary>Следующий режим по кругу (для кнопки-переключателя).</summary>
        public static DoorMode Next(DoorMode mode) => (DoorMode)(((int)mode + 1) % V.Length);

        /// <summary>Один символ, зависящий от режима (компактная подпись).</summary>
        public static string Symbol(DoorMode mode) => V[(int)mode].symbol;

        /// <summary>Читаемая подпись для выпадающего списка: символ + название.</summary>
        public static string Label(DoorMode mode) => V[(int)mode].symbol + " " + V[(int)mode].name;

        /// <summary>Плавность «синус» (ease-in-out): 0→0, 1→1, плавный разгон и торможение.</summary>
        public static float Ease(float t)
        {
            t = Mathf.Clamp01(t);
            return 0.5f * (1f - Mathf.Cos(Mathf.PI * t));
        }

        /// <summary>
        /// Поза фасада для прогресса <paramref name="progress"/> ∈ [0..1]:
        /// 0 — закрыто (равно закрытой позе), 1 — открыто (поворот 90° либо выдвижение).
        /// Плавность (синус) применяется внутри.
        /// </summary>
        /// <param name="halfExtents">Половины ФИЗИЧЕСКИХ размеров фасада (localScale/2).</param>
        public static void Pose(
            Vector3 closedPos, Quaternion closedRot, Vector3 halfExtents,
            DoorMode mode, float progress,
            out Vector3 pos, out Quaternion rot)
        {
            var v = V[(int)mode];
            float e = Ease(progress);

            if (v.isDrawer)
            {
                rot = closedRot;
                pos = closedPos + closedRot * (v.dir * (DrawerSlideMeters * e));
                return;
            }

            var pivotLocal = Vector3.Scale(v.pivotSigns, halfExtents);
            // −90° вокруг знаковой оси ребра — двери распахиваются наружу (+Z для передней грани).
            float angle = -MaxAngleDeg * e;
            var pivotWorld = closedPos + closedRot * pivotLocal;
            var axisWorld = closedRot * v.dir;
            var delta = Quaternion.AngleAxis(angle, axisWorld);

            rot = delta * closedRot;
            pos = pivotWorld + delta * (closedPos - pivotWorld);
        }

        /// <summary>Точка и ось петли режима-ребра (для тестов). Для ящика — false.</summary>
        public static bool Hinge(DoorMode mode, Vector3 halfExtents,
            out Vector3 pivotLocal, out Vector3 axisLocal)
        {
            var v = V[(int)mode];
            axisLocal = v.dir;
            if (v.isDrawer) { pivotLocal = Vector3.zero; return false; }
            pivotLocal = Vector3.Scale(v.pivotSigns, halfExtents);
            return true;
        }
    }
}
