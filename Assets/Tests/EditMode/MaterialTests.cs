using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

/// <summary>Ядро применения декоров: каталог, «вырез» текстуры под размер щита
/// (фиксированный физ. масштаб), сохранение materialId, спецификация по материалу.</summary>
public class MaterialTests
{
    private readonly List<GameObject> _spawned = new List<GameObject>();
    private readonly List<Texture2D> _textures = new List<Texture2D>();

    private Texture2D Tex(int w, int h)
    {
        var t = new Texture2D(w, h);
        _textures.Add(t);
        return t;
    }

    private KitchenElement Make(string name, Vector3Int dims, string? materialId = null)
    {
        var go = new GameObject(name);
        var e = go.AddComponent<KitchenElement>();
        e.PartName = name;
        e.DimensionsMM = dims;
        if (materialId != null) e.MaterialId = materialId;
        _spawned.Add(go);
        return e;
    }

    [TearDown]
    public void Teardown()
    {
        MaterialManager.ClearCache();
        MaterialCatalog.Reset();
        foreach (var go in _spawned) if (go != null) Object.DestroyImmediate(go);
        _spawned.Clear();
        foreach (var t in _textures) if (t != null) Object.DestroyImmediate(t);
        _textures.Clear();
        foreach (var e in Object.FindObjectsByType<KitchenElement>())
            if (e != null) Object.DestroyImmediate(e.gameObject);
    }

    // --- Каталог ---

    [Test]
    public void Catalog_LoadsProjectIndex_WithKnownDecors()
    {
        // Каталог целиком приходит из StreamingAssets/Textures/index.json. Декоры
        // из старых сейвов (docs/example.save.json) обязаны в нём остаться: их id
        // — ключ сохранения, потерять его значит потерять декор у проекта.
        Assert.IsNotNull(MaterialCatalog.Default);
        Assert.AreEqual(MaterialCatalog.DefaultId, MaterialCatalog.Default.id);
        Assert.AreEqual("oak", MaterialCatalog.Get("oak").id);
        Assert.AreEqual("wenge", MaterialCatalog.Get("wenge").id);
        Assert.AreEqual("gtv_anthracite", MaterialCatalog.Get("gtv_anthracite").id);
        Assert.Greater(MaterialCatalog.All.Count, 1);
    }

    [Test]
    public void Catalog_UnknownOrEmptyId_FallsBackToDefault()
    {
        Assert.AreEqual(MaterialCatalog.DefaultId, MaterialCatalog.Get("no-such-decor").id);
        Assert.AreEqual(MaterialCatalog.DefaultId, MaterialCatalog.Get(null).id);
        Assert.AreEqual(MaterialCatalog.DefaultId, MaterialCatalog.Get("").id);
    }

    // --- «Вырез» текстуры под размер: фиксированный физ. масштаб, без вписывания ---

    [Test]
    public void ComputeTileST_BoardEqualsTile_ScaleOne()
    {
        var st = MaterialManager.ComputeTileST(new Vector3Int(800, 800, 18), 800);
        Assert.AreEqual(1f, st.x, 0.0001f);
        Assert.AreEqual(1f, st.y, 0.0001f);
    }

    [Test]
    public void ComputeTileST_BigBoard_RepeatsDecor()
    {
        // Щит 1600×800 на декоре 800 мм → по ширине два повтора, по высоте один.
        var st = MaterialManager.ComputeTileST(new Vector3Int(1600, 800, 18), 800);
        Assert.AreEqual(2f, st.x, 0.0001f);
        Assert.AreEqual(1f, st.y, 0.0001f);
    }

    [Test]
    public void ComputeTileST_SmallBoard_ShowsCropBelowOne()
    {
        // Маленький щит 400×200 → видит лишь часть декора (масштаб < 1), картинка
        // не «вписывается», а обрезается.
        var st = MaterialManager.ComputeTileST(new Vector3Int(400, 200, 18), 800);
        Assert.AreEqual(0.5f, st.x, 0.0001f);
        Assert.AreEqual(0.25f, st.y, 0.0001f);
    }

    // --- MaterialId сохраняется ---

    [Test]
    public void MaterialId_RoundTrips_ThroughSaveLoad()
    {
        var e = Make("Decorated", new Vector3Int(800, 400, 18), "wenge");

        var json = SaveLoadManager.Serialize(SaveLoadManager.CaptureScene(new[] { e }));
        var data = SaveLoadManager.Deserialize(json);

        Assert.AreEqual(1, data!.elements.Length);
        Assert.AreEqual("wenge", data.elements[0].materialId);
    }

    [Test]
    public void MaterialId_DefaultsToDefault_WhenUnset()
    {
        var e = Make("Plain", new Vector3Int(800, 400, 18));
        Assert.AreEqual(MaterialCatalog.DefaultId, e.MaterialId);
        Assert.AreEqual(MaterialCatalog.DefaultId, ElementCapture.FromElement(e).materialId);
    }

    // --- Спецификация группирует по материалу ---

    [Test]
    public void Spec_SameSizeDifferentMaterial_SeparateLines()
    {
        Make("Board", new Vector3Int(800, 400, 18), "oak");
        Make("Board", new Vector3Int(800, 400, 18), "oak");
        Make("Board", new Vector3Int(800, 400, 18), "wenge");

        var spec = SpecificationManager.Build(_spawned.ConvertAll(g => g.GetComponent<KitchenElement>()));

        // +2 строки кромки погонными метрами (кромкование по умолчанию включено, торцы
        // открыты у всех трёх досок) — кромка продаётся по декору окантованной доски (дефект
        // приёмки №4), у "oak" и "wenge" разный декор, значит и своя строка кромки на каждый.
        Assert.AreEqual(4, spec.lines.Count,
            "одинаковый размер, но разный декор → разные строки досок, плюс своя кромка на каждый декор");
        Assert.AreEqual(3, spec.totalCount);

        var csv = SpecificationExport.ToCsv(spec);
        StringAssert.Contains("Material", csv);
        StringAssert.Contains("Дуб сонома", csv);
        StringAssert.Contains("Венге", csv);
    }

    // --- Применение декора ---

    [Test]
    public void Apply_SetsMaterialId_OnRealBoard()
    {
        var go = ElementFactory.CreatePart(new Vector3Int(800, 400, 18), "B", Vector3.zero);
        _spawned.Add(go);
        var e = go.GetComponent<KitchenElement>();

        MaterialManager.ApplyById(e, "oak");
        Assert.AreEqual("oak", e.MaterialId);
    }

    // --- «Включать текстуру, если задана»: декор поверх валидационного тона ---

    [Test]
    public void HasCustomDecor_TrueForNonDefault_FalseForDefault()
    {
        var plain = Make("Plain", new Vector3Int(800, 400, 18));
        Assert.IsFalse(MaterialManager.HasCustomDecor(plain), "дефолтный декор — не «текстура»");

        var oak = Make("Oak", new Vector3Int(800, 400, 18), "oak");
        Assert.IsTrue(MaterialManager.HasCustomDecor(oak), "выбранный декор — текстура задана");
    }

    [Test]
    public void ApplyOwnDecor_PutsDecorColorOnRenderer()
    {
        var go = ElementFactory.CreatePart(new Vector3Int(800, 400, 18), "B", Vector3.zero);
        _spawned.Add(go);
        var e = go.GetComponent<KitchenElement>();
        e.MaterialId = "wenge";

        MaterialManager.ApplyOwnDecor(e);

        var r = go.GetComponentInChildren<MeshRenderer>();
        var expected = MaterialCatalog.Get("wenge").baseColor;
        Assert.AreEqual(expected, r.sharedMaterial.GetColor("_BaseColor"),
            "декор должен лечь на рендерер (текстура/цвет объекта включена)");
    }

    // --- _BaseColor и картинка: цвет не имеет права красить текстуру ---
    //
    // URP Lit считает альбедо как _BaseColor × _BaseMap. Если оставить в цвете
    // средний тон самой картинки (а именно он лежит в index.json как заглушка),
    // декор темнеет примерно вдвое и заодно насыщается по каналам. Ровно это и
    // случилось при переезде каталога в index.json.

    private MaterialDef TexturedDef(string id, Color placeholder, Texture2D? tex)
    {
        var def = new MaterialDef(id, id, "ЛДСП", placeholder, "some.png", 800)
        {
            tileHeightMM = 800,
            textureState = TextureState.Loading, // «запрос ушёл» — файл трогать не надо
            texture = tex,
        };
        MaterialCatalog.Register(def);
        return def;
    }

    [Test]
    public void SharedMaterial_TextureLoaded_BaseColorIsWhite()
    {
        var def = TexturedDef("tex_white_base", new Color(0.53f, 0.36f, 0.20f), Tex(8, 8));

        var mat = MaterialManager.GetSharedMaterial(def);

        Assert.AreEqual(Color.white, mat!.GetColor("_BaseColor"),
            "цвет-заглушка обязан погаснуть: иначе он домножится на картинку");
        Assert.AreSame(def.texture, mat.mainTexture);
    }

    [Test]
    public void SharedMaterial_TextureNotLoadedYet_ShowsPlaceholderColor()
    {
        // Пока картинки нет, деталь должна быть правдоподобного тона, а не белой.
        var placeholder = new Color(0.53f, 0.36f, 0.20f);
        var def = TexturedDef("tex_placeholder", placeholder, null);

        var mat = MaterialManager.GetSharedMaterial(def);

        Assert.AreEqual(placeholder, mat!.GetColor("_BaseColor"));
        Assert.IsNull(mat.mainTexture);
    }

    [Test]
    public void OnTextureArrived_ClearsPlaceholderColor()
    {
        var placeholder = new Color(0.53f, 0.36f, 0.20f);
        var def = TexturedDef("tex_late_color", placeholder, null);
        var mat = MaterialManager.GetSharedMaterial(def);
        Assert.AreEqual(placeholder, mat!.GetColor("_BaseColor"), "до загрузки — заглушка");

        def.texture = Tex(8, 8);
        MaterialManager.OnTextureArrived(def);

        Assert.AreEqual(Color.white, mat.GetColor("_BaseColor"),
            "картинка приехала — заглушка обязана погаснуть");
    }

    [Test]
    public void SharedMaterial_ColorOnlyDecor_KeepsItsColor()
    {
        // Обратная сторона правила: у декора БЕЗ картинки цвет — это весь декор,
        // белить его нельзя.
        var color = new Color(0.28f, 0.20f, 0.16f);
        var def = new MaterialDef("plain_color", "Plain", "ЛДСП", color);
        MaterialCatalog.Register(def);

        var mat = MaterialManager.GetSharedMaterial(def);

        Assert.AreEqual(color, mat!.GetColor("_BaseColor"));
        Assert.IsNull(mat.mainTexture);
    }

    // --- Каталог: регистрация поверх индекса и сброс ---

    [Test]
    public void Catalog_Register_AppearsInAll_AndGet()
    {
        int before = MaterialCatalog.All.Count;
        var def = new MaterialDef("ext_test", "Ext Test", "ЛДСП", Color.white, null, 100) { tileHeightMM = 100 };
        MaterialCatalog.Register(def);

        Assert.AreEqual(before + 1, MaterialCatalog.All.Count);
        Assert.AreSame(def, MaterialCatalog.Get("ext_test"));
    }

    [Test]
    public void Catalog_Register_SameId_Replaces_NoDuplicate()
    {
        MaterialCatalog.Register(new MaterialDef("dup", "One", "ЛДСП", Color.white));
        int after1 = MaterialCatalog.All.Count;
        MaterialCatalog.Register(new MaterialDef("dup", "Two", "ЛДСП", Color.white));

        Assert.AreEqual(after1, MaterialCatalog.All.Count, "тот же id не должен дублироваться");
        Assert.AreEqual("Two", MaterialCatalog.Get("dup").displayName);
    }

    [Test]
    public void Catalog_Register_SurvivesFirstCatalogRead()
    {
        // Регистрация ДО первого чтения каталога: если Register не дочитает индекс
        // сам, чтение файла затрёт добавленный декор.
        MaterialCatalog.Reset();
        MaterialCatalog.Register(new MaterialDef("registered_first", "First", "ЛДСП", Color.white));

        Assert.AreEqual("First", MaterialCatalog.Get("registered_first").displayName);
        Assert.Greater(MaterialCatalog.All.Count, 1, "индекс тоже должен быть прочитан");
    }

    [Test]
    public void Catalog_Reset_DropsRegistered_AndRereadsIndex()
    {
        int fromIndex = MaterialCatalog.All.Count;
        MaterialCatalog.Register(new MaterialDef("x", "X", "ЛДСП", Color.white));
        MaterialCatalog.Reset();

        Assert.AreEqual(fromIndex, MaterialCatalog.All.Count);
        Assert.AreEqual(MaterialCatalog.DefaultId, MaterialCatalog.Get("x").id,
            "снятый декор отвечает дефолтным");
    }

    [Test]
    public void Catalog_EmptyIndex_EverythingFallsBackToDefault()
    {
        MaterialCatalog.Load(new List<MaterialDef>());

        Assert.AreEqual(MaterialCatalog.DefaultId, MaterialCatalog.Get("oak").id);
        Assert.AreEqual(MaterialCatalog.DefaultId, MaterialCatalog.Default.id);
    }

    [Test]
    public void ComputeTileST_NonSquareTile_UsesSeparateAxes()
    {
        // Щит 1200×1200 на плитке 1200(Ш)×600(В) → по X 1 повтор, по Y 2 повтора.
        var st = MaterialManager.ComputeTileST(new Vector3Int(1200, 1200, 18), 1200, 600);
        Assert.AreEqual(1f, st.x, 0.0001f);
        Assert.AreEqual(2f, st.y, 0.0001f);
    }

    // --- Физ. размер плитки: высота из пропорций картинки, а не квадрат ---

    [Test]
    public void TileHeightFromAspect_KeepsImageProportions()
    {
        // Картинка 1920×853 при ширине 2000 мм → 889 мм, а не квадрат 2000.
        Assert.AreEqual(889, MaterialManager.TileHeightFromAspect(2000, 1920, 853));
    }

    [Test]
    public void TileHeightFromAspect_NoTexture_FallsBackToSquare()
    {
        Assert.AreEqual(800, MaterialManager.TileHeightFromAspect(800, 0, 0));
    }

    [Test]
    public void TileMM_ExplicitHeight_WinsOverAspect()
    {
        var def = new MaterialDef("tile_explicit", "T", "ЛДСП", Color.white, null, 1200)
        {
            tileHeightMM = 600,
            texture = Tex(64, 16),
        };
        MaterialCatalog.Register(def);

        Assert.AreEqual(new Vector2Int(1200, 600), MaterialManager.TileMM(def));
    }

    [Test]
    public void TileMM_WideTexture_DerivesHeightFromAspect()
    {
        // Широкая картинка 64×16 при ширине 1600 мм → 400 мм. Квадрат 1600×1600
        // сплющил бы рисунок вчетверо.
        var def = new MaterialDef("tile_wide", "T", "ЛДСП", Color.white, null, 1600)
        {
            texture = Tex(64, 16),
        };
        MaterialCatalog.Register(def);

        Assert.AreEqual(new Vector2Int(1600, 400), MaterialManager.TileMM(def));
    }

    [Test]
    public void TileMM_ColorOnlyDecor_IsSquare()
    {
        var def = new MaterialDef("tile_plain", "T", "ЛДСП", Color.white, null, 900);
        MaterialCatalog.Register(def);

        Assert.AreEqual(new Vector2Int(900, 900), MaterialManager.TileMM(def));
    }

    [Test]
    public void TileMM_TextureNotLoadedYet_DoesNotCacheSquare()
    {
        // Картинка ещё не приехала → высота временно равна ширине. Закэшировать
        // этот ответ значило бы оставить декор сплющенным до конца сессии.
        var def = new MaterialDef("tile_pending", "T", "ЛДСП", Color.white, "pending.png", 1600)
        {
            textureState = TextureState.Loading, // «запрос ушёл» — файл трогать не надо
        };
        MaterialCatalog.Register(def);
        Assert.AreEqual(new Vector2Int(1600, 1600), MaterialManager.TileMM(def));

        def.texture = Tex(64, 16);
        Assert.AreEqual(new Vector2Int(1600, 400), MaterialManager.TileMM(def),
            "после прихода картинки размер плитки обязан пересчитаться");
    }

    [Test]
    public void OnTextureArrived_PutsTextureOnSharedMaterial()
    {
        var def = new MaterialDef("late_tex", "Late", "ЛДСП", Color.white, "late.png", 800)
        {
            tileHeightMM = 800,
            textureState = TextureState.Loading,
        };
        MaterialCatalog.Register(def);

        var mat = MaterialManager.GetSharedMaterial(def);
        Assert.IsNotNull(mat);
        Assert.IsNull(mat!.mainTexture, "картинки ещё нет — материал чисто цветовой");

        def.texture = Tex(64, 64);
        MaterialManager.OnTextureArrived(def);

        Assert.AreSame(def.texture, mat.mainTexture,
            "материал шарится по id: картинка обязана лечь в него, а не в новый");
    }

    [Test]
    public void OnTextureArrived_ElementKeepsMaterialId_AndGetsDecor()
    {
        var go = ElementFactory.CreatePart(new Vector3Int(800, 400, 18), "B", Vector3.zero);
        _spawned.Add(go);
        var e = go.GetComponent<KitchenElement>();

        var def = new MaterialDef("late_on_board", "Late", "ЛДСП", Color.white, "late.png", 800)
        {
            tileHeightMM = 800,
            textureState = TextureState.Loading,
        };
        MaterialCatalog.Register(def);
        MaterialManager.ApplyById(e, "late_on_board");
        Assert.AreEqual("late_on_board", e.MaterialId, "декор назначен ещё до загрузки картинки");

        def.texture = Tex(64, 64);
        MaterialManager.OnTextureArrived(def);

        var r = go.GetComponentInChildren<MeshRenderer>();
        Assert.AreEqual("late_on_board", e.MaterialId, "элемент не забывает свой декор");
        Assert.AreSame(def.texture, r.sharedMaterial.mainTexture);
    }

    // --- Ресайз не растягивает декор ---

    [Test]
    public void Resize_RecomputesTiling_DecorDoesNotStretch()
    {
        var def = new MaterialDef("resize_decor", "R", "ЛДСП", Color.white, null, 800)
        {
            tileHeightMM = 400,
        };
        MaterialCatalog.Register(def);

        var go = ElementFactory.CreatePart(new Vector3Int(800, 400, 18), "B", Vector3.zero);
        _spawned.Add(go);
        var e = go.GetComponent<KitchenElement>();
        MaterialManager.Apply(e, def);

        var r = go.GetComponentInChildren<MeshRenderer>();
        var mpb = new MaterialPropertyBlock();
        r.GetPropertyBlock(mpb);
        Assert.AreEqual(new Vector4(1f, 1f, 0f, 0f), mpb.GetVector("_BaseMap_ST"));

        // Щит вдвое шире и вдвое выше — декор должен ПОВТОРИТЬСЯ 2×2,
        // а не растянуться вместе с деталью.
        e.DimensionsMM = new Vector3Int(1600, 800, 18);

        r.GetPropertyBlock(mpb);
        Assert.AreEqual(new Vector4(2f, 2f, 0f, 0f), mpb.GetVector("_BaseMap_ST"),
            "после ресайза «вырез» декора обязан пересчитаться");
    }

    // --- Физ. масштаб декора одинаков на ВСЕХ гранях (включая глубину) ---
    //
    // _BaseMap_ST один на весь рендерер и масштабирует UV по X/Y детали. Грани,
    // у которых UV идёт вдоль Z (торцы ±X и пласти ±Y), обязаны компенсировать
    // это в самом меше — иначе рисунок на них тянется пропорционально глубине.

    private const int Tile = 800;

    /// <summary>Протяжённость UV на грани с заданной нормалью (min/max по её вершинам).</summary>
    private static Vector2 UvSpan(Mesh mesh, Vector3 normal)
    {
        var normals = mesh.normals;
        var uv = mesh.uv;
        float minU = float.MaxValue, maxU = float.MinValue;
        float minV = float.MaxValue, maxV = float.MinValue;
        int found = 0;
        for (int i = 0; i < normals.Length; i++)
        {
            if (Vector3.Dot(normals[i], normal) < 0.999f) continue;
            found++;
            minU = Mathf.Min(minU, uv[i].x); maxU = Mathf.Max(maxU, uv[i].x);
            minV = Mathf.Min(minV, uv[i].y); maxV = Mathf.Max(maxV, uv[i].y);
        }
        Assert.Greater(found, 0, $"грань с нормалью {normal} в меше не найдена");
        return new Vector2(maxU - minU, maxV - minV);
    }

    /// <summary>Физ. размер куска декора, который ложится на грань, в мм:
    /// UV-протяжённость × ST × размер плитки. Должен совпадать с размером грани.</summary>
    private static Vector2 DecorSpanMM(Mesh mesh, Vector3Int dims, Vector3 normal)
    {
        var span = UvSpan(mesh, normal);
        var st = MaterialManager.ComputeTileST(dims, Tile, Tile);
        return new Vector2(span.x * st.x * Tile, span.y * st.y * Tile);
    }

    [Test]
    public void BoxMesh_FrontFace_DecorMatchesFaceSize()
    {
        // Контрольная грань: она и сейчас работает верно.
        var dims = new Vector3Int(600, 400, 500);
        var mesh = GrooveMesh.Build(dims, null);
        var mm = DecorSpanMM(mesh, dims, Vector3.forward);
        Assert.AreEqual(600f, mm.x, 0.5f, "пласть ±Z: ширина");
        Assert.AreEqual(400f, mm.y, 0.5f, "пласть ±Z: высота");
        Object.DestroyImmediate(mesh);
    }

    [Test]
    public void BoxMesh_EndFace_DecorMatchesDepth()
    {
        // Торец ±X имеет размер Z×Y — по горизонтали на нём должно уложиться
        // ровно 500 мм декора, а не 600 (ширина детали).
        var dims = new Vector3Int(600, 400, 500);
        var mesh = GrooveMesh.Build(dims, null);
        var mm = DecorSpanMM(mesh, dims, Vector3.right);
        Assert.AreEqual(500f, mm.x, 0.5f, "торец ±X: глубина");
        Assert.AreEqual(400f, mm.y, 0.5f, "торец ±X: высота");
        Object.DestroyImmediate(mesh);
    }

    [Test]
    public void BoxMesh_TopFace_DecorMatchesDepth()
    {
        // Полка 600×18×500: сверху видна грань 600×500. Именно здесь растяжение
        // по глубине заметнее всего — 18 мм декора размазывается на 500.
        var dims = new Vector3Int(600, 18, 500);
        var mesh = GrooveMesh.Build(dims, null);
        var mm = DecorSpanMM(mesh, dims, Vector3.up);
        Assert.AreEqual(600f, mm.x, 0.5f, "пласть ±Y: ширина");
        Assert.AreEqual(500f, mm.y, 0.5f, "пласть ±Y: глубина");
        Object.DestroyImmediate(mesh);
    }

    [Test]
    public void BoxMesh_DepthChange_DoesNotStretchDecor()
    {
        // Та же деталь глубже вдвое — физ. масштаб декора на торце обязан
        // остаться прежним, повторов просто станет вдвое больше.
        var shallow = new Vector3Int(600, 400, 250);
        var deep = new Vector3Int(600, 400, 500);
        var m1 = GrooveMesh.Build(shallow, null);
        var m2 = GrooveMesh.Build(deep, null);

        Assert.AreEqual(250f, DecorSpanMM(m1, shallow, Vector3.right).x, 0.5f);
        Assert.AreEqual(500f, DecorSpanMM(m2, deep, Vector3.right).x, 0.5f);

        Object.DestroyImmediate(m1);
        Object.DestroyImmediate(m2);
    }

    [Test]
    public void BoxMesh_HoleAlongY_KeepsDecorScaleAfterPermute()
    {
        // Столешница-короб: вырез режется поперёк Y, меш строится канонически
        // и переставляется по осям. UV обязаны пережить перестановку.
        var dims = new Vector3Int(1200, 40, 600);
        var holes = new List<GrooveMesh.Rect2>
        {
            new GrooveMesh.Rect2 { xMin = -0.2f, xMax = 0.2f, yMin = -0.2f, yMax = 0.2f },
        };
        var mesh = GrooveMesh.Build(dims, null, holes, holeAxis: 1);
        var mm = DecorSpanMM(mesh, dims, Vector3.up);
        Assert.AreEqual(1200f, mm.x, 1f, "верхняя пласть: ширина");
        Assert.AreEqual(600f, mm.y, 1f, "верхняя пласть: глубина");
        Object.DestroyImmediate(mesh);
    }

    [Test]
    public void Part_PlainBoard_DecorScaleEqualOnAllFaces()
    {
        // Сквозная проверка через реальную деталь: меш ей ставит ApplyDimensions.
        var dims = new Vector3Int(600, 18, 500);
        var go = ElementFactory.CreatePart(dims, "Shelf", Vector3.zero);
        _spawned.Add(go);
        var e = go.GetComponent<KitchenElement>();
        e.DimensionsMM = dims;

        var mesh = go.GetComponent<MeshFilter>().sharedMesh;
        Assert.AreEqual(500f, DecorSpanMM(mesh, dims, Vector3.up).y, 0.5f, "пласть ±Y: глубина");
        Assert.AreEqual(500f, DecorSpanMM(mesh, dims, Vector3.right).x, 0.5f, "торец ±X: глубина");
    }

    // --- Каталог читает индекс ровно один раз ---

    [Test]
    public void Catalog_AfterLoad_DoesNotRereadTheIndexOverIt()
    {
        // Флаг «индекс уже пытались прочитать» ставится и здесь тоже. Без него
        // первое же обращение к каталогу после Load перечитало бы файл и стёрло
        // всё, что положили: каталог умеет приезжать корутиной (LoadIndexAsync),
        // и собранный ею список обязан пережить ближайшее же обращение к All.
        MaterialCatalog.Load(new[]
        {
            new MaterialDef("only_one", "Единственный", "ЛДСП", Color.gray),
        });

        Assert.AreEqual(1, MaterialCatalog.All.Count,
            "каталог, собранный вручную, не должен подменяться чтением index.json");
        Assert.AreEqual("only_one", MaterialCatalog.All[0].id);
    }
}
