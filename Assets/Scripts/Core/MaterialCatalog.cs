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
        public Color baseColor = Color.gray;
        public int tileSizeMM = 800;   // физ. размер картинки декора, мм
        public float metallic = 0f;
        public float smoothness = 0.2f;

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

        private static readonly List<MaterialDef> _all = new List<MaterialDef>
        {
            new MaterialDef(DefaultId, "Серый",       "ЛДСП", new Color(0.80f, 0.80f, 0.80f)),
            new MaterialDef("white",   "Белый",       "ЛДСП", new Color(0.95f, 0.95f, 0.95f)),
            new MaterialDef("oak",     "Дуб сонома",  "ЛДСП", new Color(0.78f, 0.66f, 0.45f), "Textures/oak",     800),
            new MaterialDef("wenge",   "Венге",       "ЛДСП", new Color(0.28f, 0.20f, 0.16f), "Textures/wenge",   800),
            new MaterialDef("concrete","Бетон",       "ЛДСП", new Color(0.62f, 0.62f, 0.60f), "Textures/concrete",1200),
        };

        public static IReadOnlyList<MaterialDef> All => _all;

        public static MaterialDef Default => _all[0];

        /// <summary>Декор по id. Неизвестный/пустой id → дефолтный (надёжно к
        /// повреждённым сейвам).</summary>
        public static MaterialDef Get(string id)
        {
            if (!string.IsNullOrEmpty(id))
                foreach (var d in _all)
                    if (d.id == id) return d;
            return Default;
        }
    }
}
