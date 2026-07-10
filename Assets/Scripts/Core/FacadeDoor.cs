using UnityEngine;

namespace KitchenDesigner.Core
{
    /// <summary>Режим открывания фасада: поворот вокруг одного из 4 рёбер либо
    /// выдвижение вперёд как ящик. Порядок = порядок цикла кнопкой-переключателем.</summary>
    public enum DoorMode { Left, Right, Top, Bottom, Drawer }

    /// <summary>
    /// Чистая математика открывания фасада (без Unity-состояния — покрывается
    /// юнит-тестами). Рёбра: дверца поворачивается вокруг выбранного ребра закрытой
    /// позы (само ребро стоит на месте, центр едет по дуге). Ящик: фасад выдвигается
    /// вперёд по нормали. Скорость — по синусу (плавный старт и плавное торможение).
    /// </summary>
    public static class FacadeDoor
    {
        /// <summary>Угол полностью открытой дверцы (градусы).</summary>
        public const float MaxAngleDeg = 90f;

        /// <summary>Ход полностью выдвинутого ящика (метры).</summary>
        public const float DrawerSlideMeters = 0.4f;

        private const int ModeCount = 5;

        /// <summary>Следующий режим по кругу (для кнопки-переключателя).</summary>
        public static DoorMode Next(DoorMode mode) => (DoorMode)(((int)mode + 1) % ModeCount);

        /// <summary>Один символ для кнопки-переключателя, зависящий от режима.</summary>
        public static string Symbol(DoorMode mode)
        {
            switch (mode)
            {
                case DoorMode.Left: return "◄";
                case DoorMode.Right: return "►";
                case DoorMode.Top: return "▲";
                case DoorMode.Bottom: return "▼";
                default: return "▣"; // Drawer (ящик)
            }
        }

        /// <summary>Плавность «синус» (ease-in-out): 0→0, 1→1, плавный разгон и торможение.</summary>
        public static float Ease(float t)
        {
            t = Mathf.Clamp01(t);
            return 0.5f * (1f - Mathf.Cos(Mathf.PI * t));
        }

        /// <summary>Локальные точка и ось петли для режима-ребра + знак поворота.
        /// Для режима «ящик» возвращает false (петли нет). Знаки подобраны так,
        /// чтобы все рёбра открывались в одну сторону (наружу, к −Z).</summary>
        public static bool Hinge(DoorMode mode, Vector3 halfExtents,
            out Vector3 pivotLocal, out Vector3 axisLocal, out float sign)
        {
            switch (mode)
            {
                case DoorMode.Left:
                    pivotLocal = new Vector3(-halfExtents.x, 0f, 0f); axisLocal = Vector3.up; sign = +1f; return true;
                case DoorMode.Right:
                    pivotLocal = new Vector3(+halfExtents.x, 0f, 0f); axisLocal = Vector3.up; sign = -1f; return true;
                case DoorMode.Top:
                    pivotLocal = new Vector3(0f, +halfExtents.y, 0f); axisLocal = Vector3.right; sign = +1f; return true;
                case DoorMode.Bottom:
                    pivotLocal = new Vector3(0f, -halfExtents.y, 0f); axisLocal = Vector3.right; sign = -1f; return true;
                default: // Drawer
                    pivotLocal = Vector3.zero; axisLocal = Vector3.forward; sign = 0f; return false;
            }
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
            float e = Ease(progress);

            // Ящик: без поворота, сдвиг вперёд по нормали (−Z, наружу).
            if (!Hinge(mode, halfExtents, out var pivotLocal, out var axisLocal, out var sign))
            {
                rot = closedRot;
                pos = closedPos + closedRot * (Vector3.back * (DrawerSlideMeters * e));
                return;
            }

            // Ребро: поворот вокруг ребра закрытой позы.
            float angle = sign * MaxAngleDeg * e;
            var pivotWorld = closedPos + closedRot * pivotLocal;
            var axisWorld = closedRot * axisLocal;
            var delta = Quaternion.AngleAxis(angle, axisWorld);

            rot = delta * closedRot;
            pos = pivotWorld + delta * (closedPos - pivotWorld);
        }
    }
}
