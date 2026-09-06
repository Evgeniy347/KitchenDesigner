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
        Assert.AreEqual(new Vector3Int(600 - Deduct, 716 - Deduct, AppConstants.GLASS_THICKNESS_MM),
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

    [Test]
    public void Build_HasUV_MatchingVertexCount()
    {
        var mesh = AssembledFacadeMesh.Build(new Vector3Int(600, 716, 18),
            AssembledFill.Blind, 4, Frame);
        try
        {
            var uv = mesh.uv;
            Assert.AreEqual(mesh.vertexCount, uv.Length,
                "UV-координат должно быть столько же, сколько вершин");
            Assert.Greater(uv.Length, 0);
        }
        finally { Object.DestroyImmediate(mesh); }
    }

    [Test]
    public void Build_UV_InRange()
    {
        var mesh = AssembledFacadeMesh.Build(new Vector3Int(600, 716, 18),
            AssembledFill.Blind, 4, Frame);
        try
        {
            var uv = mesh.uv;
            foreach (var v in uv)
            {
                Assert.GreaterOrEqual(v.x, -1e-5f, $"u={v.x} < 0");
                Assert.LessOrEqual(v.x, 1f + 1e-5f, $"u={v.x} > 1");
                Assert.GreaterOrEqual(v.y, -1e-5f, $"v={v.y} < 0");
                Assert.LessOrEqual(v.y, 1f + 1e-5f, $"v={v.y} > 1");
            }
        }
        finally { Object.DestroyImmediate(mesh); }
    }

    [Test]
    public void Build_FrontFaceUV_MatchesXY()
    {
        var mesh = AssembledFacadeMesh.Build(new Vector3Int(600, 716, 18),
            AssembledFill.Blind, 4, Frame);
        try
        {
            var verts = mesh.vertices;
            var normals = mesh.normals;
            var uv = mesh.uv;
            int frontCount = 0;
            for (int i = 0; i < verts.Length; i++)
            {
                if (normals[i].z <= 0.9f) continue;
                frontCount++;
                var v = verts[i];
                Assert.AreEqual(v.x + 0.5f, uv[i].x, 1e-4f,
                    $"Фронтальная вершина [{i}] uv.x (={uv[i].x:F4}) != x+0.5 (={v.x+0.5f:F4})");
                Assert.AreEqual(v.y + 0.5f, uv[i].y, 1e-4f,
                    $"Фронтальная вершина [{i}] uv.y (={uv[i].y:F4}) != y+0.5 (={v.y+0.5f:F4})");
            }
            Assert.Greater(frontCount, 0, "должна быть хотя бы одна фронтальная вершина");
        }
        finally { Object.DestroyImmediate(mesh); }
    }

    [Test]
    public void Build_SideFaceUV_MatchesZY()
    {
        var mesh = AssembledFacadeMesh.Build(new Vector3Int(600, 716, 18),
            AssembledFill.Blind, 4, Frame);
        try
        {
            var verts = mesh.vertices;
            var normals = mesh.normals;
            var uv = mesh.uv;
            int sideCount = 0;
            for (int i = 0; i < verts.Length; i++)
            {
                if (Mathf.Abs(normals[i].x) <= 0.9f) continue;
                sideCount++;
                var v = verts[i];
                Assert.AreEqual(v.z + 0.5f, uv[i].x, 1e-4f,
                    $"Боковая вершина [{i}] uv.x (={uv[i].x:F4}) != z+0.5 (={v.z+0.5f:F4})");
                Assert.AreEqual(v.y + 0.5f, uv[i].y, 1e-4f,
                    $"Боковая вершина [{i}] uv.y (={uv[i].y:F4}) != y+0.5 (={v.y+0.5f:F4})");
            }
            Assert.Greater(sideCount, 0, "должна быть хотя бы одна боковая вершина");
        }
        finally { Object.DestroyImmediate(mesh); }
    }

    [Test]
    public void Build_TopFaceUV_MatchesXZ()
    {
        var mesh = AssembledFacadeMesh.Build(new Vector3Int(600, 716, 18),
            AssembledFill.Blind, 4, Frame);
        try
        {
            var verts = mesh.vertices;
            var normals = mesh.normals;
            var uv = mesh.uv;
            int topCount = 0;
            for (int i = 0; i < verts.Length; i++)
            {
                if (normals[i].y <= 0.9f) continue;
                topCount++;
                var v = verts[i];
                Assert.AreEqual(v.x + 0.5f, uv[i].x, 1e-4f,
                    $"Верхняя вершина [{i}] uv.x (={uv[i].x:F4}) != x+0.5 (={v.x+0.5f:F4})");
                Assert.AreEqual(v.z + 0.5f, uv[i].y, 1e-4f,
                    $"Верхняя вершина [{i}] uv.y (={uv[i].y:F4}) != z+0.5 (={v.z+0.5f:F4})");
            }
            Assert.Greater(topCount, 0, "должна быть хотя бы одна верхняя вершина");
        }
        finally { Object.DestroyImmediate(mesh); }
    }

    [Test]
    public void Build_Glass_HasUV()
    {
        var mesh = AssembledFacadeMesh.Build(new Vector3Int(600, 716, 18),
            AssembledFill.Glass, 4, Frame);
        try
        {
            Assert.AreEqual(mesh.vertexCount, mesh.uv.Length, "Glass: UV count = vertex count");
            Assert.Greater(mesh.uv.Length, 0);
        }
        finally { Object.DestroyImmediate(mesh); }
    }

    [Test]
    public void Build_Open_HasUV()
    {
        var mesh = AssembledFacadeMesh.Build(new Vector3Int(600, 716, 18),
            AssembledFill.Open, 4, Frame);
        try
        {
            Assert.AreEqual(mesh.vertexCount, mesh.uv.Length, "Open: UV count = vertex count");
            Assert.Greater(mesh.uv.Length, 0);
        }
        finally { Object.DestroyImmediate(mesh); }
    }

    [Test]
    public void Build_NoGrooves_HasUV()
    {
        var mesh = AssembledFacadeMesh.Build(new Vector3Int(600, 716, 18),
            AssembledFill.Blind, 0, Frame);
        try
        {
            Assert.AreEqual(mesh.vertexCount, mesh.uv.Length,
                "Без фрезеровок: UV count = vertex count");
            Assert.Greater(mesh.uv.Length, 0);
            var uv = mesh.uv;
            foreach (var v in uv)
            {
                Assert.GreaterOrEqual(v.x, -1e-5f);
                Assert.LessOrEqual(v.x, 1f + 1e-5f);
            }
        }
        finally { Object.DestroyImmediate(mesh); }
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
        var data = ElementCapture.FromElement(af);

        Assert.IsTrue(data.assembled, "флаг сборного сохранён");
        Assert.IsTrue(data.isFacade, "сборный — тоже фасад");
        Assert.AreEqual((int)AssembledFill.Blind, data.assembledFill);
        Assert.AreEqual(new[] { 600, 716, 18 }, data.dimensionsMM);
    }
}
