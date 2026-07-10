using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

public class RadialShelfTests
{
    private GameObject _go;

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
        Assert.AreEqual(300, (converted as RadialShelfElement).Radius);
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
