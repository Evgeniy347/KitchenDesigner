using System;
using KitchenDesigner.Core.Lighting;
using KitchenDesigner.Core.Measure;
using KitchenDesigner.Core.Tools;

namespace KitchenDesigner.Core
{
    public static class PhotoMode
    {
        public static bool Active => EditModeManager.Mode == EditMode.Photo;

        public static event Action? Changed;

        private static bool _prevTintEnabled;

        public static bool ResolveTransparent(bool elementTransparent) =>
            ResolveTransparent(elementTransparent, Active);

        public static bool ResolveTransparent(bool elementTransparent, bool photoActive) =>
            elementTransparent && !photoActive;

        public static void Toggle() => SetActive(!Active);

        public static void SetActive(bool on)
        {
            if (on == Active) return;
            EditModeManager.SetMode(on ? EditMode.Photo : EditMode.Normal);
        }

        internal static void Enter()
        {
            CloseToolsThatDrawOverlays();
            SelectionManager.Instance?.DeselectAll();
            _prevTintEnabled = ElementHighlighter.TintEnabled;
            ElementHighlighter.TintEnabled = false;
            RefreshHighlights();
            ApplySceneOverFinalMaterials();
        }

        private static void CloseToolsThatDrawOverlays()
        {
            MeasureMode.SetActive(false);
            LightPickMode.SetSource(null);
            EyedropperMode.SetActive(false);
            TextureOverlayHandles.End();
            SideHighlighter.Hide();
        }

        internal static void Exit()
        {
            PhotoQualityController.Restore();
            PhotoShadowCasters.Restore();
            CeilingBuilder.Clear();
            ElementHighlighter.TintEnabled = _prevTintEnabled;
            RefreshHighlights();
        }

        internal static void RaiseChanged() => Changed?.Invoke();

        private static void RefreshHighlights()
        {
            if (ElementHighlighter.Instance != null)
                ElementHighlighter.Instance.RefreshHighlights();
        }

        public static void RefreshIfActive()
        {
            if (!Active) return;
            ApplySceneOverFinalMaterials();
        }

        private static void ApplySceneOverFinalMaterials()
        {
            RebuildCeiling();
            PhotoShadowCasters.Enable();
            PhotoQualityController.Apply();
        }

        private static void RebuildCeiling()
        {
            var s = KitchenSettings.Instance;
            if (s != null && s.PhotoCeiling) CeilingBuilder.Rebuild();
            else CeilingBuilder.Clear();
        }
    }
}
