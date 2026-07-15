using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

public class RadialShelfTests
{
    private GameObject? _go;

    [TearDown]
    public void Teardown()
    {
        if (_go != null) Object.DestroyImmediate(_go);
        PartRegistry.Clear();
        MaterialCatalog.ClearDynamic();
    }

    [Test]
    public void CreateRadialShelf_HasRadiusAndThickness()
    {
        var shelf = CreateShelf("R1", 400, 18, Vector3.zero);

        Assert.AreEqual(400, shelf.Radius);
        Assert.AreEqual(new Vector3Int(400, 18, 400), shelf.DimensionsMM);
        Assert.IsTrue(shelf.GetComponent<MeshCollider>() != null);
    }

    [Test]
    public void RadiusSetter_UpdatesDimensions()
    {
        var shelf = CreateShelf("R2", 300, 18, Vector3.zero);
        shelf.Radius = 500;

        Assert.AreEqual(500, shelf.Radius);
        Assert.AreEqual(new Vector3Int(500, 18, 500), shelf.DimensionsMM);
    }

    [Test]
    public void BuildMesh_BoundsMatchRadiusAndThickness()
    {
        var shelf = CreateShelf("R3", 300, 18, Vector3.zero);
        var mesh = shelf.GetComponent<MeshFilter>().sharedMesh;

        Assert.IsNotNull(mesh);
        Assert.Greater(mesh.vertexCount, 24, "mesh should be radial, not default cube");
        Assert.AreEqual(300f * AppConstants.MM_TO_UNITS, mesh.bounds.size.x, 1e-4f);
        Assert.AreEqual(18f * AppConstants.MM_TO_UNITS, mesh.bounds.size.y, 1e-4f);
        Assert.AreEqual(300f * AppConstants.MM_TO_UNITS, mesh.bounds.size.z, 1e-4f);
    }

    [Test]
    public void ApplyDimensions_LocalScaleIsUnity()
    {
        var shelf = CreateShelf("R_SCALE", 400, 18, Vector3.zero);
        var ls = shelf.transform.localScale;
        Assert.AreEqual(1f, ls.x, 1e-6f, "localScale.x must be 1");
        Assert.AreEqual(1f, ls.y, 1e-6f, "localScale.y must be 1");
        Assert.AreEqual(1f, ls.z, 1e-6f, "localScale.z must be 1");
    }

    [Test]
    public void EffectiveScale_ReturnsWorldUnitSize()
    {
        var shelf = CreateShelf("R_ESCALE", 500, 25, Vector3.zero);
        // EffectiveScale is protected; verify indirectly via GetVertices bounding box.
        var verts = shelf.GetVertices();
        float minX = float.MaxValue, maxX = float.MinValue;
        float minY = float.MaxValue, maxY = float.MinValue;
        float minZ = float.MaxValue, maxZ = float.MinValue;
        foreach (var v in verts)
        {
            if (v.x < minX) minX = v.x; if (v.x > maxX) maxX = v.x;
            if (v.y < minY) minY = v.y; if (v.y > maxY) maxY = v.y;
            if (v.z < minZ) minZ = v.z; if (v.z > maxZ) maxZ = v.z;
        }
        float rU = 500f * AppConstants.MM_TO_UNITS;
        float tU = 25f * AppConstants.MM_TO_UNITS;
        Assert.AreEqual(rU, maxX - minX, 1e-4f, "X extent should match radius in world units");
        Assert.AreEqual(tU, maxY - minY, 1e-4f, "Y extent should match thickness in world units");
        Assert.AreEqual(rU, maxZ - minZ, 1e-4f, "Z extent should match radius in world units");
    }

    [Test]
    public void BuildMesh_VertexCount_MatchesExpectedTopology()
    {
        // bottom cap: 1 center + 17 arc = 18
        // top cap: 1 center + 17 arc = 18
        // curved side: 17 * 2 = 34
        // flat sides: 4 + 4 = 8
        // total = 78
        var shelf = CreateShelf("R_VTX", 300, 18, Vector3.zero);
        var mesh = shelf.GetComponent<MeshFilter>().sharedMesh;
        Assert.AreEqual(78, mesh.vertexCount);
    }

    [Test]
    public void BuildMesh_TriangleCount_MatchesExpectedTopology()
    {
        // bottom cap: 16 triangles (fan)
        // top cap: 16 triangles (fan)
        // curved side: 16 * 2 = 32 triangles (quads split to 2 tris)
        // flat sides: 2 + 2 = 4 triangles
        // total = 68
        var shelf = CreateShelf("R_TRIS", 300, 18, Vector3.zero);
        var mesh = shelf.GetComponent<MeshFilter>().sharedMesh;
        Assert.AreEqual(68, mesh.triangles.Length / 3);
    }

    [Test]
    public void BuildMesh_NormalsPointOutward()
    {
        var shelf = CreateShelf("R_NORM", 300, 18, Vector3.zero);
        var mesh = shelf.GetComponent<MeshFilter>().sharedMesh;
        var normals = mesh.normals;

        // Curved side normals should have positive X and Z components (pointing away from origin).
        int curvedStart = 0;
        int bottomVerts = 18; // 1 center + 17 arc
        int topVerts = 18;
        curvedStart = bottomVerts + topVerts;

        for (int i = curvedStart; i < curvedStart + 34; i++)
        {
            var n = normals[i];
            // Normals on the curved side point radially outward from origin in XZ plane.
            float lenXZ = Mathf.Sqrt(n.x * n.x + n.z * n.z);
            Assert.Greater(lenXZ, 0.9f, $"curved side normal {i} should point outward in XZ");
            Assert.AreEqual(0f, n.y, 1e-4f, "curved side normals must have zero Y component");
        }

        // Top cap normals should point up (+Y).
        for (int i = bottomVerts; i < bottomVerts + topVerts; i++)
        {
            Assert.AreEqual(1f, normals[i].y, 1e-4f, $"top cap normal {i} should point up");
        }

        // Bottom cap normals should point down (-Y).
        for (int i = 0; i < bottomVerts; i++)
        {
            Assert.AreEqual(-1f, normals[i].y, 1e-4f, $"bottom cap normal {i} should point down");
        }
    }

    [Test]
    public void BuildMesh_RadiusChange_RebuildsMesh()
    {
        var shelf = CreateShelf("R_CHG", 300, 18, Vector3.zero);
        shelf.Radius = 500;

        var mesh = shelf.GetComponent<MeshFilter>().sharedMesh;
        Assert.AreEqual(500f * AppConstants.MM_TO_UNITS, mesh.bounds.size.x, 1e-4f);
        Assert.AreEqual(500f * AppConstants.MM_TO_UNITS, mesh.bounds.size.z, 1e-4f);
        Assert.AreEqual(Vector3.one.x, shelf.transform.localScale.x, 1e-6f);
        Assert.AreEqual(Vector3.one.y, shelf.transform.localScale.y, 1e-6f);
        Assert.AreEqual(Vector3.one.z, shelf.transform.localScale.z, 1e-6f);
    }

    [Test]
    public void ElementData_FromElement_PreservesRadius()
    {
        var shelf = CreateShelf("R4", 450, 25, new Vector3(1f, 0.5f, -2f));
        var ed = ElementData.FromElement(shelf);

        Assert.IsTrue(ed.isRadialShelf);
        Assert.AreEqual(450, ed.radius);
        Assert.AreEqual(new Vector3Int(450, 25, 450), ed.Dimensions);
    }

    [Test]
    public void SaveLoad_RoundTrip_KeepsRadius()
    {
        var shelf = CreateShelf("R5", 350, 18, new Vector3(0.5f, 0.1f, -1f));
        shelf.Movable = false;
        shelf.GroupId = 7;
        shelf.MaterialId = "oak";

        var ed = ElementData.FromElement(shelf);
        var json = JsonUtility.ToJson(ed);
        var restored = JsonUtility.FromJson<ElementData>(json);

        Assert.IsTrue(restored.isRadialShelf);
        Assert.AreEqual(350, restored.radius);
        Assert.AreEqual(new Vector3Int(350, 18, 350), restored.Dimensions);
        Assert.AreEqual("R5", restored.name);
        Assert.AreEqual(false, restored.movable);
        Assert.AreEqual(7, restored.groupId);
        Assert.AreEqual("oak", restored.materialId);
    }

    [Test]
    public void RestoreScene_CreatesRadialShelfWithRadius()
    {
        var data = new ProjectData(new[]
        {
            new ElementData
            {
                name = "RSaved",
                dimensionsMM = new[] { 400, 18, 400 },
                position = new[] { 0f, 0f, 0f },
                rotation = new[] { 0f, 0f, 0f, 1f },
                isRadialShelf = true,
                radius = 400,
                materialId = "default"
            }
        });

        var created = SaveLoadManager.RestoreScene(data);
        Assert.AreEqual(1, created.Count);

        var shelf = created[0].GetComponent<RadialShelfElement>();
        Assert.IsNotNull(shelf);
        Assert.AreEqual(400, shelf.Radius);
        Assert.AreEqual(new Vector3Int(400, 18, 400), shelf.DimensionsMM);

        foreach (var go in created)
            if (go != null) Object.DestroyImmediate(go);
    }

    [Test]
    public void Convert_PartToRadialShelf_PreservesNameAndSetsRadius()
    {
        var go = new GameObject("Part1");
        _go = go;
        go.transform.position = Vector3.zero;
        var part = go.AddComponent<KitchenElement>();
        part.PartName = "Part1";
        part.DimensionsMM = new Vector3Int(300, 18, 200);
        PartRegistry.Register(part);

        var converted = ElementConverter.Convert(part, ElementConverter.TargetType.RadialShelf);

        Assert.IsNotNull(converted as RadialShelfElement);
        Assert.AreEqual("Part1", converted.PartName);
        Assert.AreEqual(new Vector3Int(300, 18, 300), converted.DimensionsMM);
        Assert.AreEqual(300, (converted as RadialShelfElement)!.Radius);
    }

    [Test]
    public void Convert_RadialShelfToPart_PreservesName()
    {
        var shelf = CreateShelf("RS", 400, 18, Vector3.zero);
        var converted = ElementConverter.Convert(shelf, ElementConverter.TargetType.Part);

        Assert.IsNull(converted as RadialShelfElement);
        Assert.AreEqual("RS", converted.PartName);
        Assert.AreEqual(new Vector3Int(400, 18, 400), converted.DimensionsMM);
    }

    private static RadialShelfElement CreateShelf(string name, int radius, int thickness, Vector3 pos)
    {
        var go = ElementFactory.CreateRadialShelf(radius, thickness, name, pos);
        return go.GetComponent<RadialShelfElement>();
    }
}
