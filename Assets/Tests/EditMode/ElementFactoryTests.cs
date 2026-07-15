using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

public class ElementFactoryTests
{
    [SetUp]
    public void Setup()
    {
        PartRegistry.Clear();
    }

    [TearDown]
    public void Teardown()
    {
        foreach (var el in PartRegistry.GetAll())
        {
            if (el != null)
                ElementFactory.DestroyElement(el.gameObject);
        }
        PartRegistry.Clear();
    }

    [Test]
    public void CreatePart_ReturnsNonNull()
    {
        var go = ElementFactory.CreatePart(new Vector3Int(800, 400, 18), "Test", Vector3.zero);
        Assert.NotNull(go);
        Assert.NotNull(go.GetComponent<KitchenElement>());
        ElementFactory.DestroyPart(go);
    }

    [Test]
    public void CreatePart_SetsCorrectScale()
    {
        var go = ElementFactory.CreatePart(new Vector3Int(800, 400, 18), "Test", Vector3.zero);
        var scale = go.transform.localScale;
        Assert.AreEqual(0.8f, scale.x, 0.001f);
        Assert.AreEqual(0.4f, scale.y, 0.001f);
        Assert.AreEqual(0.018f, scale.z, 0.001f);
        ElementFactory.DestroyPart(go);
    }

    [Test]
    public void DimensionsMM_Clamp_ClampsToMinimum()
    {
        var go = ElementFactory.CreatePart(new Vector3Int(800, 400, 18), "Test", Vector3.zero);
        var element = go.GetComponent<KitchenElement>();

        element.DimensionsMM = new Vector3Int(0, -5, 0);

        Assert.AreEqual(new Vector3Int(1, 1, 1), element.DimensionsMM);
        ElementFactory.DestroyPart(go);
    }

    [Test]
    public void GetVertices_Returns8Elements()
    {
        var go = ElementFactory.CreatePart(new Vector3Int(800, 400, 18), "Test", Vector3.zero);
        var element = go.GetComponent<KitchenElement>();

        var vertices = element.GetVertices();
        Assert.AreEqual(8, vertices.Length);

        ElementFactory.DestroyPart(go);
    }

    [Test]
    public void GetFaces_Returns6Elements()
    {
        var go = ElementFactory.CreatePart(new Vector3Int(800, 400, 18), "Test", Vector3.zero);
        var element = go.GetComponent<KitchenElement>();

        var faces = element.GetFaces();
        Assert.AreEqual(6, faces.Length);

        ElementFactory.DestroyPart(go);
    }

    [Test]
    public void CreatePreset_CreatesCorrectDimensions()
    {
        var go = ElementFactory.CreatePreset(0, Vector3.zero);
        var element = go.GetComponent<KitchenElement>();
        Assert.AreEqual(AppConstants.PRESET_DIMENSIONS_MM[0], element.DimensionsMM);
        ElementFactory.DestroyPart(go);
    }

    [Test]
    public void Duplicate_CreatesCopyWithSameDimensions()
    {
        var original = ElementFactory.CreatePart(new Vector3Int(600, 720, 560), "Original", Vector3.zero);
        var element = original.GetComponent<KitchenElement>();

        var copy = ElementFactory.Duplicate(element);
        var copyElement = copy.GetComponent<KitchenElement>();

        Assert.AreEqual(element.DimensionsMM, copyElement.DimensionsMM);
        Assert.AreNotEqual(original.transform.position, copy.transform.position);

        ElementFactory.DestroyPart(original);
        ElementFactory.DestroyPart(copy);
    }

    [Test]
    public void CreateRadialShelf_CreatesMeshAndCollider()
    {
        var go = ElementFactory.CreateRadialShelf(600, 400, 18, 200, "Radial", Vector3.zero);
        var shelf = go.GetComponent<RadialShelfElement>();

        Assert.IsNotNull(shelf);
        Assert.AreEqual(200, shelf.CornerRadius);
        Assert.AreEqual(new Vector3Int(600, 18, 400), shelf.DimensionsMM);
        Assert.IsNotNull(go.GetComponent<MeshFilter>());
        Assert.IsNotNull(go.GetComponent<MeshCollider>());

        ElementFactory.DestroyElement(go);
    }

    [Test]
    public void Duplicate_RadialShelf_KeepsDimensionsAndCornerRadius()
    {
        var original = ElementFactory.CreateRadialShelf(500, 350, 25, 150, "RadialOriginal", Vector3.zero);
        var shelf = original.GetComponent<RadialShelfElement>();

        var copy = ElementFactory.Duplicate(shelf);
        var copyShelf = copy.GetComponent<RadialShelfElement>();

        Assert.IsNotNull(copyShelf);
        Assert.AreEqual(150, copyShelf.CornerRadius);
        Assert.AreEqual(new Vector3Int(500, 25, 350), copyShelf.DimensionsMM);

        ElementFactory.DestroyElement(original);
        ElementFactory.DestroyElement(copy);
    }

    [Test]
    public void Boards_ShareMaterialInstance_ForBatching()
    {
        var go1 = ElementFactory.CreatePart(new Vector3Int(800, 400, 18), "A", Vector3.zero);
        var go2 = ElementFactory.CreatePart(new Vector3Int(600, 300, 18), "B", Vector3.zero);

        var mat1 = go1.GetComponent<MeshRenderer>().sharedMaterial;
        var mat2 = go2.GetComponent<MeshRenderer>().sharedMaterial;

        Assert.AreSame(mat1, mat2, "Boards must share material for batching");

        ElementFactory.DestroyPart(go1);
        ElementFactory.DestroyPart(go2);
    }

    [Test]
    public void BoardAndFacade_ShareMaterialInstance()
    {
        var board = ElementFactory.CreatePart(new Vector3Int(800, 400, 18), "Board", Vector3.zero);
        var facade = ElementFactory.CreateFacade(new Vector3Int(600, 716, 18), "Facade", Vector3.zero);

        var boardMat = board.GetComponent<MeshRenderer>().sharedMaterial;
        var facadeMat = facade.GetComponent<MeshRenderer>().sharedMaterial;

        Assert.AreSame(boardMat, facadeMat, "Board and Facade must share material");

        ElementFactory.DestroyPart(board);
        ElementFactory.DestroyFacade(facade);
    }

    [Test]
    public void Pool_ReusesDestroyedBoard()
    {
        var go = ElementFactory.CreatePart(new Vector3Int(800, 400, 18), "First", Vector3.zero);
        var element = go.GetComponent<KitchenElement>();
        ElementFactory.DestroyPart(go);

        var go2 = ElementFactory.CreatePart(new Vector3Int(600, 300, 18), "Second", Vector3.zero);
        // Пул переиспользовал GameObject — ссылка валидна, имя новое
        Assert.AreEqual("Second", go2.name);
        Assert.AreEqual(new Vector3Int(600, 300, 18), go2.GetComponent<KitchenElement>().DimensionsMM);
        ElementFactory.DestroyPart(go2);
    }

    [Test]
    public void Pool_ClearsGroupId_OnReuse()
    {
        var go = ElementFactory.CreatePart(new Vector3Int(800, 400, 18), "Grouped", Vector3.zero);
        var el = go.GetComponent<KitchenElement>();
        el.GroupId = 42; // симулируем членство в группе
        ElementFactory.DestroyPart(go);

        var go2 = ElementFactory.CreatePart(new Vector3Int(600, 300, 18), "Reused", Vector3.zero);
        var el2 = go2.GetComponent<KitchenElement>();
        Assert.AreEqual(0, el2.GroupId, "GroupId must be reset on pool reuse");
        ElementFactory.DestroyPart(go2);
    }

    [Test]
    public void Pool_UnregistersOnRelease_ReRegistersOnGet()
    {
        var go = ElementFactory.CreatePart(new Vector3Int(800, 400, 18), "A", Vector3.zero);
        Assert.IsTrue(PartRegistry.GetAll().Exists(e => e.PartName == "A"));

        ElementFactory.DestroyPart(go);
        Assert.IsFalse(PartRegistry.GetAll().Exists(e => e.PartName == "A"));

        var go2 = ElementFactory.CreatePart(new Vector3Int(600, 300, 18), "B", Vector3.zero);
        Assert.IsTrue(PartRegistry.GetAll().Exists(e => e.PartName == "B"));
        ElementFactory.DestroyPart(go2);
    }
}