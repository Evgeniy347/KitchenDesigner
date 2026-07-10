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
            public readonly string symbol;
            public Variant(bool isDrawer, Vector3 pivotSigns, Vector3 dir, string symbol)
            {
                this.isDrawer = isDrawer; this.pivotSigns = pivotSigns; this.dir = dir; this.symbol = symbol;
            }
        }

        private static readonly Vector3 X = Vector3.right, Y = Vector3.up, Z = Vector3.forward;

        // Оси у рёбер знаковые так, чтобы поворот +90° распахивал их «наружу»
        // (передние — к −Z, задние — к +Z). Порядок строго совпадает с DoorMode.
        private static readonly Variant[] V =
        {
            // Передняя грань (z=−hz).
            new Variant(false, new Vector3(-1f, 0f, -1f),  Y, "◄"), // FrontLeft
            new Variant(false, new Vector3( 1f, 0f, -1f), -Y, "►"), // FrontRight
            new Variant(false, new Vector3( 0f, 1f, -1f),  X, "▲"), // FrontTop
            new Variant(false, new Vector3( 0f,-1f, -1f), -X, "▼"), // FrontBottom
            // Задняя грань (z=+hz) — оси зеркальны передним.
            new Variant(false, new Vector3(-1f, 0f,  1f), -Y, "◁"), // BackLeft
            new Variant(false, new Vector3( 1f, 0f,  1f),  Y, "▷"), // BackRight
            new Variant(false, new Vector3( 0f, 1f,  1f), -X, "△"), // BackTop
            new Variant(false, new Vector3( 0f,-1f,  1f),  X, "▽"), // BackBottom
            // Рёбра по толщине (ось Z) в 4 углах.
            new Variant(false, new Vector3(-1f, 1f,  0f),  Z, "◤"), // EdgeTopLeft
            new Variant(false, new Vector3( 1f, 1f,  0f),  Z, "◥"), // EdgeTopRight
            new Variant(false, new Vector3(-1f,-1f,  0f),  Z, "◣"), // EdgeBottomLeft
            new Variant(false, new Vector3( 1f,-1f,  0f),  Z, "◢"), // EdgeBottomRight
            // Ящик — сдвиг по нормали грани.
            new Variant(true, Vector3.zero, -Z, "⊙"), // Out  (−Z, к зрителю)
            new Variant(true, Vector3.zero,  Z, "⊗"), // In   (+Z)
            new Variant(true, Vector3.zero,  X, "→"), // Right (+X)
            new Variant(true, Vector3.zero, -X, "←"), // Left  (−X)
            new Variant(true, Vector3.zero,  Y, "↑"), // Up    (+Y)
            new Variant(true, Vector3.zero, -Y, "↓"), // Down  (−Y)
        };

        /// <summary>Число режимов (12 рёбер + 6 ящиков = 18).</summary>
        public static int Count => V.Length;

        /// <summary>Следующий режим по кругу (для кнопки-переключателя).</summary>
        public static DoorMode Next(DoorMode mode) => (DoorMode)(((int)mode + 1) % V.Length);

        /// <summary>Один символ для кнопки-переключателя, зависящий от режима.</summary>
        public static string Symbol(DoorMode mode) => V[(int)mode].symbol;

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
            float angle = MaxAngleDeg * e; // +90° вокруг знаковой оси ребра
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
