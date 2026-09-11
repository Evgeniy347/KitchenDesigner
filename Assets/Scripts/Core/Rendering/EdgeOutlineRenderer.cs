using UnityEngine;

namespace KitchenDesigner.Core
{
    public class EdgeOutlineRenderer : MonoBehaviour
    {
        internal static readonly Color OutlineColor = Color.black;

        internal static readonly int[,] AabbEdgeVertexPairs =
        {
            { 0, 1 }, { 1, 2 }, { 2, 3 }, { 3, 0 },
            { 4, 5 }, { 5, 6 }, { 6, 7 }, { 7, 4 },
            { 0, 4 }, { 1, 5 }, { 2, 6 }, { 3, 7 },
        };

        private Material? _lineMaterial;

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

            var view = ViewResolver.Current;
            if (!view.EdgeOutline && !view.WallOutline) return;

            _lineMaterial.SetPass(0);
            GL.PushMatrix();
            GL.MultMatrix(Matrix4x4.identity);
            GL.Begin(GL.LINES);
            GL.Color(OutlineColor);

            foreach (var e in PartRegistry.All)
            {
                if (!ShouldOutline(e, view)) continue;
                var v = e.GetVertices();
                for (int i = 0; i < AabbEdgeVertexPairs.GetLength(0); i++)
                {
                    GL.Vertex(v[AabbEdgeVertexPairs[i, 0]]);
                    GL.Vertex(v[AabbEdgeVertexPairs[i, 1]]);
                }
            }

            GL.End();
            GL.PopMatrix();
        }

        public static bool ShouldOutline(KitchenElement e, in ViewState view)
        {
            if (e == null) return false;
            if (e.GetComponent<BasePlate>() != null) return false;

            bool isWall = e.GetComponent<Wall>() != null;
            if (!(isWall ? view.WallOutline : view.EdgeOutline)) return false;

            return SceneVisibility.AnyRendererEnabled(e);
        }

        private void OnDestroy()
        {
            DestroyNow.The(_lineMaterial);
        }
    }
}
