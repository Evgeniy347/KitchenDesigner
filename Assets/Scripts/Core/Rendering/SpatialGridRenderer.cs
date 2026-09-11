using UnityEngine;

namespace KitchenDesigner.Core
{
    [RequireComponent(typeof(Transform))]
    public class SpatialGridRenderer : MonoBehaviour
    {
        private Material? _lineMaterial;

        public const float HalfExtentUnits = 3f;
        public const float StepMM = 100f;
        public const float MajorStepMM = 1000f;

        private const float StepUnits = StepMM * AppConstants.MM_TO_UNITS;
        private const float MajorStepUnits = MajorStepMM * AppConstants.MM_TO_UNITS;
        private const float LastLineInclusionUnits = 1e-3f;

        private static readonly Color MajorLineColor = new Color(0.6f, 0.8f, 1f, 0.5f);
        private static readonly Color MinorLineColor = new Color(1f, 1f, 1f, 0.18f);

        private const float FloorY = 0f;

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

            for (float x = -HalfExtentUnits; x <= HalfExtentUnits + LastLineInclusionUnits; x += StepUnits)
            {
                SetColor(x);
                GL.Vertex3(x, FloorY, -HalfExtentUnits);
                GL.Vertex3(x, FloorY, HalfExtentUnits);
            }
            for (float z = -HalfExtentUnits; z <= HalfExtentUnits + LastLineInclusionUnits; z += StepUnits)
            {
                SetColor(z);
                GL.Vertex3(-HalfExtentUnits, FloorY, z);
                GL.Vertex3(HalfExtentUnits, FloorY, z);
            }

            GL.End();
            GL.PopMatrix();
        }

        public static bool IsMajorLine(float coordUnits) =>
            Mathf.Abs(coordUnits - Mathf.Round(coordUnits / MajorStepUnits) * MajorStepUnits) < 1e-3f;

        private static void SetColor(float coord) =>
            GL.Color(IsMajorLine(coord) ? MajorLineColor : MinorLineColor);

        private void OnDestroy()
        {
            DestroyNow.The(_lineMaterial);
        }
    }
}
