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
        SelectionManager.Instance = sm;
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

        Assert.AreEqual("oak", table.PrimaryMaterialId,
            "BUG: PrimaryMaterialId сбросился после снятия выделения");

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

        Assert.AreEqual("oak", table.PrimaryMaterialId,
            "BUG: PrimaryMaterialId сбросился у прямоугольного стола после снятия выделения");

        Object.DestroyImmediate(go);
    }

    // ═══════════════════════════════════════════════════════════════
    //  Ресайз стола не должен ломать геометрию
    // ═══════════════════════════════════════════════════════════════

    [Test]
    public void Table_LocalScale_MatchesDimensions()
    {
        var dims = new Vector3Int(2000, 750, 1000);
        var go = ElementFactory.CreateTable(dims, "RectScl", Vector3.zero);
        var table = go.GetComponent<TableElement>();
        Assert.IsNotNull(table);

        float toU = AppConstants.MM_TO_UNITS;
        var expectedScale = new Vector3(dims.x * toU, dims.y * toU, dims.z * toU);
        Assert.AreEqual(expectedScale, table.transform.localScale,
            "BUG: localScale не совпадает с размерами — GetFaces будет неверен");

        table.DimensionsMM = new Vector3Int(1500, 800, 600);
        var expectedScale2 = new Vector3(1500f * toU, 800f * toU, 600f * toU);
        Assert.AreEqual(expectedScale2, table.transform.localScale,
            "BUG: после ресайза localScale не совпадает с размерами");

        Object.DestroyImmediate(go);
    }

    [Test]
    public void RadiusTable_Resize_KeepsUnitScale_AndMovesTheBoundingBox()
    {
        var dims = new Vector3Int(2000, 750, 1000);
        var go = ElementFactory.CreateRadiusTable(dims, "RadiusScl", Vector3.zero);
        var table = go.GetComponent<RadiusTableElement>();
        Assert.IsNotNull(table);

        float toU = AppConstants.MM_TO_UNITS;
        Assert.AreEqual(Vector3.one, table!.transform.localScale,
            "радиусный стол строится в физических миллиметрах: масштаб на корне растянул "
            + "бы круглые торцы столешницы в эллипс");

        table.DimensionsMM = new Vector3Int(1500, 800, 600);
        var extent = AabbExtent(table);

        Assert.AreEqual(Vector3.one, table.transform.localScale,
            "и после ресайза корень остаётся единичным");
        Assert.AreEqual(1500f * toU, extent.x, 0.001f,
            "BUG: после ресайза габарит не совпадает с размерами");
        Assert.AreEqual(800f * toU, extent.y, 0.001f);
        Assert.AreEqual(600f * toU, extent.z, 0.001f);

        Object.DestroyImmediate(go);
    }

    [Test]
    public void Table_Resize_ChildrenWorldSizeMatchesDimensions()
    {
        var dims = new Vector3Int(1200, 750, 700);
        var go = ElementFactory.CreateTable(dims, "RectChild", Vector3.zero);
        var table = go.GetComponent<TableElement>();
        Assert.IsNotNull(table);

        // Проверяем что столешница (ребёнок 4) имеет правильный world-размер
        var topMr = table.transform.GetChild(4).GetComponent<MeshRenderer>();
        Assert.IsNotNull(topMr);
        var topBounds = topMr.bounds.size;
        Assert.AreEqual(1.2f, topBounds.x, 0.01f, "world ширина столешницы != 1200mm");
        Assert.AreEqual(0.03f, topBounds.y, 0.01f, "world толщина столешницы != 30mm");
        Assert.AreEqual(0.7f, topBounds.z, 0.01f, "world глубина столешницы != 700mm");

        // Ресайз
        table.DimensionsMM = new Vector3Int(1800, 800, 900);

        var topBounds2 = topMr.bounds.size;
        Assert.AreEqual(1.8f, topBounds2.x, 0.01f, "после ресайза: world ширина столешницы != 1800mm");
        Assert.AreEqual(0.03f, topBounds2.y, 0.01f, "после ресайза: world толщина столешницы != 30mm");
        Assert.AreEqual(0.9f, topBounds2.z, 0.01f, "после ресайза: world глубина столешницы != 900mm");

        Object.DestroyImmediate(go);
    }

    [Test]
    public void RadiusTable_Resize_MeshWorldSizeMatchesDimensions()
    {
        var dims = new Vector3Int(1200, 750, 700);
        var go = ElementFactory.CreateRadiusTable(dims, "RadMesh", Vector3.zero);
        var table = go.GetComponent<RadiusTableElement>();
        Assert.IsNotNull(table);

        var mr = table.GetComponent<MeshRenderer>();
        Assert.IsNotNull(mr);
        var bounds = mr.bounds.size;
        // Толщина столешницы 30mm = 0.03m (bounding box по Y — только столешница, не весь стол)
        Assert.AreEqual(0.03f, bounds.y, 0.005f, "BUG: толщина меша столешницы не 30mm — двойной масштаб");

        // Ресайз
        table.DimensionsMM = new Vector3Int(1800, 800, 900);
        var bounds2 = mr.bounds.size;
        Assert.AreEqual(0.03f, bounds2.y, 0.005f,
            "BUG: после ресайза толщина меша столешницы не 30mm");

        Object.DestroyImmediate(go);
    }

    // ═══════════════════════════════════════════════════════════════
    //  GetFaces: центры граней должны совпадать с размерами стола
    // ═══════════════════════════════════════════════════════════════

    [Test]
    public void Table_GetFaces_CentersMatchDimensions()
    {
        var dims = new Vector3Int(2000, 750, 1000);
        var go = ElementFactory.CreateTable(dims, "FaceTbl", Vector3.zero);
        var table = go.GetComponent<TableElement>();
        Assert.IsNotNull(table);

        var faces = table.GetFaces();
        Assert.AreEqual(6, faces.Length);

        var sizeM = new Vector3(
            dims.x * AppConstants.MM_TO_UNITS,
            dims.y * AppConstants.MM_TO_UNITS,
            dims.z * AppConstants.MM_TO_UNITS);
        var half = sizeM * 0.5f;

        // Face index/2 = axis. Top face (Y+, index 2): center.y должен быть на верху стола
        var topFace = faces[2];
        Assert.AreEqual(half.y, topFace.center.y, 0.001f,
            "BUG: центр верхней грани не на верху стола — ручки будут под столешницей");

        // Bottom face (Y-, index 3): center.y у низа
        var bottomFace = faces[3];
        Assert.AreEqual(-half.y, bottomFace.center.y, 0.001f);

        // Right face (X+, index 0)
        Assert.AreEqual( half.x, faces[0].center.x, 0.001f);
        // Left face (X-, index 1)
        Assert.AreEqual(-half.x, faces[1].center.x, 0.001f);

        // Front face (Z+, index 4)
        Assert.AreEqual( half.z, faces[4].center.z, 0.001f);
        // Back face (Z-, index 5)
        Assert.AreEqual(-half.z, faces[5].center.z, 0.001f);

        Object.DestroyImmediate(go);
    }

    [Test]
    public void RadiusTable_GetFaces_CentersMatchDimensions()
    {
        var dims = new Vector3Int(2000, 750, 1000);
        var go = ElementFactory.CreateRadiusTable(dims, "FaceRad", Vector3.zero);
        var table = go.GetComponent<RadiusTableElement>();
        Assert.IsNotNull(table);

        var faces = table.GetFaces();
        Assert.AreEqual(6, faces.Length);

        var sizeM = new Vector3(
            dims.x * AppConstants.MM_TO_UNITS,
            dims.y * AppConstants.MM_TO_UNITS,
            dims.z * AppConstants.MM_TO_UNITS);
        var half = sizeM * 0.5f;

        Assert.AreEqual( half.y, faces[2].center.y, 0.001f,
            "BUG: радиусный стол — центр верхней грани не на верху");
        Assert.AreEqual(-half.y, faces[3].center.y, 0.001f);
        Assert.AreEqual( half.x, faces[0].center.x, 0.001f);
        Assert.AreEqual(-half.x, faces[1].center.x, 0.001f);
        Assert.AreEqual( half.z, faces[4].center.z, 0.001f);
        Assert.AreEqual(-half.z, faces[5].center.z, 0.001f);

        Object.DestroyImmediate(go);
    }

    // ═══════════════════════════════════════════════════════════════
    //  Меш радиусного стола: winding должен смотреть наружу
    // ═══════════════════════════════════════════════════════════════

    [Test]
    public void RadiusTable_Mesh_Winding_FacesOutward()
    {
        var dims = new Vector3Int(2000, 750, 1000);
        var go = ElementFactory.CreateRadiusTable(dims, "RadWind", Vector3.zero);
        var table = go.GetComponent<RadiusTableElement>();
        Assert.IsNotNull(table);

        var mesh = table.GetComponent<MeshFilter>().sharedMesh;
        Assert.IsNotNull(mesh, "у радиусного стола должен быть меш столешницы");

        var verts = mesh.vertices;
        var normals = mesh.normals;
        var tris = mesh.triangles;
        Assert.Greater(tris.Length, 0);

        for (int t = 0; t < tris.Length; t += 3)
        {
            var a = verts[tris[t]];
            var b = verts[tris[t + 1]];
            var c = verts[tris[t + 2]];
            var winding = Vector3.Cross(b - a, c - a);
            // Стыки профиля дают вырожденные треугольники (две точки совпадают),
            // а их normalized — прецизионный мусор; отбрасываем по площади.
            if (winding.sqrMagnitude < 1e-8f) continue;

            var attr = normals[tris[t]];
            // Вершины, добавленные вырожденной парой профиля, несут нулевую
            // нормаль и попадают в соседние грани; они не про видимость.
            if (attr.sqrMagnitude < 0.5f) continue;

            Assert.Greater(Vector3.Dot(winding.normalized, attr), 0f,
                $"треугольник {t / 3}: winding смотрит против атрибутной нормали — " +
                "верхняя крышка отсекается и стол выглядит как стакан без верха");
        }

        Object.DestroyImmediate(go);
    }

    // ═══════════════════════════════════════════════════════════════
    //  Текстура столешницы должна переживать сохранение и загрузку
    // ═══════════════════════════════════════════════════════════════

    [Test]
    public void Table_SaveLoad_TabletopMaterial_RoundTrip()
    {
        var dims = new Vector3Int(1200, 750, 700);
        var go = ElementFactory.CreateTable(dims, "TblTopMtl", Vector3.zero);
        var table = go.GetComponent<TableElement>();
        Assert.IsNotNull(table);

        var oak = MaterialCatalog.Get("oak");
        var wenge = MaterialCatalog.Get("wenge");
        Assert.IsNotNull(oak, "декор 'oak' должен быть в каталоге");
        Assert.IsNotNull(wenge, "декор 'wenge' должен быть в каталоге");

        MaterialManager.ApplyPrimarySlot(table, oak);
        MaterialManager.ApplySecondarySlot(table, wenge);

        var data = ElementCapture.FromElement(table);
        Assert.AreEqual("oak", data.tabletopMaterialId,
            "столешница обязана сохраняться в tabletopMaterialId");
        Assert.AreEqual("wenge", data.legsMaterialId);

        var restored = SaveLoadManager.RestoreScene(new ProjectData(new[] { data }));
        Assert.AreEqual(1, restored.Count);
        var rt = restored[0].GetComponent<TableElement>();
        Assert.IsNotNull(rt);
        Assert.AreEqual("oak", rt.PrimaryMaterialId,
            "столешница обязана восстанавливаться из файла");
        Assert.AreEqual("wenge", rt.SecondaryMaterialId);

        foreach (var o in restored)
            if (o != null) Object.DestroyImmediate(o);
        Object.DestroyImmediate(go);
    }

    [Test]
    public void RadiusTable_SaveLoad_TabletopMaterial_RoundTrip()
    {
        var dims = new Vector3Int(1200, 750, 700);
        var go = ElementFactory.CreateRadiusTable(dims, "RadTopMtl", Vector3.zero);
        var table = go.GetComponent<RadiusTableElement>();
        Assert.IsNotNull(table);

        var oak = MaterialCatalog.Get("oak");
        var wenge = MaterialCatalog.Get("wenge");
        Assert.IsNotNull(oak, "декор 'oak' должен быть в каталоге");
        Assert.IsNotNull(wenge, "декор 'wenge' должен быть в каталоге");

        MaterialManager.ApplyPrimarySlot(table, oak);
        MaterialManager.ApplySecondarySlot(table, wenge);

        var data = ElementCapture.FromElement(table);
        Assert.AreEqual("oak", data.tabletopMaterialId,
            "столешница обязана сохраняться в tabletopMaterialId");
        Assert.AreEqual("wenge", data.legsMaterialId);

        var restored = SaveLoadManager.RestoreScene(new ProjectData(new[] { data }));
        Assert.AreEqual(1, restored.Count);
        var rt = restored[0].GetComponent<RadiusTableElement>();
        Assert.IsNotNull(rt);
        Assert.AreEqual("oak", rt.PrimaryMaterialId,
            "столешница обязана восстанавливаться из файла");
        Assert.AreEqual("wenge", rt.SecondaryMaterialId);

        foreach (var o in restored)
            if (o != null) Object.DestroyImmediate(o);
        Object.DestroyImmediate(go);
    }
}
