using System.Collections.Generic;
using UnityEngine;

namespace KitchenDesigner.Core
{
    /// <summary>Применение декоров к деталям. Материал шарится по декору (один
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
            => ComputeTileST(dimsMM, tileSizeMM, tileSizeMM);

        /// <summary>Как выше, но с раздельными физ. размерами плитки по X и Y
        /// (декор может быть неквадратным — грейн/направленная текстура).</summary>
        public static Vector4 ComputeTileST(Vector3Int dimsMM, int tileWidthMM, int tileHeightMM)
        {
            float tw = Mathf.Max(1, tileWidthMM);
            float th = Mathf.Max(1, tileHeightMM);
            return new Vector4(dimsMM.x / tw, dimsMM.y / th, 0f, 0f);
        }

        public static void ApplyById(KitchenElement element, string materialId)
            => Apply(element, MaterialCatalog.Get(materialId));

        /// <summary>У элемента назначен НЕстандартный декор (не дефолтный серый) —
        /// т.е. пользователь выбрал текстуру и её надо показывать вместо
        /// валидационного тона подсветки.</summary>
        public static bool HasCustomDecor(KitchenElement element)
            => element != null
               && !string.IsNullOrEmpty(element.MaterialId)
               && element.MaterialId != MaterialCatalog.DefaultId;

        /// <summary>Повесить на элемент его СОБСТВЕННЫЙ декор (по текущему MaterialId).
        /// Используется подсветкой, чтобы показать текстуру объекта.</summary>
        public static void ApplyOwnDecor(KitchenElement element)
            => Apply(element, MaterialCatalog.Get(element != null ? element.MaterialId : null));

        public static void Apply(KitchenElement element, MaterialDef def)
        {
            if (element == null || def == null) return;
            element.MaterialId = def.id;

            var mat = GetSharedMaterial(def);
            if (mat == null) return;

            if (element is TableElement table)
            {
                table.SetMaterial(mat);
                RefreshTiling(element, def);
                return;
            }

            if (element is RadiusTableElement radiusTable)
            {
                radiusTable.SetMaterial(mat);
                RefreshTiling(element, def);
                return;
            }

            var r = element.GetComponentInChildren<MeshRenderer>();
            if (r == null) return;

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
            mpb.SetVector(BaseMapST, ComputeTileST(element.DimensionsMM, def.tileSizeMM, def.TileHeightMM));
            r.SetPropertyBlock(mpb);
        }

        private static Material? GetSharedMaterial(MaterialDef def)
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

            // Текстура: уже загруженная из внешней папки (приоритет) или из Resources.
            var tex = def.texture != null ? def.texture
                : (!string.IsNullOrEmpty(def.baseMapResource) ? Resources.Load<Texture2D>(def.baseMapResource) : null);
            if (tex != null)
            {
                tex.wrapMode = TextureWrapMode.Repeat; // повтор декора при крупном щите
                mat.SetTexture(BaseMap, tex);
                mat.mainTexture = tex;
            }

            _cache[def.id] = mat;
            return mat;
        }

        /// <summary>Сброс кэша материалов (для тестов).</summary>
        public static void ClearCache() => _cache.Clear();
    }
}
