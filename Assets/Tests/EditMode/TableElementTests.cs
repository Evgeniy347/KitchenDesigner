using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using KitchenDesigner.Core;

public class TableElementTests
{
    private SelectionManager? _selection;

    [SetUp]
    public void SetUp()
    {
        LogAssert.ignoreFailingMessages = true;
        _selection = new GameObject("SelMgr").AddComponent<SelectionManager>();
        SetSelectionInstance(_selection);
    }

    [TearDown]
    public void TearDown()
    {
        if (_selection != null)
            Object.DestroyImmediate(_selection.gameObject);
        SetSelectionInstance(null);
        LogAssert.ignoreFailingMessages = false;
    }

    private static void SetSelectionInstance(SelectionManager? sm)
    {
        var prop = typeof(SelectionManager).GetProperty("Instance",
            BindingFlags.Public | BindingFlags.Static);
        prop!.GetSetMethod(nonPublic: true)!.Invoke(null, new object?[] { sm });
    }
    private static Vector3 AabbExtent(KitchenElement e)
    {
        var v = e.GetVertices();
        Vector3 min = v[0], max = v[0];
        for (int i = 1; i < v.Length; i++)
        {
            min = Vector3.Min(min, v[i]);
            max = Vector3.Max(max, v[i]);
        }
        return max - min;
    }

    private static Vector3 AabbCenter(KitchenElement e)
    {
        var v = e.GetVertices();
        Vector3 min = v[0], max = v[0];
        for (int i = 1; i < v.Length; i++)
        {
            min = Vector3.Min(min, v[i]);
            max = Vector3.Max(max, v[i]);
        }
        return (min + max) * 0.5f;
    }

    [Test]
    public void Table_BoundingBox_MatchesDimensions()
    {
        var dims = new Vector3Int(2000, 750, 1000);
        var go = ElementFactory.CreateTable(dims, "TestTable", Vector3.zero);
        var table = go.GetComponent<TableElement>();
        Assert.IsNotNull(table);

        var extent = AabbExtent(table);
        Assert.AreEqual(2.0f, extent.x, 0.001f, "ширина 2000мм = 2м");
        Assert.AreEqual(0.75f, extent.y, 0.001f, "высота 750мм = 0.75м");
        Assert.AreEqual(1.0f, extent.z, 0.001f, "глубина 1000мм = 1м");

        Object.DestroyImmediate(go);
    }

    [Test]
    public void RadiusTable_BoundingBox_MatchesDimensions()
    {
        var dims = new Vector3Int(2000, 750, 1000);
        var go = ElementFactory.CreateRadiusTable(dims, "TestRadiusTable", Vector3.zero);
        var table = go.GetComponent<RadiusTableElement>();
        Assert.IsNotNull(table);

        var extent = AabbExtent(table);
        Assert.AreEqual(2.0f, extent.x, 0.001f, "ширина 2000мм = 2м");
        Assert.AreEqual(0.75f, extent.y, 0.001f, "высота 750мм = 0.75м");
        Assert.AreEqual(1.0f, extent.z, 0.001f, "глубина 1000мм = 1м");

        Object.DestroyImmediate(go);
    }

    [Test]
    public void RadiusTable_FloorSnap_CenterAboveFloor()
    {
        var dims = new Vector3Int(2000, 750, 1000);
        float posY = dims.y * 0.5f * AppConstants.MM_TO_UNITS;
        var go = ElementFactory.CreateRadiusTable(dims, "TestRadiusTable", new Vector3(0, posY, 0));
        var table = go.GetComponent<RadiusTableElement>();
        Assert.IsNotNull(table);

        var center = AabbCenter(table);
        Assert.AreEqual(posY, center.y, 0.001f, "центр bounding box на posY (пол)");

        var extent = AabbExtent(table);
        var min = center - extent * 0.5f;
        Assert.AreEqual(0f, min.y, 0.001f, "низ bounding box на y=0 (пол)");

        Object.DestroyImmediate(go);
    }

    [Test]
    public void Table_FloorSnap_CenterAboveFloor()
    {
        var dims = new Vector3Int(2000, 750, 1000);
        float posY = dims.y * 0.5f * AppConstants.MM_TO_UNITS;
        var go = ElementFactory.CreateTable(dims, "TestTable", new Vector3(0, posY, 0));
        var table = go.GetComponent<TableElement>();
        Assert.IsNotNull(table);

        var center = AabbCenter(table);
        Assert.AreEqual(posY, center.y, 0.001f, "центр bounding box на posY (пол)");

        var extent = AabbExtent(table);
        var min = center - extent * 0.5f;
        Assert.AreEqual(0f, min.y, 0.001f, "низ bounding box на y=0 (пол)");

        Object.DestroyImmediate(go);
    }

    [Test]
    public void RadiusTable_GetFaces_ReturnsSixFaces()
    {
        var dims = new Vector3Int(2000, 750, 1000);
        var go = ElementFactory.CreateRadiusTable(dims, "TestRadiusTable", Vector3.zero);
        var table = go.GetComponent<RadiusTableElement>();
        Assert.IsNotNull(table);

        var faces = table.GetFaces();
        Assert.AreEqual(6, faces.Length, "должно быть 6 граней");

        Object.DestroyImmediate(go);
    }

    [Test]
    public void RadiusTable_LegInset_Default100()
    {
        var dims = new Vector3Int(2000, 750, 1000);
        var go = ElementFactory.CreateRadiusTable(dims, "TestRadiusTable", Vector3.zero);
        var table = go.GetComponent<RadiusTableElement>();
        Assert.IsNotNull(table);
        Assert.AreEqual(100, table.LegInsetMM);

        Object.DestroyImmediate(go);
    }

    // ═══════════════════════════════════════════════════════════════
    //  Материал не должен сбрасываться при снятии выделения
    // ═══════════════════════════════════════════════════════════════

    [Test]
    public void RadiusTable_Material_KeptAfterDeselect_WhenChangedViaApply()
    {
        var dims = new Vector3Int(1200, 750, 700);
        var go = ElementFactory.CreateRadiusTable(dims, "RadiusTbl", Vector3.zero);
        var table = go.GetComponent<RadiusTableElement>();
        Assert.IsNotNull(table);

        var renderer = table.GetComponent<MeshRenderer>();
        Assert.IsNotNull(renderer, "у радиусного стола должен быть корневой MeshRenderer");

        var oakDef = MaterialCatalog.Get("oak");
        Assert.IsNotNull(oakDef, "декор 'oak' должен быть в каталоге");
        var oakMat = MaterialManager.GetSharedMaterial(oakDef);
        Assert.IsNotNull(oakMat);

        // Выделяем стол
        _selection!.Select(table);
        Assert.IsTrue(_selection.IsSelected(table));

        // Меняем текстуру (путь MCP: edit_elements → MaterialManager.Apply)
        MaterialManager.Apply(table, oakDef);

        // Снимаем выделение — в RestoreMaterial не должно перетереться
        _selection.DeselectAll();
        Assert.IsFalse(_selection.IsSelected(table));

        // Текстура на корневом рендерере не должна сброситься
        Assert.AreEqual(oakMat, renderer.sharedMaterial,
            "BUG: после снятия выделения радиусного стола текстура сбросилась в дефолтную");

        Assert.AreEqual("oak", table.TabletopMaterialId,
            "BUG: TabletopMaterialId сбросился после снятия выделения");

        Object.DestroyImmediate(go);
    }

    [Test]
    public void Table_Material_KeptAfterDeselect_WhenChangedViaApply()
    {
        var dims = new Vector3Int(1200, 750, 700);
        var go = ElementFactory.CreateTable(dims, "RectTbl", Vector3.zero);
        var table = go.GetComponent<TableElement>();
        Assert.IsNotNull(table);

        var oakDef = MaterialCatalog.Get("oak");
        Assert.IsNotNull(oakDef, "декор 'oak' должен быть в каталоге");

        Assert.IsNull(table.GetComponent<MeshRenderer>(),
            "у прямоугольного стола НЕ должно быть корневого MeshRenderer");

        // Выделяем стол
        _selection!.Select(table);
        Assert.IsTrue(_selection.IsSelected(table));

        // Меняем текстуру (путь MCP)
        MaterialManager.Apply(table, oakDef);

        // Снимаем выделение
        _selection.DeselectAll();
        Assert.IsFalse(_selection.IsSelected(table));

        Assert.AreEqual("oak", table.TabletopMaterialId,
            "BUG: TabletopMaterialId сбросился у прямоугольного стола после снятия выделения");

        Object.DestroyImmediate(go);
    }

    // ═══════════════════════════════════════════════════════════════
    //  Ресайз стола не должен ломать геометрию (двойное масштабирование)
    // ═══════════════════════════════════════════════════════════════

    [Test]
    public void RadiusTable_LocalScale_IsOne_AfterResize()
    {
        var go = ElementFactory.CreateRadiusTable(
            new Vector3Int(2000, 750, 1000), "RadiusScl", Vector3.zero);
        var table = go.GetComponent<RadiusTableElement>();
        Assert.IsNotNull(table);

        Assert.AreEqual(Vector3.one, table.transform.localScale,
            "BUG: после создания радиусного стола localScale != (1,1,1) — " +
            "меш двойным масштабированием растянут");

        // Ресайз
        table.DimensionsMM = new Vector3Int(1500, 800, 600);

        Assert.AreEqual(Vector3.one, table.transform.localScale,
            "BUG: после ресайза радиусного стола localScale != (1,1,1) — " +
            "меш двойным масштабированием растянут");

        Object.DestroyImmediate(go);
    }

    [Test]
    public void Table_LocalScale_IsOne_AfterResize()
    {
        var go = ElementFactory.CreateTable(
            new Vector3Int(2000, 750, 1000), "RectScl", Vector3.zero);
        var table = go.GetComponent<TableElement>();
        Assert.IsNotNull(table);

        Assert.AreEqual(Vector3.one, table.transform.localScale,
            "BUG: после создания прямоугольного стола localScale != (1,1,1) — " +
            "дочерние кубы двойным масштабированием растянуты");

        table.DimensionsMM = new Vector3Int(1500, 800, 600);

        Assert.AreEqual(Vector3.one, table.transform.localScale,
            "BUG: после ресайза прямоугольного стола localScale != (1,1,1) — " +
            "дочерние кубы двойным масштабированием растянуты");

        Object.DestroyImmediate(go);
    }
}
