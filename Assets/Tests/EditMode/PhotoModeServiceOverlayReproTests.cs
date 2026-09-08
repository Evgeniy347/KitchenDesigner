using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using KitchenDesigner.Core;

public class PhotoModeServiceOverlayReproTests
{
    private readonly List<GameObject> _spawned = new List<GameObject>();
    private bool _tintBefore;

    [SetUp]
    public void SetUp()
    {
        LogAssert.ignoreFailingMessages = true;
        _tintBefore = ElementHighlighter.TintEnabled;
        PartRegistry.Clear();
        EditModeManager.Reset();
    }

    [TearDown]
    public void TearDown()
    {
        EditModeManager.Reset();
        ElementHighlighter.TintEnabled = _tintBefore;
        foreach (var go in _spawned) if (go != null) Object.DestroyImmediate(go);
        _spawned.Clear();
        PartRegistry.Clear();
        MaterialManager.ClearCache();
        MaterialCatalog.Reset();
        LogAssert.ignoreFailingMessages = false;
    }

    private GameObject Spawn(GameObject go)
    {
        _spawned.Add(go);
        return go;
    }

    private FacadeElement MakeFacade(string name, Vector3Int dims, Vector3 pos,
        int gapL = 2, int gapR = 2, int gapT = 2, int gapB = 2)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.position = pos;
        var f = go.AddComponent<FacadeElement>();
        f.PartName = name;
        f.DimensionsMM = dims;
        f.GapLeft = gapL;
        f.GapRight = gapR;
        f.GapTop = gapT;
        f.GapBottom = gapB;
        PartRegistry.Register(f);
        Spawn(go);
        return f;
    }

    private KitchenElement MakePart(Vector3Int dims, string name)
    {
        var go = Spawn(ElementFactory.CreatePart(dims, name, Vector3.zero));
        var e = go.GetComponent<KitchenElement>()!;
        e.DimensionsMM = dims;
        e.ApplyDimensions();
        return e;
    }

    // ── D11: тонировка нарушения (ConstraintValidator) не должна попадать в кадр ──

    [Test]
    public void ApplyMaterial_InvalidFacade_NormalMode_PaintsTheInvalidTint()
    {
        var host = Spawn(new GameObject("Highlighter"));
        var highlighter = host.AddComponent<ElementHighlighter>();
        highlighter.CreateMaterials();

        var a = MakeFacade("A", new Vector3Int(400, 300, 18), Vector3.zero);
        MakeFacade("B", new Vector3Int(400, 300, 18), new Vector3(0.003f, 0f, 0f));
        Assume.That(ConstraintValidator.Validate(PartRegistry.GetAll()).violations.Contains(a), Is.True,
            "тест построен на перекрытии эффективных границ фасадов — без нарушения "
            + "непонятно, что именно должен гасить фоторежим");
        var ownDecor = a.GetComponent<MeshRenderer>().sharedMaterial;

        ElementHighlighter.TintEnabled = true;
        highlighter.ApplyForElement(a);

        Assert.AreNotEqual(ownDecor, a.GetComponent<MeshRenderer>().sharedMaterials[0],
            "вне фоторежима нарушение обязано красить деталь красным — иначе противоположный "
            + "вход не доказывает, что фоторежим что-то гасит");
    }

    [Test]
    public void ApplyMaterial_InvalidFacade_PhotoModeActive_ShowsOwnDecor_NotTheValidityTint()
    {
        var host = Spawn(new GameObject("Highlighter"));
        var highlighter = host.AddComponent<ElementHighlighter>();
        highlighter.CreateMaterials();

        var a = MakeFacade("A", new Vector3Int(400, 300, 18), Vector3.zero);
        MakeFacade("B", new Vector3Int(400, 300, 18), new Vector3(0.003f, 0f, 0f));
        Assume.That(ConstraintValidator.Validate(PartRegistry.GetAll()).violations.Contains(a), Is.True,
            "нарушение обязано существовать — иначе непонятно, что именно проверяется");
        var ownDecor = a.GetComponent<MeshRenderer>().sharedMaterial;

        EditModeManager.SetMode(EditMode.Photo);
        highlighter.ApplyForElement(a);

        Assert.AreEqual(ownDecor, a.GetComponent<MeshRenderer>().sharedMaterials[0],
            "D11: тонкая красная метка валидности не должна попадать в фотографию для "
            + "заказчика — TintEnabled=false в фоторежиме обязан гасить И невалидную ветку, "
            + "а не только зелёную подсветку валидной детали");
    }

    // ── D11: золотая подсветка выделения не должна попадать в кадр ──

    [Test]
    public void Select_NormalMode_PaintsTheSelectionHighlight()
    {
        var e = MakePart(new Vector3Int(400, 300, 18), "Board");
        var ownDecor = e.GetComponent<MeshRenderer>().sharedMaterial;

        var sm = Spawn(new GameObject("SelectionManager")).AddComponent<SelectionManager>();

        sm.Select(e);

        Assert.AreNotEqual(ownDecor, e.GetComponent<MeshRenderer>().sharedMaterial,
            "вне фоторежима выделение обязано красить деталь золотистым тоном — "
            + "противоположный вход, без которого следующий тест ничего не доказывает");
    }

    [Test]
    public void Select_WhilePhotoModeActive_DoesNotPaintTheSelectionHighlight()
    {
        var e = MakePart(new Vector3Int(400, 300, 18), "Board");
        var ownDecor = e.GetComponent<MeshRenderer>().sharedMaterial;

        var sm = Spawn(new GameObject("SelectionManager")).AddComponent<SelectionManager>();

        EditModeManager.SetMode(EditMode.Photo);
        sm.Select(e);

        Assert.AreEqual(ownDecor, e.GetComponent<MeshRenderer>().sharedMaterial,
            "D11: выбор детали ВО ВРЕМЯ фоторежима не должен зажигать золотистую "
            + "подсветку — до этого коммита DeselectAll на входе спасал только случай "
            + "«выделили ДО фоторежима», а клик уже внутри режима ничем не был перекрыт");
    }

    [Test]
    public void Select_BeforeEnteringPhotoMode_HighlightIsRemovedOnEntry_AndSelectionIsCleared()
    {
        var e = MakePart(new Vector3Int(400, 300, 18), "Board");
        var ownDecor = e.GetComponent<MeshRenderer>().sharedMaterial;

        var sm = Spawn(new GameObject("SelectionManager")).AddComponent<SelectionManager>();

        sm.Select(e);
        Assume.That(e.GetComponent<MeshRenderer>().sharedMaterial, Is.Not.EqualTo(ownDecor),
            "предпосылка: выделение реально перекрашивает деталь");

        EditModeManager.SetMode(EditMode.Photo);

        Assert.AreEqual(ownDecor, e.GetComponent<MeshRenderer>().sharedMaterial,
            "вход в фоторежим с уже выделенным объектом обязан снять подсветку до кадра");
        Assert.IsNull(sm.Selected,
            "выделение снимается целиком — это не «то же самое, но другим цветом»");
    }
}
