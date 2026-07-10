using UnityEngine;

namespace KitchenDesigner.Core
{
    /// <summary>Рисует сетку на полу (y=0) через GL, если включено в настройках.</summary>
    [RequireComponent(typeof(Transform))]
    public class SpatialGridRenderer : MonoBehaviour
    {
        private Material _lineMaterial;

        private const float Extent = 3f;     // ±3 м (размер базовой плиты)
        private const float Step = 0.1f;      // 100 мм
        private const float MajorStep = 1f;   // 1 м — крупные линии

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
            if (s == null || !s.SpatialGrid || _lineMaterial == null) return;

            _lineMaterial.SetPass(0);
            GL.PushMatrix();
            GL.MultMatrix(Matrix4x4.identity);
            GL.Begin(GL.LINES);

            for (float x = -Extent; x <= Extent + 1e-3f; x += Step)
            {
                SetColor(x);
                GL.Vertex3(x, 0f, -Extent);
                GL.Vertex3(x, 0f, Extent);
            }
            for (float z = -Extent; z <= Extent + 1e-3f; z += Step)
            {
                SetColor(z);
                GL.Vertex3(-Extent, 0f, z);
                GL.Vertex3(Extent, 0f, z);
            }

            GL.End();
            GL.PopMatrix();
        }

        private static void SetColor(float coord)
        {
            bool major = Mathf.Abs(coord - Mathf.Round(coord / MajorStep) * MajorStep) < 1e-3f;
            GL.Color(major ? new Color(0.6f, 0.8f, 1f, 0.5f) : new Color(1f, 1f, 1f, 0.18f));
        }

        private void OnDestroy()
        {
            if (_lineMaterial != null) Destroy(_lineMaterial);
        }
    }
}
