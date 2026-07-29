using System.Collections.Generic;
using UnityEngine;

namespace KitchenDesigner.Core
{
    /// <summary>Стадия загрузки картинки декора. Метаданные декора есть всегда
    /// (они приходят из index.json), а сама картинка подтягивается лениво — по
    /// первому обращению к декору.</summary>
    public enum TextureState
    {
        NotRequested,
        Loading,
        Loaded,
        Failed,
    }

    /// <summary>Декор материала мебельного щита (ЛДСП/МДФ/…). Текстура имеет
    /// ФИЗИЧЕСКИЙ размер <see cref="tileSizeMM"/>: щит любого размера показывает
    /// «вырез» этого декора (не вписывание картинки). fileName — имя файла в папке
    /// текстур (может отсутствовать — тогда декор чисто цветовой).</summary>
    public class MaterialDef
    {
        public string id;
        public string displayName;
        public string kind;            // ЛДСП / МДФ / Массив / Стекло / Металл
        public string? fileName;       // имя файла в папке текстур, напр. "oak.jpg"
        public Texture2D? texture;     // картинка, загруженная TextureLibrary (лениво)
        public TextureState textureState = TextureState.NotRequested;
        public Color baseColor = Color.gray;
        public int tileSizeMM = 800;   // физ. ШИРИНА картинки декора, мм
        public int tileHeightMM = 0;   // физ. ВЫСОТА картинки, мм (0 → по пропорциям картинки)
        public float metallic = 0f;
        public float smoothness = 0.2f;

        /// <summary>У декора есть файл картинки — значит его надо ждать/грузить.
        /// Без файла декор целиком описывается своим <see cref="baseColor"/>.</summary>
        public bool HasTextureFile => !string.IsNullOrEmpty(fileName);

        /// <summary>Физическая высота плитки декора (мм) БЕЗ учёта картинки.
        /// Настоящую высоту (с выводом из пропорций текстуры при
        /// <c>tileHeightMM == 0</c>) даёт <c>MaterialManager.TileMM</c> — только
        /// он умеет доставать текстуру. Здесь остаётся квадратный запас на
        /// случай чисто цветового декора.</summary>
        public int TileHeightMM => tileHeightMM > 0 ? tileHeightMM : tileSizeMM;

        public MaterialDef(string id, string displayName, string kind, Color color,
            string? fileName = null, int tileSizeMM = 800,
            float metallic = 0f, float smoothness = 0.2f)
        {
            this.id = id;
            this.displayName = displayName;
            this.kind = kind;
            this.baseColor = color;
            this.fileName = fileName;
            this.tileSizeMM = tileSizeMM;
            this.metallic = metallic;
            this.smoothness = smoothness;
        }
    }

    /// <summary>Каталог декоров. Единственный источник — <c>index.json</c> в папке
    /// текстур (см. <see cref="TextureLibrary"/>); в коде зашит ровно один декор —
    /// серый <see cref="DefaultId"/>, страховка на случай отсутствующего или битого
    /// индекса. В сохранение пишется только id материала; при загрузке декор
    /// находится здесь по id.</summary>
    public static class MaterialCatalog
    {
        public const string DefaultId = "default";

        /// <summary>Единственный код-определённый декор: им отвечает <see cref="Get"/>
        /// на неизвестный id и он же остаётся всем каталогом, если индекс не прочитался.
        /// Запись с тем же id в index.json перекрывает его.</summary>
        private static readonly MaterialDef _fallback =
            new MaterialDef(DefaultId, "Серый", "ЛДСП", new Color(0.80f, 0.80f, 0.80f));

        private static readonly List<MaterialDef> _defs = new List<MaterialDef>();

        // Индекс уже пытались прочитать. Без флага каждое обращение к каталогу
        // дёргало бы файл заново, а в WebGL — бесконечно ждало бы синхронного пути,
        // которого там нет (индекс приезжает корутиной и зовёт Load).
        private static bool _loaded;

        /// <summary>Прочитать индекс, если это ещё не делали. Синхронный путь есть
        /// на Standalone и в редакторе; в WebGL он ничего не делает, и каталог до
        /// прихода корутины состоит из одного дефолтного декора.</summary>
        private static void EnsureLoaded()
        {
            if (_loaded) return;
            _loaded = true; // до вызова: Load() внутри не должен уйти в рекурсию
            TextureLibrary.TryLoadIndexSync();
        }

        public static IReadOnlyList<MaterialDef> All
        {
            get { EnsureLoaded(); return _defs; }
        }

        public static MaterialDef Default => Get(DefaultId);

        /// <summary>Заменить содержимое каталога разобранным индексом.</summary>
        public static void Load(IEnumerable<MaterialDef> defs)
        {
            _defs.Clear();
            foreach (var d in defs) Put(d);
            _loaded = true;
        }

        /// <summary>Добавить/заменить декор по id поверх индекса. Замена по id
        /// делает повторную регистрацию идемпотентной.
        ///
        /// Индекс дочитывается ПЕРЕД добавлением: иначе первое же обращение к
        /// каталогу после этого вызова затёрло бы добавленный декор чтением файла.</summary>
        public static void Register(MaterialDef def)
        {
            EnsureLoaded();
            Put(def);
        }

        private static void Put(MaterialDef def)
        {
            if (def == null || string.IsNullOrEmpty(def.id)) return;
            _defs.RemoveAll(d => d.id == def.id);
            _defs.Add(def);
        }

        /// <summary>Забыть каталог: следующее обращение перечитает индекс. Зовётся
        /// из <c>TextureLibrary.Reload</c> и из тестов.</summary>
        public static void Reset()
        {
            _defs.Clear();
            _loaded = false;
        }

        /// <summary>Декор по id. Неизвестный/пустой id → дефолтный (надёжно к
        /// повреждённым сейвам и к декору, выпавшему из индекса).</summary>
        public static MaterialDef Get(string? id)
        {
            EnsureLoaded();
            if (!string.IsNullOrEmpty(id))
                foreach (var d in _defs)
                    if (d.id == id) return d;

            foreach (var d in _defs)
                if (d.id == DefaultId) return d;
            return _fallback;
        }
    }
}
