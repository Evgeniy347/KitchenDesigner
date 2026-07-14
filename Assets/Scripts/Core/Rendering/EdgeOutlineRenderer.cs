using UnityEngine;

namespace KitchenDesigner.Core
{
    /// <summary>Рисует чёрный контур (12 рёбер AABB) каждого объекта через GL,
    /// если включена настройка EdgeOutline. BasePlate (пол) исключён.</summary>
    public class EdgeOutlineRenderer : MonoBehaviour
    {
        private Material _lineMaterial = null!;

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
            var s = KitchenSettings.Instance;
            if (s == null || !s.EdgeOutline || _lineMaterial == null) return;

            _lineMaterial.SetPass(0);
            GL.PushMatrix();
            GL.MultMatrix(Matrix4x4.identity);
            GL.Begin(GL.LINES);
            GL.Color(Color.black);

            foreach (var e in PartRegistry.GetAll())
            {
                if (e == null || e.GetComponent<BasePlate>() != null) continue;
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

        private void OnDestroy()
        {
            if (_lineMaterial != null) Destroy(_lineMaterial);
        }
    }
}
