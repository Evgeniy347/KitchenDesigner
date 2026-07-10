using UnityEngine;

namespace KitchenDesigner.Core
{
    /// <summary>Ребро-петля, вокруг которого фасад-дверца поворачивается при открытии.</summary>
    public enum HingeEdge { Left, Right, Top, Bottom }

    /// <summary>
    /// Чистая математика открывания фасада-дверцы (без Unity-состояния — покрывается
    /// юнит-тестами). Дверца поворачивается вокруг выбранного РЕБРА закрытой позы:
    /// само ребро остаётся на месте, а центр едет по дуге. Скорость — по синусу
    /// (плавный старт и плавное торможение).
    /// </summary>
    public static class FacadeDoor
    {
        /// <summary>Угол полностью открытой дверцы (градусы).</summary>
        public const float MaxAngleDeg = 90f;

        /// <summary>Плавность «синус» (ease-in-out): 0→0, 1→1, плавный разгон и торможение.</summary>
        public static float Ease(float t)
        {
            t = Mathf.Clamp01(t);
            return 0.5f * (1f - Mathf.Cos(Mathf.PI * t));
        }

        /// <summary>Локальные точка и ось петли для ребра + знак поворота.
        /// Знаки подобраны так, чтобы все рёбра открывались в одну сторону (наружу).</summary>
        public static void Hinge(HingeEdge edge, Vector3 halfExtents,
            out Vector3 pivotLocal, out Vector3 axisLocal, out float sign)
        {
            switch (edge)
            {
                case HingeEdge.Left:
                    pivotLocal = new Vector3(-halfExtents.x, 0f, 0f); axisLocal = Vector3.up; sign = +1f; break;
                case HingeEdge.Right:
                    pivotLocal = new Vector3(+halfExtents.x, 0f, 0f); axisLocal = Vector3.up; sign = -1f; break;
                case HingeEdge.Top:
                    pivotLocal = new Vector3(0f, +halfExtents.y, 0f); axisLocal = Vector3.right; sign = +1f; break;
                default: // HingeEdge.Bottom
                    pivotLocal = new Vector3(0f, -halfExtents.y, 0f); axisLocal = Vector3.right; sign = -1f; break;
            }
        }

        /// <summary>
        /// Поза дверцы (позиция центра + поворот) для прогресса <paramref name="progress"/> ∈ [0..1].
        /// progress=0 — закрыто (равно закрытой позе), progress=1 — открыто на 90°.
        /// Плавность применяется внутри (по синусу).
        /// </summary>
        /// <param name="halfExtents">Половины ФИЗИЧЕСКИХ размеров дверцы (localScale/2).</param>
        public static void Pose(
            Vector3 closedPos, Quaternion closedRot, Vector3 halfExtents,
            HingeEdge edge, float progress,
            out Vector3 pos, out Quaternion rot)
        {
            Hinge(edge, halfExtents, out var pivotLocal, out var axisLocal, out var sign);

            float angle = sign * MaxAngleDeg * Ease(progress);
            var pivotWorld = closedPos + closedRot * pivotLocal;
            var axisWorld = closedRot * axisLocal;
            var delta = Quaternion.AngleAxis(angle, axisWorld);

            rot = delta * closedRot;
            pos = pivotWorld + delta * (closedPos - pivotWorld);
        }
    }
}
