using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

// Геометрия и раскладка сборного (рамочного) фасада — чистые функции.
public class AssembledFacadeMeshTests
{
    private const int Frame = AppConstants.ASSEMBLED_FRAME_MM;   // 100
    private const int Deduct = AppConstants.ASSEMBLED_GLASS_DEDUCT_MM; // 180

    [Test]
    public void Fraction_IsFrameOverSide()
    {
        Assert.AreEqual(100f / 600f, AssembledFacadeMesh.Fraction(600, Frame), 1e-5f);
        Assert.AreEqual(100f / 1000f, AssembledFacadeMesh.Fraction(1000, Frame), 1e-5f);
    }

    [Test]
    public void Fraction_ClampsForTinyOrHugeFrame()
    {
        Assert.LessOrEqual(AssembledFacadeMesh.Fraction(150, Frame), 0.48f, "не съедает весь фасад");
        Assert.GreaterOrEqual(AssembledFacadeMesh.Fraction(100000, Frame), 0.02f);
    }

    [Test]
    public void Parts_Blind_FrameAndPanel()
    {
        // 600×716×18: B=L−2A=400, C=H−2A=516.
        var parts = AssembledFacadeMesh.ComputeParts(new Vector3Int(600, 716, 18),
            AssembledFill.Blind, Frame, Deduct);

        Assert.AreEqual(5, parts.Count);
        AssertPartCount(parts, "Стойка", new Vector3Int(100, 716, 18), 2);
        AssertPartCount(parts, "Перекладина", new Vector3Int(400, 100, 18), 2);
        AssertPartCount(parts, "Панель", new Vector3Int(400, 516, 18), 1);
    }

    [Test]
    public void Parts_Glass_UsesDeduction_AndGlassMaterial()
    {
        var parts = AssembledFacadeMesh.ComputeParts(new Vector3Int(600, 716, 18),
            AssembledFill.Glass, Frame, Deduct);

        Assert.AreEqual(5, parts.Count);
        var glass = parts.Find(p => p.suffix == "Стекло");
        Assert.AreEqual(new Vector3Int(600 - Deduct, 716 - Deduct, AppConstants.ASSEMBLED_GLASS_THICKNESS_MM),
            glass.dimsMM, "стекло по формуле L−180 × H−180");
        Assert.AreEqual("Стекло", glass.materialKind);
    }

    [Test]
    public void Parts_Open_HasNoInsert()
    {
        var parts = AssembledFacadeMesh.ComputeParts(new Vector3Int(600, 716, 18),
            AssembledFill.Open, Frame, Deduct);
        Assert.AreEqual(4, parts.Count, "только рамка (2 стойки + 2 перекладины)");
    }

    [Test]
    public void Build_HasTwoSubmeshes_WithinUnitCube()
    {
        var mesh = AssembledFacadeMesh.Build(new Vector3Int(600, 716, 18),
            AssembledFill.Blind, 4, Frame);
        try
        {
            Assert.AreEqual(2, mesh.subMeshCount, "0=рамка/панель, 1=фрезеровки");
            Assert.Greater(mesh.vertexCount, 0);
            Assert.LessOrEqual(mesh.bounds.extents.x, 0.5f + 1e-3f);
            Assert.LessOrEqual(mesh.bounds.extents.y, 0.5f + 1e-3f);
            Assert.LessOrEqual(mesh.bounds.extents.z, 0.5f + 1e-3f);
        }
        finally { Object.DestroyImmediate(mesh); }
    }

    [Test]
    public void Build_Open_HasNoPanelGeometry_ButStillTwoSubmeshes()
    {
        var blind = AssembledFacadeMesh.Build(new Vector3Int(600, 716, 18), AssembledFill.Blind, 0, Frame);
        var open = AssembledFacadeMesh.Build(new Vector3Int(600, 716, 18), AssembledFill.Open, 0, Frame);
        try
        {
            Assert.Greater(blind.vertexCount, open.vertexCount, "у глухого есть панель — вершин больше");
        }
        finally { Object.DestroyImmediate(blind); Object.DestroyImmediate(open); }
    }

    private static void AssertPartCount(List<AssembledFacadeMesh.Part> parts, string suffix,
        Vector3Int dims, int expected)
    {
        int n = parts.FindAll(p => p.suffix == suffix && p.dimsMM == dims).Count;
        Assert.AreEqual(expected, n, $"{suffix} {dims} должно быть {expected} шт.");
    }
}

// Интеграция типа-элемента: спецификация и сохранение.
public class AssembledFacadeElementTests
{
    private readonly List<GameObject> _spawned = new List<GameObject>();

    private AssembledFacadeElement MakeAssembled(Vector3Int dims)
    {
        // Без примитива: MeshFilter отсутствует → RebuildMesh безопасно пропускается
        // (шейдеры/материалы не трогаются), а логика размеров/спеки доступна.
        var go = new GameObject("Assembled");
        var af = go.AddComponent<AssembledFacadeElement>();
        af.PartName = "Сборный";
        af.DimensionsMM = dims;
        _spawned.Add(go);
        return af;
    }

    [TearDown]
    public void Teardown()
    {
        foreach (var go in _spawned) if (go != null) Object.DestroyImmediate(go);
        _spawned.Clear();
    }

    [Test]
    public void Specification_DecomposesIntoParts()
    {
        var af = MakeAssembled(new Vector3Int(600, 716, 18)); // Blind по умолчанию
        var spec = SpecificationManager.Build(new KitchenElement[] { af });

        // 3 уникальные детали: Стойка(×2), Перекладина(×2), Панель(×1) = totalCount 5.
        Assert.AreEqual(5, spec.totalCount);
        Assert.IsTrue(spec.lines.Exists(l => l.name == "Сборный·Стойка" && l.count == 2));
        Assert.IsTrue(spec.lines.Exists(l => l.name == "Сборный·Перекладина" && l.count == 2));
        Assert.IsTrue(spec.lines.Exists(l => l.name == "Сборный·Панель" && l.count == 1));
    }

    [Test]
    public void Save_PersistsAssembledTypeAndFill()
    {
        var af = MakeAssembled(new Vector3Int(600, 716, 18));
        var data = ElementData.FromElement(af);

        Assert.IsTrue(data.assembled, "флаг сборного сохранён");
        Assert.IsTrue(data.isFacade, "сборный — тоже фасад");
        Assert.AreEqual((int)AssembledFill.Blind, data.assembledFill);
        Assert.AreEqual(new[] { 600, 716, 18 }, data.dimensionsMM);
    }
}
