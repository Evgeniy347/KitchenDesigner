using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace KitchenDesigner.Core
{
    public static class PhotoShadowCasters
    {
        private static readonly List<(MeshRenderer renderer, ShadowCastingMode prev)> _changed = new();

        public static void Enable()
        {
            Restore();
            foreach (var r in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
            {
                if (r == null) continue;
                var target = ShadowModeFor(r.sharedMaterial);
                if (r.shadowCastingMode == target) continue;
                _changed.Add((r, r.shadowCastingMode));
                r.shadowCastingMode = target;
            }
        }

        public static void Restore()
        {
            foreach (var (r, prev) in _changed)
                if (r != null) r.shadowCastingMode = prev;
            _changed.Clear();
        }

        public static ShadowCastingMode ShadowModeFor(Material? material) =>
            IsTransparent(material) ? ShadowCastingMode.Off : ShadowCastingMode.On;

        public static bool IsTransparent(Material? m) =>
            m != null && m.renderQueue >= (int)RenderQueue.Transparent;
    }
}
