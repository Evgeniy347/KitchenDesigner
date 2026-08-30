using UnityEngine;

namespace KitchenDesigner.Core
{
    public static class SceneVisibility
    {
        public static void SetRenderersEnabled(KitchenElement element, bool enabled)
        {
            if (element == null) return;
            var renderers = element.GetComponentsInChildren<MeshRenderer>(true);
            for (int i = 0; i < renderers.Length; i++)
                if (renderers[i] != null) renderers[i].enabled = enabled;
        }

        public static bool AnyRendererEnabled(KitchenElement element)
        {
            if (element == null) return false;
            var renderers = element.GetComponentsInChildren<MeshRenderer>(true);
            if (renderers.Length == 0) return true;
            for (int i = 0; i < renderers.Length; i++)
                if (renderers[i] != null && renderers[i].enabled) return true;
            return false;
        }

        public static bool IsObject(KitchenElement element)
        {
            if (element == null) return false;
            if (element is WindowElement || element is DoorElement) return false;
            if (element is FloorElement) return false;
            if (element.GetComponent<Wall>() != null) return false;
            if (element.GetComponent<BasePlate>() != null) return false;
            return true;
        }

        public static bool ShouldBeVisible(KitchenElement element, in ViewState view)
        {
            if (element is LightSourceElement && view.HideLightSources) return false;
            return view.ObjectsVisible;
        }
    }

    public class SceneVisibilityManager : MonoBehaviour
    {
        private static int _appliedHash = -1;

        public void LateUpdate() => Apply();

        public static void Invalidate() => _appliedHash = -1;

        public static void Apply()
        {
            using var _ = PerfMarkers.SceneVisibilityApply.Auto();
            var view = ViewResolver.Current;
            int hash = (view.ObjectsVisible ? 1 : 0) | (view.HideLightSources ? 2 : 0);
            if (hash == _appliedHash) return;
            _appliedHash = hash;

            foreach (var e in PartRegistry.All)
            {
                if (e == null || !SceneVisibility.IsObject(e)) continue;
                SceneVisibility.SetRenderersEnabled(e, SceneVisibility.ShouldBeVisible(e, view));
            }
        }
    }
}
