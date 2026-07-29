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
        public string? baseMapResource; // путь под Resources/ (без расширения), напр. "Textures/oak"
        public Texture2D? texture;      // уже загруженная текстура (из внешней папки); приоритетнее baseMapResource
        public Color baseColor = Color.gray;
        public int tileSizeMM = 800;   // физ. ШИРИНА картинки декора, мм
        public int tileHeightMM = 0;   // физ. ВЫСОТА картинки, мм (0 → по пропорциям картинки)
        public float metallic = 0f;
        public float smoothness = 0.2f;

        /// <summary>Физическая высота плитки декора (мм) БЕЗ учёта картинки.
        /// Настоящую высоту (с выводом из пропорций текстуры при
        /// <c>tileHeightMM == 0</c>) даёт <c>MaterialManager.TileMM</c> — только
        /// он умеет доставать текстуру. Здесь остаётся квадратный запас на
        /// случай чисто цветового декора.</summary>
        public int TileHeightMM => tileHeightMM > 0 ? tileHeightMM : tileSizeMM;

        public MaterialDef(string id, string displayName, string kind, Color color,
            string? baseMapResource = null, int tileSizeMM = 800,
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
            // Картинка 1920×853 px — лист декора; при ширине 2000 мм ламель
            // выходит ~178 мм, как у настоящего дуба. Высота (≈889 мм) считается
            // из пропорций картинки, задавать её вручную не нужно.
            new MaterialDef("teplyy_medovyy_dub", "теплый медовый дуб", "ЛДСП", Color.white, "Textures/teplyy_medovyy_dub", 2000),
            // Строганый шпон дуба (фото 14_dub_16, лист целиком — ламелей нет).
            // Опорный размер — ширина листа шпона ~220 мм: она занимает ~380 px
            // исходника, кроп взял 214 px ≈ 124 мм по высоте, откуда ширина
            // 920 px ≈ 550 мм. Высота считается из пропорций картинки.
            new MaterialDef("dub_evropeyskiy", "Дуб европейский", "ЛДСП", Color.white, "Textures/dub_evropeyskiy", 550),
            // Тот же шпон под тонировкой: картинка умножена в линейном свете на
            // константу по каналам, поэтому рисунок волокон тот же, изменён тон.
            // Цель — альбедо пола с фото-референса, sRGB (142, 102, 66).
            new MaterialDef("dub_evropeyskiy_tinting", "Дуб европейский тонированный", "ЛДСП", Color.white, "Textures/dub_evropeyskiy_tinting", 550),
            new MaterialDef("dub_evropeyskiy_tinting_2", "Сосна", "ЛДСП", Color.white, "Textures/dub_evropeyskiy_tinting_2", 550),
            // Та же тонировка, но тон снят со второго фото той же поверхности.
            // Два фото дают РАЗНОЕ альбедо (см. tmp-scripts/tint-match.ps1):
            // (153,114,78) против (200,175,148). Лист бумаги снимает цвет лампы,
            // но не убирает блик-вуаль от глянца — какой вариант верный, решает глаз.
            new MaterialDef("dub_evropeyskiy_tinting_3", "Дуб европейский тонированный 3", "ЛДСП", Color.white, "Textures/dub_evropeyskiy_tinting_3", 550),
            // --- ЛМДФ двусторонняя (EGGER / Kastamonu / СвиссКроно), лист 2800×2070×16 ---
            // Исходники — квадратные сколы декора 1080×1080 px, обработаны на
            // бесшовность (tools/make-seamless.ps1). tileSizeMM = число ламелей
            // поперёк картинки × 190 мм (реальная ширина плашки), высота считается
            // из пропорций — картинки квадратные, поэтому равна ширине.
            // Однотонные декоры рисунка не имеют, им дан нейтральный размер 600 мм.
            new MaterialDef("belaya_gladkaya_svisskrono", "Белая гладкая (СвиссКроно)", "ЛМДФ", Color.white, "Textures/belaya_gladkaya_svisskrono", 600),
            new MaterialDef("belaya_gladkaya_kastamonu", "Белая гладкая (Kastamonu F)", "ЛМДФ", Color.white, "Textures/belaya_gladkaya_kastamonu", 600),
            new MaterialDef("belyy_premium_filvud_w1000", "Белый премиум филвуд (W1000 ST38)", "ЛМДФ", Color.white, "Textures/belyy_premium_filvud_w1000", 600),
            new MaterialDef("les_chernyy_u998", "Лес чёрный филвуд (U998 ST38)", "ЛМДФ", Color.white, "Textures/les_chernyy_u998", 600),
            new MaterialDef("dub_galifaks_belyy_h1176", "Дуб галифакс белый (H1176 ST37)", "ЛМДФ", Color.white, "Textures/dub_galifaks_belyy_h1176", 400),
            new MaterialDef("dub_galifaks_naturalnyy_h1180", "Дуб галифакс натуральный (H1180 ST37)", "ЛМДФ", Color.white, "Textures/dub_galifaks_naturalnyy_h1180", 400),
            new MaterialDef("dub_galifaks_olovo_h3176", "Дуб галифакс олово (H3176 ST37)", "ЛМДФ", Color.white, "Textures/dub_galifaks_olovo_h3176", 400),
            new MaterialDef("dub_galifaks_tabak_h1181", "Дуб галифакс табак (H1181 ST37)", "ЛМДФ", Color.white, "Textures/dub_galifaks_tabak_h1181", 550),
            new MaterialDef("dub_gladstoun_pesochnyy_h3309", "Дуб гладстоун песочный (H3309 ST28)", "ЛМДФ", Color.white, "Textures/dub_gladstoun_pesochnyy_h3309", 550),
            new MaterialDef("dub_gladstoun_tabak_h3325", "Дуб гладстоун табак (H3325 ST28)", "ЛМДФ", Color.white, "Textures/dub_gladstoun_tabak_h3325", 550),
            new MaterialDef("dub_davenport_naturalnyy_svetlyy_h3359", "Дуб давенпорт натуральный светлый (H3359 ST32)", "ЛМДФ", Color.white, "Textures/dub_davenport_naturalnyy_svetlyy_h3359", 550),
            new MaterialDef("dub_kuneo_korichnevyy_h3317", "Дуб кунео коричневый (H3317 ST28)", "ЛМДФ", Color.white, "Textures/dub_kuneo_korichnevyy_h3317", 750),
            // Единственный декор партии со СНЯТЫМ ПОПЕРЁК рисунком: ламели идут
            // горизонтально, 6 штук по высоте. Картинка квадратная, поэтому
            // ширина равна той же высоте — 6 × 190 ≈ 1150 мм.
            new MaterialDef("dub_kasella_kashtanovyy_h1369", "Дуб каселла каштановый (H1369 ST40)", "ЛМДФ", Color.white, "Textures/dub_kasella_kashtanovyy_h1369", 1150),
            new MaterialDef("dub_kasella_korichnevyy_h1386", "Дуб каселла коричневый (H1386 ST40)", "ЛМДФ", Color.white, "Textures/dub_kasella_korichnevyy_h1386", 400),
            new MaterialDef("dub_kasella_naturalnyy_svetlyy_h1367", "Дуб каселла натуральный светлый (H1367 ST40)", "ЛМДФ", Color.white, "Textures/dub_kasella_naturalnyy_svetlyy_h1367", 750),
            new MaterialDef("dub_kasella_naturalnyy_h1385", "Дуб каселла натуральный (H1385 ST40)", "ЛМДФ", Color.white, "Textures/dub_kasella_naturalnyy_h1385", 750),
            new MaterialDef("dub_sakramento_korichnevyy_h1142", "Дуб сакраменто коричневый (H1142 ST36)", "ЛМДФ", Color.white, "Textures/dub_sakramento_korichnevyy_h1142", 750),
            new MaterialDef("dub_charlston_temno_korichnevyy_h3154", "Дуб чарльстон тёмно-коричневый (H3154 ST36)", "ЛМДФ", Color.white, "Textures/dub_charlston_temno_korichnevyy_h3154", 950),
            new MaterialDef("dub_sherman_antratsit_h1346", "Дуб шерман антрацит (H1346 ST32)", "ЛМДФ", Color.white, "Textures/dub_sherman_antratsit_h1346", 550),
            new MaterialDef("dub_sherman_konyak_h1344", "Дуб шерман коньяк коричневый (H1344 ST32)", "ЛМДФ", Color.white, "Textures/dub_sherman_konyak_h1344", 400),
            new MaterialDef("yasen_navarra_h1250", "Ясень наварра (H1250 ST36)", "ЛМДФ", Color.white, "Textures/yasen_navarra_h1250", 950),
            new MaterialDef("gtv_anthracite", "Антрацит (GTV)", "Металл", new Color(0.25f, 0.25f, 0.27f), metallic: 0.4f, smoothness: 0.3f),
            new MaterialDef("gtv_white",      "Белый (GTV)",    "Металл", new Color(0.92f, 0.92f, 0.90f), metallic: 0.3f, smoothness: 0.3f),
            new MaterialDef("gtv_black",      "Чёрный (GTV)",   "Металл", new Color(0.10f, 0.10f, 0.11f), metallic: 0.3f, smoothness: 0.3f),
        };

        // Динамические декоры, подгруженные из внешней папки в рантайме
        // (ExternalTextureCatalog). Пересобираются при каждом reload.
        private static readonly List<MaterialDef> _dynamic = new List<MaterialDef>();

        // Кэш объединённого списка (встроенные + динамические); сбрасывается на null
        // при изменении _dynamic и лениво пересобирается.
        private static List<MaterialDef>? _combined;

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
        public static MaterialDef Get(string? id)
        {
            if (!string.IsNullOrEmpty(id))
                foreach (var d in Combined())
                    if (d.id == id) return d;
            return Default;
        }
    }
}
