using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

/// <summary>Дорого оплаченное знание про декоры, снятое из комментариев
/// Core/Materials. Правило проекта: декор ТАЙЛИТСЯ, он никогда не растягивается,
/// а физический размер плитки умеет назвать только MaterialManager.TileMM —
/// единственный, кто дотягивается до картинки. Здесь же — почему материал один
/// на декор, а «вырез» персональный.</summary>
public class DecorTilingKnowledgeTests
{
    private static readonly int BaseMapST = Shader.PropertyToID("_BaseMap_ST");

    private readonly List<GameObject> _spawned = new List<GameObject>();
    private readonly List<Texture2D> _textures = new List<Texture2D>();

    private Texture2D Tex(int w, int h)
    {
        var t = new Texture2D(w, h);
        _textures.Add(t);
        return t;
    }

    private KitchenElement Part(Vector3Int dims, string name)
    {
        var go = ElementFactory.CreatePart(dims, name, Vector3.zero);
        _spawned.Add(go);
        return go.GetComponent<KitchenElement>();
    }

    private static MeshRenderer RendererOf(KitchenElement element)
    {
        var r = element.DecorRenderer != null
            ? element.DecorRenderer
            : element.GetComponentInChildren<MeshRenderer>();
        Assert.IsNotNull(r, "у детали нет рендерера декора — проверять нечего");
        return r!;
    }

    private static Vector4 TilingOf(KitchenElement element)
    {
        var block = new MaterialPropertyBlock();
        RendererOf(element).GetPropertyBlock(block);
        return block.GetVector(BaseMapST);
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
        PartRegistry.Clear();
    }

    [Test]
    public void MaterialDef_OwnTileHeight_DoesNotSeeTheImage_UnlikeTileMM()
    {
        var def = new MaterialDef("wide_decor", "Широкий", "ЛДСП", Color.white, "wide.jpg", 800);
        def.texture = Tex(1920, 853);

        Assert.AreEqual(800, def.TileHeightMmWithoutLookingAtTheImage,
            "MaterialDef картинку не видит и честно отвечает квадратом");

        var tile = MaterialManager.TileMM(def);
        Assert.AreEqual(800, tile.x);
        Assert.AreEqual(355, tile.y,
            "1920×853 при ширине 800 мм — это 355 мм высоты. Взять сюда квадрат "
            + "значит сплющить рисунок в 2,25 раза");

        Assert.AreNotEqual(def.TileHeightMmWithoutLookingAtTheImage, tile.y,
            "ВТОРАЯ ОСЬ — самое дорогое место во всём декоре: взятая не оттуда, она "
            + "тайлит рисунок в разы плотнее нужного и делает это молча, на каждом "
            + "проекте сразу. Единственный, кому можно её доверять, — MaterialManager.TileMM");
    }

    [Test]
    public void SharedMaterial_TwoPartsOneDecor_ShareExactlyOneMaterial()
    {
        var def = new MaterialDef("shared_decor", "Ш", "ЛДСП", Color.white, null, 800)
        {
            tileHeightMM = 800,
        };
        MaterialCatalog.Register(def);

        var a = Part(new Vector3Int(800, 800, 18), "A");
        var b = Part(new Vector3Int(1600, 800, 18), "B");
        MaterialManager.Apply(a, def);
        MaterialManager.Apply(b, def);

        Assert.AreSame(RendererOf(a).sharedMaterial, RendererOf(b).sharedMaterial,
            "материал шарится по id декора — свой Material на деталь означает свой "
            + "батч на деталь, а деталей в проекте сотни");
    }

    [Test]
    public void Tiling_IsPerElement_AndNeverTouchesTheSharedMaterial()
    {
        var def = new MaterialDef("st_decor", "Ш", "ЛДСП", Color.white, null, 800)
        {
            tileHeightMM = 800,
        };
        MaterialCatalog.Register(def);

        var a = Part(new Vector3Int(800, 800, 18), "A");
        var b = Part(new Vector3Int(2400, 1600, 18), "B");
        MaterialManager.Apply(a, def);
        MaterialManager.Apply(b, def);

        Assert.AreEqual(new Vector4(1f, 1f, 0f, 0f), TilingOf(a));
        Assert.AreEqual(new Vector4(3f, 2f, 0f, 0f), TilingOf(b),
            "деталь 2400×1600 на плитке 800 показывает её 3×2 раза");

        var shared = MaterialManager.GetSharedMaterial(def);
        Assert.IsNotNull(shared);
        Assert.AreEqual(new Vector4(1f, 1f, 0f, 0f), shared!.GetVector(BaseMapST),
            "«вырез» живёт в MaterialPropertyBlock. Записать его в общий материал "
            + "значит отдать всем деталям с этим декором масштаб последней из них");
    }

    [Test]
    public void Tiling_OfOneElement_DoesNotLeakIntoTheNeighbour()
    {
        var def = new MaterialDef("leak_decor", "Ш", "ЛДСП", Color.white, null, 800)
        {
            tileHeightMM = 800,
        };
        MaterialCatalog.Register(def);

        var a = Part(new Vector3Int(800, 800, 18), "A");
        var b = Part(new Vector3Int(2400, 1600, 18), "B");
        MaterialManager.Apply(a, def);
        MaterialManager.Apply(b, def);

        MaterialManager.RefreshTiling(b, def);
        MaterialManager.RefreshTiling(a, def);

        Assert.AreEqual(new Vector4(3f, 2f, 0f, 0f), TilingOf(b),
            "MaterialPropertyBlock переиспользуется один на весь менеджер — ресайз "
            + "зовёт RefreshTiling каждый кадр, и своя аллокация там ни к чему. "
            + "Но переиспользованный блок обязан перечитываться у КАЖДОГО рендерера: "
            + "иначе соседняя деталь получает чужой масштаб");
        Assert.AreEqual(new Vector4(1f, 1f, 0f, 0f), TilingOf(a));
    }

    [Test]
    public void ResolveTexture_OfANotYetLoadedDecor_IsItselfTheRequest()
    {
        var def = MaterialCatalog.Get("oak");
        Assume.That(def.HasTextureFile, Is.True, "у дуба есть файл картинки");
        Assume.That(def.textureState, Is.EqualTo(TextureState.NotRequested),
            "картинка ещё не запрошена");

        var tex = MaterialManager.ResolveTexture(def);

        Assert.IsNotNull(tex,
            "картинки грузятся лениво, и обращение к декору — единственный повод "
            + "их затребовать. Если ResolveTexture перестанет звать TextureLibrary, "
            + "декор навсегда останется на цвете-заглушке");
        Assert.AreEqual(TextureState.Loaded, def.textureState);
    }

    [Test]
    public void ApplyById_DecorMissingFromTheCatalog_ShowsGreyButKeepsTheSavedId()
    {
        MaterialCatalog.Load(new[]
        {
            new MaterialDef(MaterialCatalog.DefaultId, "Серый", "ЛДСП", Color.gray),
        });

        var e = Part(new Vector3Int(600, 400, 18), "Board");
        MaterialManager.ApplyById(e, "decor_that_is_not_here_yet");

        Assert.AreEqual("decor_that_is_not_here_yet", e.MaterialId,
            "декора нет в каталоге (в WebGL индекс приезжает корутиной уже после "
            + "восстановления сцены; папку текстур могли временно подменить). "
            + "Показываем серый, но ЗАПОМНЕННЫЙ id не теряем: потеряв его, "
            + "следующее сохранение запишет вместо выбранной текстуры «default» — "
            + "перезапуск молча сотрёт выбор пользователя");
    }

    [Test]
    public void ConfigureTexture_TilesSeamlessly_AndSurvivesGrazingAngles()
    {
        var tex = Tex(64, 64);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Point;
        tex.anisoLevel = 0;

        MaterialManager.ConfigureTexture(tex);

        Assert.AreEqual(TextureWrapMode.Repeat, tex.wrapMode,
            "щит крупнее плитки просто повторяет её — на Clamp вместо повтора "
            + "растянулся бы крайний пиксель");
        Assert.AreEqual(FilterMode.Trilinear, tex.filterMode);
        Assert.AreEqual(8, tex.anisoLevel,
            "столешница и пол уходят от камеры почти в плоскость. На bilinear и "
            + "анизотропии 1 дальняя половина смазывается в кашу");
    }
}
