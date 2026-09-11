using System;
using UnityEngine;
using KitchenDesigner.Core.Measure;

namespace KitchenDesigner.Core.Lighting
{
    public class LightPickRenderer : MonoBehaviour
    {
        internal const float LineThicknessPx = 2.5f;
        internal const float EndpointRadiusPx = 5f;

        internal const string OverlayLineShaderMissing =
            "[LightPick] Шейдер Hidden/OverlayLine не найден — связи выключателя не будут видны.";

        private Material? _lineMaterial;

        private void Awake() => _lineMaterial = BuildLineMaterialOrWarn(
            OverlayLineMaterial.FindShader(), message => Debug.LogWarning(message));

        internal static Material? BuildLineMaterialOrWarn(Shader? shader, Action<string> warn)
            => OverlayLineMaterial.BuildOrWarn(shader, warn, OverlayLineShaderMissing);

        private void OnDestroy()
        {
            DestroyNow.The(_lineMaterial);
        }

        private void OnRenderObject()
        {
            if (!LightPickMode.Active || _lineMaterial == null) return;
            var cam = MeasureRenderer.ResolveCamera(Camera.current, Camera.main);
            if (cam == null) return;

            _lineMaterial.SetPass(0);
            GL.PushMatrix();
            GL.MultMatrix(Matrix4x4.identity);
            GL.Begin(GL.QUADS);

            DrawExistingLinks(cam);
            DrawPreview(cam);

            GL.End();
            GL.PopMatrix();
        }

        private static void DrawExistingLinks(Camera cam)
        {
            foreach (var link in LightPickController.VisibleLinks())
            {
                GL.Color(link.BelongsToTheSwitchBeingEdited
                    ? LightLinkPalette.Line
                    : LightLinkPalette.Existing);
                DashedLineDrawer.Dashed(cam, link.From, link.To, LineThicknessPx);
                DashedLineDrawer.Point(cam, link.From, EndpointRadiusPx);
                DashedLineDrawer.Point(cam, link.To, EndpointRadiusPx);
            }
        }

        private static void DrawPreview(Camera cam)
        {
            var ctrl = LightPickController.Instance;
            if (ctrl == null || !ctrl.HasPreview) return;

            GL.Color(ctrl.HoveredLight != null ? LightLinkPalette.Hover : LightLinkPalette.Line);
            DashedLineDrawer.Dashed(cam, ctrl.Anchor!.Value, ctrl.CursorPoint!.Value,
                LineThicknessPx);
            DashedLineDrawer.Point(cam, ctrl.Anchor!.Value, EndpointRadiusPx);
            DashedLineDrawer.Point(cam, ctrl.CursorPoint!.Value, EndpointRadiusPx);
        }
    }
}
