using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

public class ElementFactoryTests
{
    [Test]
    public void CreateBoard_ReturnsNonNull()
    {
        var go = ElementFactory.CreateBoard(new Vector3Int(800, 400, 18), "Test", Vector3.zero);
        Assert.NotNull(go);
        Assert.NotNull(go.GetComponent<KitchenElement>());
        Object.DestroyImmediate(go);
    }

    [Test]
    public void CreateBoard_SetsCorrectScale()
    {
        var go = ElementFactory.CreateBoard(new Vector3Int(800, 400, 18), "Test", Vector3.zero);
        var scale = go.transform.localScale;
        Assert.AreEqual(0.8f, scale.x, 0.001f);
        Assert.AreEqual(0.4f, scale.y, 0.001f);
        Assert.AreEqual(0.018f, scale.z, 0.001f);
        Object.DestroyImmediate(go);
    }

    [Test]
    public void DimensionsMM_Clamp_ClampsToMinimum()
    {
        var go = ElementFactory.CreateBoard(new Vector3Int(800, 400, 18), "Test", Vector3.zero);
        var element = go.GetComponent<KitchenElement>();

        element.DimensionsMM = new Vector3Int(0, -5, 0);

        Assert.AreEqual(new Vector3Int(1, 1, 1), element.DimensionsMM);
        Object.DestroyImmediate(go);
    }

    [Test]
    public void GetVertices_Returns8Elements()
    {
        var go = ElementFactory.CreateBoard(new Vector3Int(800, 400, 18), "Test", Vector3.zero);
        var element = go.GetComponent<KitchenElement>();

        var vertices = element.GetVertices();
        Assert.AreEqual(8, vertices.Length);

        Object.DestroyImmediate(go);
    }

    [Test]
    public void GetFaces_Returns6Elements()
    {
        var go = ElementFactory.CreateBoard(new Vector3Int(800, 400, 18), "Test", Vector3.zero);
        var element = go.GetComponent<KitchenElement>();

        var faces = element.GetFaces();
        Assert.AreEqual(6, faces.Length);

        Object.DestroyImmediate(go);
    }

    [Test]
    public void CreatePreset_CreatesCorrectDimensions()
    {
        var go = ElementFactory.CreatePreset(0, Vector3.zero);
        var element = go.GetComponent<KitchenElement>();
        Assert.AreEqual(AppConstants.PRESET_DIMENSIONS_MM[0], element.DimensionsMM);
        Object.DestroyImmediate(go);
    }

    [Test]
    public void Duplicate_CreatesCopyWithSameDimensions()
    {
        var original = ElementFactory.CreateBoard(new Vector3Int(600, 720, 560), "Original", Vector3.zero);
        var element = original.GetComponent<KitchenElement>();

        var copy = ElementFactory.Duplicate(element);
        var copyElement = copy.GetComponent<KitchenElement>();

        Assert.AreEqual(element.DimensionsMM, copyElement.DimensionsMM);
        Assert.AreNotEqual(original.transform.position, copy.transform.position);

        Object.DestroyImmediate(original);
        Object.DestroyImmediate(copy);
    }
}
