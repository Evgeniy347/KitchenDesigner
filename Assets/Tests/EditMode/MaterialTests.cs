using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

/// <summary>Ядро применения декоров: каталог, «вырез» текстуры под размер щита
/// (фиксированный физ. масштаб), сохранение materialId, спецификация по материалу.</summary>
public class MaterialTests
{
    private readonly List<GameObject> _spawned = new List<GameObject>();

    private KitchenElement Make(string name, Vector3Int dims, string materialId = null)
    {
        var go = new GameObject(name);
        var e = go.AddComponent<KitchenElement>();
        e.BoardName = name;
        e.DimensionsMM = dims;
        if (materialId != null) e.MaterialId = materialId;
        _spawned.Add(go);
        return e;
    }

    [TearDown]
    public void Teardown()
    {
        MaterialManager.ClearCache();
        foreach (var go in _spawned) if (go != null) Object.DestroyImmediate(go);
        _spawned.Clear();
        foreach (var e in Object.FindObjectsByType<KitchenElement>())
            if (e != null) Object.DestroyImmediate(e.gameObject);
    }

    // --- Каталог ---

    [Test]
    public void Catalog_HasDefault_AndKnownDecors()
    {
        Assert.IsNotNull(MaterialCatalog.Default);
        Assert.AreEqual(MaterialCatalog.DefaultId, MaterialCatalog.Default.id);
        Assert.AreEqual("oak", MaterialCatalog.Get("oak").id);
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

        Assert.AreEqual(1, data.elements.Length);
        Assert.AreEqual("wenge", data.elements[0].materialId);
    }

    [Test]
    public void MaterialId_DefaultsToDefault_WhenUnset()
    {
        var e = Make("Plain", new Vector3Int(800, 400, 18));
        Assert.AreEqual(MaterialCatalog.DefaultId, e.MaterialId);
        Assert.AreEqual(MaterialCatalog.DefaultId, ElementData.FromElement(e).materialId);
    }

    // --- Спецификация группирует по материалу ---

    [Test]
    public void Spec_SameSizeDifferentMaterial_SeparateLines()
    {
        Make("Board", new Vector3Int(800, 400, 18), "oak");
        Make("Board", new Vector3Int(800, 400, 18), "oak");
        Make("Board", new Vector3Int(800, 400, 18), "wenge");

        var spec = SpecificationManager.Build(_spawned.ConvertAll(g => g.GetComponent<KitchenElement>()));

        Assert.AreEqual(2, spec.lines.Count, "одинаковый размер, но разный декор → разные строки");
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
        var go = ElementFactory.CreateBoard(new Vector3Int(800, 400, 18), "B", Vector3.zero);
        _spawned.Add(go);
        var e = go.GetComponent<KitchenElement>();

        MaterialManager.ApplyById(e, "oak");
        Assert.AreEqual("oak", e.MaterialId);
    }
}
