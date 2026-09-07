using System.Collections.Generic;
using UnityEngine;

namespace KitchenDesigner.Core
{
    public static class EdgeSubstrate
    {
        public const string DecorName = "Подложка торца";

        private static Material? _plain;

        public static int BareFaceMask(KitchenElement? element,
            IReadOnlyList<KitchenElement>? others)
            => BareFaceMask(element, others == null ? null : SceneFaces.Of(others));

        public static int BareFaceMask(KitchenElement? element, SceneFaces? scene)
        {
            if (element == null || !element.SupportsEdges) return 0;
            var layout = EdgeBanding.LayoutOf(element.DimensionsMM);
            if (!layout.IsValid) return 0;

            if (!element.EdgeBandingEnabled)
                return (1 << layout.FaceIndex(EdgeSide.L1))
                     | (1 << layout.FaceIndex(EdgeSide.L2))
                     | (1 << layout.FaceIndex(EdgeSide.W1))
                     | (1 << layout.FaceIndex(EdgeSide.W2));

            if (scene == null) return 0;

            var coverage = EdgeBanding.Coverage(element, scene);
            int mask = 0;
            foreach (EdgeSide side in EdgeStates.All)
                if (!EdgeBanding.HasEdgeEffective(element, coverage, side))
                    mask |= 1 << layout.FaceIndex(side);
            return mask;
        }

        public static void Sync(KitchenElement? element)
        {
            if (element == null) return;
            element.SetBareFaceMask(BareFaceMask(element, PartRegistry.GetAll()));
        }

        public static void SyncScene(IReadOnlyList<KitchenElement>? all)
        {
            if (all == null) return;
            using var _ = PerfMarkers.EdgeSubstrateSync.Auto();

            var scene = SceneFaces.Of(all);
            foreach (var element in all)
            {
                if (element == null || !element.SupportsEdges) continue;
                element.SetBareFaceMask(BareFaceMask(element, scene));
            }
        }

        public static MaterialDef? Decor()
        {
            foreach (var def in MaterialCatalog.All)
            {
                if (def == null || string.IsNullOrEmpty(def.displayName)) continue;
                if (string.Equals(def.displayName.Trim(), DecorName,
                        System.StringComparison.OrdinalIgnoreCase))
                    return def;
            }
            return null;
        }

        public static Material? Material()
        {
            var def = Decor();
            if (def != null)
            {
                var shared = MaterialManager.GetSharedMaterial(def);
                if (shared != null) return shared;
            }
            return Plain();
        }

        private static Material? Plain()
        {
            if (_plain != null) return _plain;

            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) return null;

            _plain = new Material(shader);
            _plain.SetColor(Shader.PropertyToID("_BaseColor"), Color.white);
            _plain.SetFloat(Shader.PropertyToID("_Smoothness"), 0.05f);
            _plain.color = Color.white;
            return _plain;
        }
    }
}
