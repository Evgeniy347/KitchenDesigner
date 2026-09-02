using System;
using UnityEngine;
using KitchenDesigner.Core.UI;

namespace KitchenDesigner.Core.Measure
{
    public class MeasureRenderer : MonoBehaviour
    {
        internal const float LineThicknessPx = 2.5f;
        private const float PointRadiusPx = 5f;
        private const float TubeRadiusPx = 10f;
        private const int TubeSideCount = 12;

        internal const string OverlayLineShaderMissing =
            "[Measure] Шейдер Hidden/OverlayLine не найден — разметка рулетки не будет видна.";

        private Material? _lineMaterial;

        private void Awake() => _lineMaterial = BuildLineMaterialOrWarn(
            OverlayLineMaterial.FindShader(), message => Debug.LogWarning(message));

        internal static Material? BuildLineMaterialOrWarn(Shader? shader, Action<string> warn)
            => OverlayLineMaterial.BuildOrWarn(shader, warn, OverlayLineShaderMissing);

        public static Camera? ResolveCamera(Camera? current, Camera? main) =>
            current != null ? current : main;

        private void OnDestroy()
        {
            if (_lineMaterial != null) Destroy(_lineMaterial);
        }

        private void OnRenderObject()
        {
            if (!MeasureMode.Active || _lineMaterial == null) return;
            var cam = ResolveCamera(Camera.current, Camera.main);
            if (cam == null) return;

            _lineMaterial.SetPass(0);
            GL.PushMatrix();
            GL.MultMatrix(Matrix4x4.identity);

            foreach (var pass in DrawOrder) Draw(pass, cam);

            GL.PopMatrix();
        }

        internal enum DrawPass
        {
            SelectionTube,
            DashedLines,
            Points,
        }

        internal static readonly DrawPass[] DrawOrder =
        {
            DrawPass.SelectionTube,
            DrawPass.DashedLines,
            DrawPass.Points,
        };

        private void Draw(DrawPass pass, Camera cam)
        {
            switch (pass)
            {
                case DrawPass.SelectionTube: DrawSelectionTube(cam); break;
                case DrawPass.DashedLines: DrawLines(cam); break;
                case DrawPass.Points: DrawPoints(cam); break;
            }
        }

        private void DrawSelectionTube(Camera cam)
        {
            var selected = MeasureStore.Selected;
            if (selected == null) return;

            GL.Begin(GL.TRIANGLES);
            GL.Color(UIStyle.MeasureSelected);
            DrawTube(cam, selected.A, selected.B);
            GL.End();
        }

        private void DrawLines(Camera cam)
        {
            var ctrl = MeasureController.Instance;

            GL.Begin(GL.QUADS);
            foreach (var seg in MeasureStore.Segments)
            {
                bool accent = ctrl != null && ctrl.Hovered == seg;
                GL.Color(accent ? UIStyle.MeasureHover : UIStyle.MeasureLine);
                DashedLineDrawer.Dashed(cam, seg.A, seg.B, LineThicknessPx);
            }

            if (ctrl != null && ctrl.HasPreview)
            {
                GL.Color(UIStyle.MeasureLine);
                DashedLineDrawer.Dashed(cam, ctrl.Anchor!.Value, ctrl.PreviewEnd!.Value,
                    LineThicknessPx);
            }
            GL.End();
        }

        private void DrawPoints(Camera cam)
        {
            var ctrl = MeasureController.Instance;

            GL.Begin(GL.QUADS);
            foreach (var seg in MeasureStore.Segments)
            {
                GL.Color(UIStyle.MeasureLine);
                DashedLineDrawer.Point(cam, seg.A, PointRadiusPx);
                DashedLineDrawer.Point(cam, seg.B, PointRadiusPx);
            }

            if (ctrl != null)
            {
                if (ctrl.Anchor.HasValue)
                {
                    GL.Color(UIStyle.MeasureLine);
                    DashedLineDrawer.Point(cam, ctrl.Anchor.Value, PointRadiusPx);
                }
                if (ctrl.Hint.HasValue) DrawInterchangeableHintPoint(cam, ctrl.Hint.Value);
                if (ctrl.PlaneHint.HasValue) DrawInterchangeableHintPoint(cam, ctrl.PlaneHint.Value);
            }
            GL.End();
        }

        private static void DrawInterchangeableHintPoint(Camera cam, Vector3 point)
        {
            GL.Color(UIStyle.MeasureHint);
            DashedLineDrawer.Point(cam, point, PointRadiusPx);
        }

        private static void DrawTube(Camera cam, Vector3 a, Vector3 b)
        {
            Vector3 axis = b - a;
            if (axis.sqrMagnitude < Tolerance.EpsilonSqr) return;
            axis.Normalize();

            Vector3 refUp = Mathf.Abs(Vector3.Dot(axis, Vector3.up)) > Tolerance.UpDotThreshold
                ? Vector3.forward : Vector3.up;
            Vector3 u = Vector3.Cross(axis, refUp).normalized;
            Vector3 v = Vector3.Cross(axis, u);

            float ra = MeasureGeometry.WorldSizeForPixels(cam, a, TubeRadiusPx);
            float rb = MeasureGeometry.WorldSizeForPixels(cam, b, TubeRadiusPx);

            for (int i = 0; i < TubeSideCount; i++)
            {
                float a0 = (float)i / TubeSideCount * Mathf.PI * 2f;
                float a1 = (float)(i + 1) / TubeSideCount * Mathf.PI * 2f;
                Vector3 d0 = u * Mathf.Cos(a0) + v * Mathf.Sin(a0);
                Vector3 d1 = u * Mathf.Cos(a1) + v * Mathf.Sin(a1);

                Vector3 p00 = a + d0 * ra, p01 = a + d1 * ra;
                Vector3 p10 = b + d0 * rb, p11 = b + d1 * rb;

                GL.Vertex(p00); GL.Vertex(p10); GL.Vertex(p11);
                GL.Vertex(p00); GL.Vertex(p11); GL.Vertex(p01);
            }
        }
    }
}
