using System.Collections.Generic;
using UnityEngine;

namespace KitchenDesigner.Core
{
    /// <summary>Декор материала мебельного щита (ЛДСП/МДФ/…). Текстура имеет
    /// ФИЗИЧЕСКИЙ размер <see cref="tileSizeMM"/>: щит любого размера показывает
    /// «вырез» этого декора (не вписывание картинки). baseMapResource — путь к
    /// текстуре в Resources (может отсутствовать — тогда только цвет).</summary>
    public class MaterialDef
    {
        public string id;
        public string displayName;
        public string kind;            // ЛДСП / МДФ / Массив / Стекло / Металл
        public string baseMapResource; // путь под Resources/ (без расширения), напр. "Textures/oak"
        public Texture2D texture;      // уже загруженная текстура (из внешней папки); приоритетнее baseMapResource
        public Color baseColor = Color.gray;
        public int tileSizeMM = 800;   // физ. ШИРИНА картинки декора, мм
        public int tileHeightMM = 0;   // физ. ВЫСОТА картинки, мм (0 → квадрат = tileSizeMM)
        public float metallic = 0f;
        public float smoothness = 0.2f;

        /// <summary>Физическая высота плитки декора (мм). 0 в поле → квадрат (= ширине).</summary>
        public int TileHeightMM => tileHeightMM > 0 ? tileHeightMM : tileSizeMM;

        public MaterialDef(string id, string displayName, string kind, Color color,
            string baseMapResource = null, int tileSizeMM = 800,
            float metallic = 0f, float smoothness = 0.2f)
        {
            this.id = id;
            this.displayName = displayName;
            this.kind = kind;
            this.baseColor = color;
            this.baseMapResource = baseMapResource;
            this.tileSizeMM = tileSizeMM;
            this.metallic = metallic;
            this.smoothness = smoothness;
        }
    }

    /// <summary>Встроенный каталог декоров. Код-определённый (без .asset), чтобы
    /// работать в тестах и до добавления текстур. В сохранение пишется только
    /// id материала; при загрузке декор находится здесь по id.</summary>
    public static class MaterialCatalog
    {
        public const string DefaultId = "default";

        // Встроенные (код-определённые) декоры — всегда есть, работают в тестах.
        private static readonly List<MaterialDef> _builtin = new List<MaterialDef>
        {
            new MaterialDef(DefaultId, "Серый",       "ЛДСП", new Color(0.80f, 0.80f, 0.80f)),
            new MaterialDef("white",   "Белый",       "ЛДСП", new Color(0.95f, 0.95f, 0.95f)),
            new MaterialDef("oak",     "Дуб сонома",  "ЛДСП", new Color(0.78f, 0.66f, 0.45f), "Textures/oak",     800),
            new MaterialDef("wenge",   "Венге",       "ЛДСП", new Color(0.28f, 0.20f, 0.16f), "Textures/wenge",   800),
            new MaterialDef("concrete","Бетон",       "ЛДСП", new Color(0.62f, 0.62f, 0.60f), "Textures/concrete",1200),
        };

        // Динамические декоры, подгруженные из внешней папки в рантайме
        // (ExternalTextureCatalog). Пересобираются при каждом reload.
        private static readonly List<MaterialDef> _dynamic = new List<MaterialDef>();

        // Кэш объединённого списка (встроенные + динамические); сбрасывается на null
        // при изменении _dynamic и лениво пересобирается.
        private static List<MaterialDef> _combined;

        private static List<MaterialDef> Combined()
        {
            if (_combined == null)
            {
                _combined = new List<MaterialDef>(_builtin);
                _combined.AddRange(_dynamic);
            }
            return _combined;
        }

        public static IReadOnlyList<MaterialDef> All => Combined();

        public static MaterialDef Default => _builtin[0];

        /// <summary>Зарегистрировать/заменить динамический декор (по id). Замена по id
        /// делает повторный reload идемпотентным.</summary>
        public static void RegisterDynamic(MaterialDef def)
        {
            if (def == null || string.IsNullOrEmpty(def.id)) return;
            _dynamic.RemoveAll(d => d.id == def.id);
            _dynamic.Add(def);
            _combined = null;
        }

        /// <summary>Убрать все динамические декоры (перед пере-сканированием папки).</summary>
        public static void ClearDynamic()
        {
            _dynamic.Clear();
            _combined = null;
        }

        /// <summary>Декор по id. Неизвестный/пустой id → дефолтный (надёжно к
        /// повреждённым сейвам).</summary>
        public static MaterialDef Get(string id)
        {
            if (!string.IsNullOrEmpty(id))
                foreach (var d in Combined())
                    if (d.id == id) return d;
            return Default;
        }
    }
}
