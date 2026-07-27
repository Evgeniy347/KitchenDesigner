using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

public class ElementFactoryTests
{
    private GameObject? _extraObjects;

    [SetUp]
    public void Setup()
    {
        PartRegistry.Clear();
        _extraObjects = new GameObject("_test_cleanup");
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
        if (_extraObjects != null)
        {
            var children = new System.Collections.Generic.List<GameObject>();
            foreach (Transform t in _extraObjects.transform) children.Add(t.gameObject);
            foreach (var c in children) Object.DestroyImmediate(c);
            Object.DestroyImmediate(_extraObjects);
        }
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
    public void Duplicate_AssembledFacade_PreservesGaps()
    {
        // Регрессия: фабрика CreateAssembledFacade не принимает зазоры, и дубль
        // сборного фасада терял их (обычный фасад получает зазоры в CreateFacade).
        var original = ElementFactory.CreateAssembledFacade(
            new Vector3Int(600, 716, 18), "Assembled", Vector3.zero, AssembledFill.Blind);
        var src = original.GetComponent<AssembledFacadeElement>();
        src.GapLeft = 3;
        src.GapRight = 5;
        src.GapTop = 1;
        src.GapBottom = 4;

        var copy = ElementFactory.Duplicate(src);
        var copyFacade = copy.GetComponent<AssembledFacadeElement>();

        Assert.IsNotNull(copyFacade);
        Assert.AreEqual(3, copyFacade!.GapLeft, "gapLeft");
        Assert.AreEqual(5, copyFacade!.GapRight, "gapRight");
        Assert.AreEqual(1, copyFacade!.GapTop, "gapTop");
        Assert.AreEqual(4, copyFacade!.GapBottom, "gapBottom");
        Assert.AreEqual(AssembledFill.Blind, copyFacade!.Fill, "fill");

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

    [Test]
    public void Duplicate_Wall_DoesNotKeepRedValidationTint_AfterMove()
    {
        var hlGo = new GameObject("ElementHighlighter");
        var hl = hlGo.AddComponent<ElementHighlighter>();
        hlGo.transform.SetParent(_extraObjects!.transform);

        var instanceProp = typeof(ElementHighlighter).GetProperty("Instance",
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
        instanceProp?.SetValue(null, hl);

        var wallGo = ElementFactory.CreateWall(
            new Vector3Int(2000, 2700, 100), "SourceWall", new Vector3(0f, 1.35f, 0f));
        var wall = wallGo.GetComponent<KitchenElement>();

        var cloneGo = ElementFactory.Duplicate(wall);
        var clone = cloneGo.GetComponent<KitchenElement>();

        Assert.IsNotNull(clone.GetComponent<Wall>(), "clone is a wall");

        var renderer = cloneGo.GetComponent<MeshRenderer>();
        Assert.IsNotNull(renderer, "clone has MeshRenderer");

        var color = renderer!.sharedMaterial.GetColor("_BaseColor");
        Assert.AreEqual(0.8f, color.r, 0.05f, "clone should have source material (~0.8), not red");

        clone!.transform.position = new Vector3(100f, 100f, 100f);
        ElementHighlighter.Instance?.RefreshHighlights();

        var vr = ConstraintValidator.Validate(PartRegistry.GetAll());
        Assert.IsFalse(vr.violations.Contains(clone), "no violations after moving away");

        color = renderer.sharedMaterial.GetColor("_BaseColor");
        Assert.AreEqual(0.8f, color.r, 0.05f,
            "after move + refresh, wall should still have source material, not red — " +
            "ApplyMaterial must not skip walls that still have validation tint");
    }
}