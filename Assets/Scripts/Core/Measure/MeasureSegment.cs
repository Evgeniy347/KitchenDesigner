using UnityEngine;

namespace KitchenDesigner.Core.Measure
{
    /// <summary>Один зафиксированный замер: отрезок между двумя точками сцены
    /// (мировые юниты). Временная разметка — в проект не сохраняется.</summary>
    public class MeasureSegment
    {
        public Vector3 A { get; }
        public Vector3 B { get; }

        public MeasureSegment(Vector3 a, Vector3 b)
        {
            A = a;
            B = b;
        }

        /// <summary>Длина в миллиметрах (для подписи и окна свойств).</summary>
        public float LengthMm => MeasureGeometry.ToMm((B - A).magnitude);

        /// <summary>Ось, вдоль которой лежит отрезок: 0=X, 1=Y, 2=Z, −1 — диагональ.</summary>
        public int Axis => MeasureGeometry.AxisOf(A, B);

        public Vector3 Middle => (A + B) * 0.5f;
    }
}
