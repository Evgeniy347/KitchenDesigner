using UnityEngine;

namespace KitchenDesigner.Core
{
    /// <summary>Габаритная грань детали в МИРОВЫХ координатах: центр, внешняя
    /// нормаль, размер в плоскости и две оси этой плоскости. Это единица, которой
    /// оперируют прилипание, ресайз и проверка кромок.
    ///
    /// Порядок граней в <see cref="KitchenElement.GetFaces"/> — контракт:
    /// index/2 = ось (0=X, 1=Y, 2=Z), чётный индекс = положительное направление.
    /// Любой оверрайд обязан его сохранять: <c>ResizeHandleManager</c> выводит
    /// из индекса ось ресайза.
    ///
    /// Живёт в KitchenDesigner.Geometry, потому что грань — это геометрия, а не
    /// деталь сцены: раньше тип был вложен в KitchenElement и тянул за собой
    /// MonoBehaviour в любую чистую функцию.</summary>
    public struct Face
    {
        public const int BoxFaceCount = 6;

        public Vector3 center;
        public Vector3 normal;
        public Vector2 size;
        public Vector3 rightAxis;
        public Vector3 upAxis;

        public Face(Vector3 center, Vector3 normal, Vector2 size, Vector3 right, Vector3 up)
        {
            this.center = center;
            this.normal = normal;
            this.size = size;
            this.rightAxis = right;
            this.upAxis = up;
        }
    }
}
