using UnityEngine;

namespace KitchenDesigner.Core
{
    /// <summary>Рисует чёрный контур (12 рёбер AABB) каждого объекта через GL.
    /// Контур стен и контур прочих объектов — независимые настройки
    /// (WallOutline и EdgeOutline). BasePlate (пол) исключён, скрытые объекты
    /// тоже: обводка у невидимой детали выглядела бы «призраком».</summary>
    public class EdgeOutlineRenderer : MonoBehaviour
    {
        private Material? _lineMaterial;

        // Рёбра AABB по индексам вершин из KitchenElement.GetVertices().
        private static readonly int[,] Edges =
        {
            { 0, 1 }, { 1, 2 }, { 2, 3 }, { 3, 0 }, // нижняя грань
            { 4, 5 }, { 5, 6 }, { 6, 7 }, { 7, 4 }, // верхняя грань
            { 0, 4 }, { 1, 5 }, { 2, 6 }, { 3, 7 }, // вертикальные рёбра
        };

        private void Awake()
        {
            var shader = Shader.Find("Hidden/GridLine");
            if (shader == null)
                shader = Resources.Load<Shader>("Shaders/GridLine");
            if (shader == null) return;
            _lineMaterial = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
        }

        private void OnRenderObject()
        {
            if (_lineMaterial == null) return;
            // Контур — единственная настройка вида, которую фоторежим не форсирует:
            // это не сокрытие геометрии, и нужен он в кадре или нет — решает
            // пользователь заранее, в обычном режиме.
            var view = ViewResolver.Current;
            if (!view.EdgeOutline && !view.WallOutline) return;

            _lineMaterial.SetPass(0);
            GL.PushMatrix();
            GL.MultMatrix(Matrix4x4.identity);
            GL.Begin(GL.LINES);
            GL.Color(Color.black);

            foreach (var e in PartRegistry.All)
            {
                if (e == null || e.GetComponent<BasePlate>() != null) continue;
                if (!ShouldOutline(e, view)) continue;
                var v = e.GetVertices();
                for (int i = 0; i < Edges.GetLength(0); i++)
                {
                    GL.Vertex(v[Edges[i, 0]]);
                    GL.Vertex(v[Edges[i, 1]]);
                }
            }

            GL.End();
            GL.PopMatrix();
        }

        /// <summary>Стена берёт свою настройку контура, остальное — общую.
        /// Погашенный рендер (скрытые стены, скрытые объекты, окна опущенной
        /// стены) контура не получает.</summary>
        public static bool ShouldOutline(KitchenElement e, in ViewState view)
        {
            if (e == null) return false;
            bool isWall = e.GetComponent<Wall>() != null;
            if (!(isWall ? view.WallOutline : view.EdgeOutline)) return false;

            return SceneVisibility.AnyRendererEnabled(e);
        }

        private void OnDestroy()
        {
            if (_lineMaterial != null) Destroy(_lineMaterial);
        }
    }
}
