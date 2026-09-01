using System.Collections.Generic;
using UnityEngine;

namespace KitchenDesigner.Core
{
    public static class CeilingBuilder
    {
        private const string CeilingName = "PhotoCeiling";
        private static readonly Color CeilingColor = new Color(0.93f, 0.93f, 0.95f);
        private static GameObject? _ceiling;

        public static bool Exists => _ceiling != null;

        internal static GameObject? Slab => _ceiling;

        public static void Rebuild()
        {
            Clear();

            if (!CeilingGeometry.TryCompute(CollectWallBoundsAtFullHeight(), out var ceil))
                return;

            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = CeilingName;
            DropColliderSoClicksPassThrough(go);

            go.transform.position = ceil.center;
            go.transform.localScale = ceil.size;
            PaintAsShadowCastingSlab(go.GetComponent<MeshRenderer>());

            _ceiling = go;
        }

        public static void Clear()
        {
            if (_ceiling == null) return;
            DestroyNow(_ceiling);
            _ceiling = null;
        }

        private static void DropColliderSoClicksPassThrough(GameObject go)
        {
            var col = go.GetComponent<Collider>();
            if (col != null) DestroyNow(col);
        }

        private static void PaintAsShadowCastingSlab(MeshRenderer renderer)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader != null)
            {
                var mat = new Material(shader);
                mat.SetColor("_BaseColor", CeilingColor);
                renderer.sharedMaterial = mat;
            }
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            renderer.receiveShadows = true;
        }

        private static void DestroyNow(Object target)
        {
            if (Application.isPlaying) Object.Destroy(target);
            else Object.DestroyImmediate(target);
        }

        private static List<Bounds> CollectWallBoundsAtFullHeight()
        {
            var result = new List<Bounds>();
            foreach (var e in PartRegistry.GetAll())
            {
                if (e == null) continue;
                var wall = e.GetComponent<Wall>();
                if (wall == null) continue;

                wall.RestoreFull();
                var renderer = e.GetComponent<MeshRenderer>();
                if (renderer == null) continue;
                result.Add(renderer.bounds);
            }
            return result;
        }
    }
}
