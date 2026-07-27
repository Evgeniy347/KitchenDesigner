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

        // Разрешённый физ. размер плитки (мм) по id декора: высота может браться
        // из пропорций картинки, а это требует её загрузки — кэшируем, чтобы
        // тянущийся ресайз не дёргал Resources.Load каждый кадр.
        private static readonly Dictionary<string, Vector2Int> _tileMM = new Dictionary<string, Vector2Int>();

        // Один переиспользуемый блок: RefreshTiling зовётся на каждый кадр
        // ресайза, аллокация MaterialPropertyBlock там ни к чему.
        private static readonly MaterialPropertyBlock _mpb = new MaterialPropertyBlock();

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

        /// <summary>Физ. высота плитки по её ширине и пропорциям картинки. Нужна,
        /// когда декор задан только шириной: подставлять квадрат для картинки
        /// 1920×853 значит сплющить её по вертикали в 2,25 раза. Чистая функция.</summary>
        public static int TileHeightFromAspect(int tileWidthMM, int texWidthPx, int texHeightPx)
        {
            if (texWidthPx <= 0 || texHeightPx <= 0) return Mathf.Max(1, tileWidthMM);
            return Mathf.Max(1, Mathf.RoundToInt(tileWidthMM * (float)texHeightPx / texWidthPx));
        }

        /// <summary>Физ. размер плитки декора (мм). Ширина — из декора; высота
        /// либо задана явно, либо выводится из пропорций текстуры (а без
        /// текстуры плитка квадратная).</summary>
        public static Vector2Int TileMM(MaterialDef def)
        {
            if (def == null) return new Vector2Int(1, 1);

            int w = Mathf.Max(1, def.tileSizeMM);
            if (def.tileHeightMM > 0) return new Vector2Int(w, def.tileHeightMM);

            if (!string.IsNullOrEmpty(def.id) && _tileMM.TryGetValue(def.id, out var cached))
                return cached;

            var tex = ResolveTexture(def);
            var size = new Vector2Int(w, tex != null
                ? TileHeightFromAspect(w, tex.width, tex.height)
                : w);
            if (!string.IsNullOrEmpty(def.id)) _tileMM[def.id] = size;
            return size;
        }

        /// <summary>Картинка декора: уже загруженная из внешней папки (приоритет)
        /// или из Resources. null — декор чисто цветовой.</summary>
        private static Texture2D? ResolveTexture(MaterialDef def)
            => def.texture != null ? def.texture
                : (!string.IsNullOrEmpty(def.baseMapResource)
                    ? Resources.Load<Texture2D>(def.baseMapResource)
                    : null);

        public static void ApplyById(KitchenElement element, string materialId)
            => Apply(element, MaterialCatalog.Get(materialId));

        public static void ApplyTabletop(TableElement table, MaterialDef def)
        {
            if (table == null || def == null) return;
            table.TabletopMaterialId = def.id;
            var mat = GetSharedMaterial(def);
            if (mat != null) table.SetTabletopMaterial(mat);
            RefreshTiling(table, def);
        }

        public static void ApplyLegs(TableElement table, MaterialDef def)
        {
            if (table == null || def == null) return;
            table.LegsMaterialId = def.id;
            var mat = GetSharedMaterial(def);
            if (mat != null) table.SetLegsMaterial(mat);
        }

        public static void ApplyTabletop(RadiusTableElement table, MaterialDef def)
        {
            if (table == null || def == null) return;
            table.TabletopMaterialId = def.id;
            var mat = GetSharedMaterial(def);
            if (mat != null) table.SetTabletopMaterial(mat);
            RefreshTiling(table, def);
        }

        public static void ApplyLegs(RadiusTableElement table, MaterialDef def)
        {
            if (table == null || def == null) return;
            table.LegsMaterialId = def.id;
            var mat = GetSharedMaterial(def);
            if (mat != null) table.SetLegsMaterial(mat);
        }

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
                table.TabletopMaterialId = def.id;
                table.SetTabletopMaterial(mat);
                RefreshTiling(element, def);
                return;
            }

            if (element is RadiusTableElement radiusTable)
            {
                radiusTable.TabletopMaterialId = def.id;
                radiusTable.SetTabletopMaterial(mat);
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

            var tile = TileMM(def);
            r.GetPropertyBlock(_mpb);
            _mpb.SetVector(BaseMapST, ComputeTileST(element.DimensionsMM, tile.x, tile.y));
            r.SetPropertyBlock(_mpb);
        }

        public static Material? GetSharedMaterial(MaterialDef def)
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

            var tex = ResolveTexture(def);
            if (tex != null)
            {
                ConfigureTexture(tex);
                mat.SetTexture(BaseMap, tex);
                mat.mainTexture = tex;
            }

            _cache[def.id] = mat;
            return mat;
        }

        /// <summary>Режимы фильтрации декора. Repeat — щит крупнее плитки просто
        /// повторяет её (текстуры бесшовные). Трилинейная фильтрация + анизотропия
        /// нужны из-за косых углов: столешница и пол уходят от камеры почти в
        /// плоскость, и на bilinear+aniso 1 дальняя часть смазывается в кашу.</summary>
        public static void ConfigureTexture(Texture2D tex)
        {
            if (tex == null) return;
            tex.wrapMode = TextureWrapMode.Repeat;
            tex.filterMode = FilterMode.Trilinear;
            tex.anisoLevel = 8;
        }

        /// <summary>Сброс кэша материалов (для тестов).</summary>
        public static void ClearCache()
        {
            _cache.Clear();
            _tileMM.Clear();
        }
    }
}
