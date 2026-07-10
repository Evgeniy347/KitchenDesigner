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

    [Test]
    public void Boards_ShareMaterialInstance_ForBatching()
    {
        var go1 = ElementFactory.CreateBoard(new Vector3Int(800, 400, 18), "A", Vector3.zero);
        var go2 = ElementFactory.CreateBoard(new Vector3Int(600, 300, 18), "B", Vector3.zero);

        var mat1 = go1.GetComponent<MeshRenderer>().sharedMaterial;
        var mat2 = go2.GetComponent<MeshRenderer>().sharedMaterial;

        // Оба используют один инстанс материала — Static/Dynamic Batching работает
        Assert.AreSame(mat1, mat2, "Boards must share material for batching");

        Object.DestroyImmediate(go1);
        Object.DestroyImmediate(go2);
    }

    [Test]
    public void BoardAndFacade_ShareMaterialInstance()
    {
        var board = ElementFactory.CreateBoard(new Vector3Int(800, 400, 18), "Board", Vector3.zero);
        var facade = ElementFactory.CreateFacade(new Vector3Int(600, 716, 18), "Facade", Vector3.zero);

        var boardMat = board.GetComponent<MeshRenderer>().sharedMaterial;
        var facadeMat = facade.GetComponent<MeshRenderer>().sharedMaterial;

        Assert.AreSame(boardMat, facadeMat, "Board and Facade must share material");

        Object.DestroyImmediate(board);
        Object.DestroyImmediate(facade);
    }
}
