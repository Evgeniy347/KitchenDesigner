using System.Collections.Generic;
using UnityEngine;

namespace KitchenDesigner.Core
{
    /// <summary>Применение декоров к доскам. Материал шарится по декору (один
    /// Material на id — для батчинга), а индивидуальный «вырез» текстуры под
    /// размер щита задаётся через MaterialPropertyBlock (_BaseMap_ST).</summary>
    public static class MaterialManager
    {
        private static readonly int BaseMapST = Shader.PropertyToID("_BaseMap_ST");
        private static readonly int BaseMap = Shader.PropertyToID("_BaseMap");
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        private static readonly int Metallic = Shader.PropertyToID("_Metallic");
        private static readonly int Smoothness = Shader.PropertyToID("_Smoothness");

        private static readonly Dictionary<string, Material> _cache = new Dictionary<string, Material>();

        /// <summary>UV-масштаб «вырез под размер щита»: фиксированный физический
        /// масштаб декора, картинка обрезается/повторяется, а не вписывается.
        /// scale = размер_щита_мм / размер_декора_мм. Чистая функция.</summary>
        public static Vector4 ComputeTileST(Vector3Int dimsMM, int tileSizeMM)
        {
            float tile = Mathf.Max(1, tileSizeMM);
            return new Vector4(dimsMM.x / tile, dimsMM.y / tile, 0f, 0f);
        }

        public static void ApplyById(KitchenElement element, string materialId)
            => Apply(element, MaterialCatalog.Get(materialId));

        public static void Apply(KitchenElement element, MaterialDef def)
        {
            if (element == null || def == null) return;
            element.MaterialId = def.id;

            var r = element.GetComponentInChildren<MeshRenderer>();
            if (r == null) return;

            var mat = GetSharedMaterial(def);
            if (mat != null)
            {
                // У сборного фасада 2 сабмеша (декор + фрезеровки) — меняем только
                // декор (индекс 0), сохраняя остальные материалы.
                var mats = r.sharedMaterials;
                if (mats.Length > 1)
                {
                    mats[0] = mat;
                    r.sharedMaterials = mats;
                }
                else
                {
                    r.sharedMaterial = mat;
                }
            }
            RefreshTiling(element, def);
        }

        /// <summary>Пересчитать «вырез» текстуры под текущий размер щита (звать
        /// после ресайза, чтобы декор не растягивался).</summary>
        public static void RefreshTiling(KitchenElement element)
            => RefreshTiling(element, MaterialCatalog.Get(element != null ? element.MaterialId : null));

        public static void RefreshTiling(KitchenElement element, MaterialDef def)
        {
            if (element == null || def == null) return;
            var r = element.GetComponentInChildren<MeshRenderer>();
            if (r == null) return;

            var mpb = new MaterialPropertyBlock();
            r.GetPropertyBlock(mpb);
            mpb.SetVector(BaseMapST, ComputeTileST(element.DimensionsMM, def.tileSizeMM));
            r.SetPropertyBlock(mpb);
        }

        private static Material GetSharedMaterial(MaterialDef def)
        {
            if (def == null) return null;
            if (_cache.TryGetValue(def.id, out var cached) && cached != null)
                return cached;

            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) return null;

            var mat = new Material(shader);
            mat.SetColor(BaseColor, def.baseColor);
            mat.color = def.baseColor; // совместимость со стандартным доступом
            mat.SetFloat(Metallic, def.metallic);
            mat.SetFloat(Smoothness, def.smoothness);

            if (!string.IsNullOrEmpty(def.baseMapResource))
            {
                var tex = Resources.Load<Texture2D>(def.baseMapResource);
                if (tex != null)
                {
                    tex.wrapMode = TextureWrapMode.Repeat; // повтор декора при крупном щите
                    mat.SetTexture(BaseMap, tex);
                    mat.mainTexture = tex;
                }
            }

            _cache[def.id] = mat;
            return mat;
        }

        /// <summary>Сброс кэша материалов (для тестов).</summary>
        public static void ClearCache() => _cache.Clear();
    }
}
