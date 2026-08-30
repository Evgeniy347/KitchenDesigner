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

            // Пока картинка декора не приехала, квадрат — ВРЕМЕННЫЙ ответ.
            // Закэшировать его значило бы оставить сплющенный декор до конца
            // сессии: OnTextureArrived сбросит запись, но только если она есть.
            if (!string.IsNullOrEmpty(def.id) && (tex != null || !def.HasTextureFile))
                _tileMM[def.id] = size;
            return size;
        }

        /// <summary>Картинка декора. null — либо декор чисто цветовой (тогда
        /// показывать надо <see cref="MaterialDef.baseColor"/>, как делает образец
        /// пипетки), либо картинка ещё не загружена: обращение к ней и есть повод
        /// её затребовать.</summary>
        public static Texture2D? ResolveTexture(MaterialDef def)
        {
            if (def == null) return null;
            if (def.texture != null) return def.texture;
            TextureLibrary.Request(def);
            return def.texture;
        }

        /// <summary>Картинка декора доехала. Материал шарится по id, поэтому
        /// достаточно положить текстуру в него — она появится сразу на всех
        /// деталях с этим декором, без обхода сцены.
        ///
        /// Если высота плитки выводилась из пропорций картинки, до её прихода она
        /// была квадратной — пересчитываем «вырез» у элементов с этим декором и
        /// перестраиваем накладки: их МЕШ строится по TileMM.</summary>
        public static void OnTextureArrived(MaterialDef def)
        {
            if (def == null || def.texture == null) return;

            if (_cache.TryGetValue(def.id, out var mat) && mat != null)
            {
                mat.SetTexture(BaseMap, def.texture);
                mat.mainTexture = def.texture;
                // Заглушка отработала: дальше цвет обязан быть белым, иначе он
                // домножится на приехавшую картинку и перекрасит её.
                ApplyAlbedo(mat, def);
            }

            if (def.tileHeightMM > 0) return; // размер плитки от картинки не зависел

            _tileMM.Remove(def.id);
            var all = PartRegistry.GetAll();
            if (all == null) return;

            foreach (var el in all)
            {
                if (el == null) continue;

                if (el.MaterialId == def.id
                    || MaterialIdOf(el, MaterialSlot.Tabletop) == def.id)
                    RefreshTiling(el, def);

                // SyncAll здесь не поможет: он пересобирает только сдвинутые и
                // растянутые элементы, а тут поменялся размер плитки декора.
                if (UsesInOverlay(el, def.id)) TextureOverlayRenderer.Refresh(el);
            }
        }

        private static bool UsesInOverlay(KitchenElement element, string materialId)
        {
            foreach (var overlay in element.TextureOverlays)
                if (overlay.MaterialId == materialId) return true;
            return false;
        }

        /// <summary>Надеть декор по id из сейва. Если такого декора в каталоге
        /// пока нет (индекс текстур ещё не приехал — WebGL; папку временно
        /// подменили), показываем дефолтный серый, но ЗАПОМНЕННЫЙ id не теряем:
        /// иначе перезапуск молча стирал бы выбранную текстуру, а следующее
        /// сохранение записывало бы вместо неё «default».</summary>
        public static void ApplyById(KitchenElement element, string materialId)
        {
            if (element == null) return;
            var def = MaterialCatalog.Get(materialId);
            Apply(element, def);
            if (!string.IsNullOrEmpty(materialId) && def.id != materialId)
                element.MaterialId = materialId;
        }

        /// <summary>Надеть декор на конкретный слот элемента. У обычной детали
        /// слот один («Текстура»), у стола их три — щит, столешница и ножки.</summary>
        public static void ApplySlot(KitchenElement element, MaterialSlot slot, MaterialDef def)
        {
            if (element == null || def == null) return;
            switch (slot)
            {
                case MaterialSlot.Tabletop:
                    if (element is TableElement topTable) ApplyTabletop(topTable, def);
                    else if (element is RadiusTableElement topRadius) ApplyTabletop(topRadius, def);
                    break;
                case MaterialSlot.Legs:
                    if (element is TableElement legsTable) ApplyLegs(legsTable, def);
                    else if (element is RadiusTableElement legsRadius) ApplyLegs(legsRadius, def);
                    break;
                default:
                    Apply(element, def);
                    break;
            }
        }

        /// <summary>Декор, который сейчас в слоте. У элемента без столешницы и
        /// ножек любой слот отвечает базовым декором.</summary>
        public static string MaterialIdOf(KitchenElement element, MaterialSlot slot)
        {
            if (element == null) return MaterialCatalog.DefaultId;
            switch (slot)
            {
                case MaterialSlot.Tabletop:
                    if (element is TableElement topTable) return topTable.TabletopMaterialId;
                    if (element is RadiusTableElement topRadius) return topRadius.TabletopMaterialId;
                    return element.MaterialId;
                case MaterialSlot.Legs:
                    if (element is TableElement legsTable) return legsTable.LegsMaterialId;
                    if (element is RadiusTableElement legsRadius) return legsRadius.LegsMaterialId;
                    return element.MaterialId;
                default:
                    return element.MaterialId;
            }
        }

        public static void ApplyTabletop(TableElement table, MaterialDef def)
        {
            if (table == null || def == null) return;
            table.TabletopMaterialId = def.id;
            var mat = GetSharedMaterial(def);
            if (mat != null) table.SetTabletopMaterial(mat);
            RefreshTiling(table, def);
        }

        public static void ApplyLegs(ITabletop tabletop, MaterialDef def)
        {
            if (tabletop is TableElement table) ApplyLegs(table, def);
            else if (tabletop is RadiusTableElement radiusTable) ApplyLegs(radiusTable, def);
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

            // У варочной две коробки (плита и короб выреза) — красить надо обе,
            // а GetComponentInChildren нашёл бы только первую. Материал она
            // выводит из своего же MaterialId, поэтому декор переживает и
            // пересборку геометрии.
            if (element is CooktopElement cooktop)
            {
                cooktop.ApplyMaterials();
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
                // Служебные сабмеши детали (паз, некромкованный торец) могли быть
                // затёрты тонировкой — декор возвращается вместе с ними.
                element.RefreshSubmeshMaterials();
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

            var tile = TileMM(def);
            var st = ComputeTileST(element.DimensionsMM, tile.x, tile.y);

            // У варочной две коробки, и «вырез» декора нужен обеим: на одной
            // плите он оставил бы короб выреза с нерастянутой плиткой.
            if (element is CooktopElement)
            {
                foreach (var cr in element.GetComponentsInChildren<MeshRenderer>())
                {
                    if (cr == null) continue;
                    cr.GetPropertyBlock(_mpb);
                    _mpb.SetVector(BaseMapST, st);
                    cr.SetPropertyBlock(_mpb);
                }
                return;
            }

            var r = element.GetComponentInChildren<MeshRenderer>();
            if (r == null) return;

            r.GetPropertyBlock(_mpb);
            _mpb.SetVector(BaseMapST, st);
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
            mat.SetFloat(Metallic, def.metallic);
            mat.SetFloat(Smoothness, def.smoothness);

            // Картинку достаём ДО цвета: от того, приехала ли она, зависит, что
            // класть в _BaseColor (см. ApplyAlbedo).
            var tex = ResolveTexture(def);
            if (tex != null)
            {
                ConfigureTexture(tex);
                mat.SetTexture(BaseMap, tex);
                mat.mainTexture = tex;
            }
            ApplyAlbedo(mat, def);

            _cache[def.id] = mat;
            return mat;
        }

        /// <summary>Положить в <c>_BaseColor</c> то, что должно быть видно.
        ///
        /// У декора с ЗАГРУЖЕННОЙ картинкой цвет обязан быть белым: URP Lit
        /// умножает <c>_BaseColor</c> на <c>_BaseMap</c>, и любой другой цвет
        /// перекрашивает текстуру. Средний цвет самой картинки тут особенно
        /// коварен — декор темнеет примерно вдвое, а по каналам ещё и насыщается.
        ///
        /// <c>MaterialDef.baseColor</c> у такого декора — ЗАГЛУШКА: она видна,
        /// пока картинка не приехала (иначе деталь секунду светится белым), и
        /// гасится в <see cref="OnTextureArrived"/>. У декора БЕЗ картинки цвет —
        /// это и есть весь декор.</summary>
        private static void ApplyAlbedo(Material mat, MaterialDef def)
        {
            var color = def.texture != null ? Color.white : def.baseColor;
            mat.SetColor(BaseColor, color);
            mat.color = color; // совместимость со стандартным доступом
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
