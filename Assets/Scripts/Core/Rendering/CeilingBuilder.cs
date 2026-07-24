using System.Collections.Generic;
using UnityEngine;

namespace KitchenDesigner.Core
{
    /// <summary>Строит и убирает временную плиту потолка в фоторежиме. Потолок —
    /// чистая декорация: не регистрируется в PartRegistry и без коллайдера (не ловит
    /// клики/рейкасты). Отбрасывает тень — перекрывает солнце сверху, поэтому комната
    /// освещается только через окна, открытые двери и собственные светильники.</summary>
    public static class CeilingBuilder
    {
        private const string CeilingName = "PhotoCeiling";
        private static GameObject? _ceiling;

        public static bool Exists => _ceiling != null;

        /// <summary>Пересобрать потолок по текущим стенам. Если стен нет —
        /// потолок убирается.</summary>
        public static void Rebuild()
        {
            Clear();

            var bounds = CollectWallBounds();
            if (!CeilingGeometry.TryCompute(bounds, out var ceil))
                return;

            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = CeilingName;

            var col = go.GetComponent<Collider>();
            if (col != null)
            {
                if (Application.isPlaying) Object.Destroy(col);
                else Object.DestroyImmediate(col);
            }

            go.transform.position = ceil.center;
            go.transform.localScale = ceil.size;

            var renderer = go.GetComponent<MeshRenderer>();
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader != null)
            {
                var mat = new Material(shader);
                mat.SetColor("_BaseColor", new Color(0.93f, 0.93f, 0.95f));
                renderer.sharedMaterial = mat;
            }
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            renderer.receiveShadows = true;

            _ceiling = go;
        }

        public static void Clear()
        {
            if (_ceiling == null) return;
            if (Application.isPlaying) Object.Destroy(_ceiling);
            else Object.DestroyImmediate(_ceiling);
            _ceiling = null;
        }

        /// <summary>Мировые AABB всех стен на полной высоте. Опущенные стены
        /// временно восстанавливаются, чтобы контур считался по реальному верху.</summary>
        private static List<Bounds> CollectWallBounds()
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
