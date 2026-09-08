using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using KitchenDesigner.Core;

public class ElementHighlighterTests
{
    private readonly List<GameObject> _spawned = new List<GameObject>();
    private ElementHighlighter? _highlighter;
    private bool _tintBefore;

    [SetUp]
    public void SetUp()
    {
        LogAssert.ignoreFailingMessages = true;
        _tintBefore = ElementHighlighter.TintEnabled;
        PartRegistry.Clear();

        var host = new GameObject("Highlighter");
        _spawned.Add(host);
        _highlighter = host.AddComponent<ElementHighlighter>();
        _highlighter.CreateMaterials();
    }

    [TearDown]
    public void TearDown()
    {
        ElementHighlighter.TintEnabled = _tintBefore;
        foreach (var go in _spawned) if (go != null) Object.DestroyImmediate(go);
        _spawned.Clear();
        foreach (var e in Object.FindObjectsByType<KitchenElement>())
            if (e != null) Object.DestroyImmediate(e.gameObject);
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

    private KitchenElement MakePart(Vector3Int dims, string name)
    {
        var go = Spawn(ElementFactory.CreatePart(dims, name, Vector3.zero));
        var e = go.GetComponent<KitchenElement>()!;
        e.DimensionsMM = dims;
        e.ApplyDimensions();
        return e;
    }

    [Test]
    public void MakeTransparent_SetsTheBlendStatesExplicitly_NotJustTheSurfaceFlag()
    {
        var shader = Shader.Find("Universal Render Pipeline/Lit");
        Assert.IsNotNull(shader);

        var m = ElementHighlighter.MakeTransparent(shader, new Color(0.7f, 0.85f, 0.7f, 0.08f));

        Assert.AreEqual((int)UnityEngine.Rendering.RenderQueue.Transparent, m.renderQueue,
            "очередь Transparent — по ней PhotoShadowCasters отличает стекло от глухой детали");
        Assert.AreEqual((int)UnityEngine.Rendering.BlendMode.SrcAlpha, m.GetInt("_SrcBlend"),
            "одного _Surface=1 в рантайме мало: без явных blend-состояний URP оставит "
            + "непрозрачный проход и грань нарисуется сплошной");
        Assert.AreEqual((int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha, m.GetInt("_DstBlend"));
        Assert.AreEqual(0, m.GetInt("_ZWrite"), "запись в глубину гасит то, что за сквозной гранью");
        Assert.IsTrue(m.IsKeywordEnabled("_SURFACE_TYPE_TRANSPARENT"));
        Assert.Less(m.GetColor("_BaseColor").a, 1f, "тонировка еле заметная: форму читает контур");

        Object.DestroyImmediate(m);
    }

    [Test]
    public void RefreshHighlights_SyncsTheEdgeSubstrate_WithoutASeparateTrigger()
    {
        var e = MakePart(new Vector3Int(600, 18, 500), "SubstratePart");
        e.EdgeBandingEnabled = false;
        Assume.That(MaterialManager.HasCustomDecor(e), Is.False, "деталь на дефолтном декоре");

        _highlighter!.RefreshHighlights();

        Assert.AreEqual(2, e.GetComponent<MeshRenderer>().sharedMaterials.Length,
            "подложка торцов зависит от соседей ровно как валидация и пересобирается тем же "
            + "проходом: отдельного триггера у неё нет, и без него голый торец не появился бы");
    }

    [Test]
    public void BasePlateAndLamp_KeepTheirOwnMaterial_WhateverTheValidationSays()
    {
        var plate = MakePart(new Vector3Int(3000, 18, 3000), "Plate");
        plate.gameObject.AddComponent<BasePlate>();
        var plateMat = plate.GetComponent<MeshRenderer>().sharedMaterial;

        var lampGo = Spawn(ElementFactory.CreateLightSource("Lamp", new Vector3(0f, 2f, 0f)));
        var lamp = lampGo.GetComponent<LightSourceElement>()!;
        lamp.EnsureLight();
        var lampMat = lampGo.GetComponent<MeshRenderer>()!.sharedMaterial;

        _highlighter!.RefreshHighlights();

        Assert.AreEqual(plateMat, plate.GetComponent<MeshRenderer>().sharedMaterial,
            "пол держит свой материал: валидационный тон на всю плиту забивает сцену");
        Assert.AreEqual(lampMat, lampGo.GetComponent<MeshRenderer>()!.sharedMaterial,
            "плафон лампы светящийся — тонировка погасила бы его");
    }

    [Test]
    public void TintOff_ValidPart_ShowsItsOwnDecorInsteadOfTheFlatGreenTint()
    {
        var plate = MakePart(new Vector3Int(3000, 18, 3000), "AnchorPlate");
        plate.transform.position = new Vector3(0f, -0.009f, 0f);
        plate.gameObject.AddComponent<BasePlate>();

        var e = MakePart(new Vector3Int(600, 18, 500), "TintOffPart");
        e.transform.position = new Vector3(0f, 0.009f, 0f);
        var ownDecor = e.GetComponent<MeshRenderer>().sharedMaterial;

        Assume.That(ConstraintValidator.Validate(PartRegistry.GetAll()).violations.Contains(e), Is.False,
            "деталь должна быть валидной, иначе обе ветки красят её красным и тест пуст");

        ElementHighlighter.TintEnabled = true;
        _highlighter!.ApplyForElement(e);
        var tinted = e.GetComponent<MeshRenderer>().sharedMaterials[0];
        Assert.AreNotEqual(ownDecor, tinted, "с включённой тонировкой валидная деталь красится зелёным");

        ElementHighlighter.TintEnabled = false;
        _highlighter!.ApplyForElement(e);

        Assert.AreNotEqual(tinted, e.GetComponent<MeshRenderer>().sharedMaterials[0],
            "с выключенной тонировкой валидная деталь показывает СВОЙ материал, а не плоский тон");
    }

    [Test]
    public void RefreshHighlights_AssembledFacadeGrooveSubmesh_SensorPrintsSlotsAcrossTintCycle()
    {
        var facade = Spawn(ElementFactory.CreateAssembledFacade(
            new Vector3Int(600, 716, 18), "Facade", Vector3.zero));
        var facadeElement = facade.GetComponent<AssembledFacadeElement>()!;
        var renderer = facade.GetComponent<MeshRenderer>()!;

        Assume.That(renderer.sharedMaterials.Length, Is.EqualTo(2),
            "у сборного фасада два слота: 0=тело, 1=паз (AssembledFacadeMesh.Build)");
        var grooveOriginal = renderer.sharedMaterials[1].GetColor("_BaseColor");
        var bodyOriginal = renderer.sharedMaterials[0].GetColor("_BaseColor");

        var blocker = Spawn(ElementFactory.CreateAssembledFacade(
            new Vector3Int(600, 716, 18), "Blocker", Vector3.zero));

        _highlighter!.RefreshHighlights();
        var invalid = ConstraintValidator.Validate(PartRegistry.GetAll());
        Assume.That(invalid.violations.Contains(facadeElement), Is.True,
            "деталь должна получить нарушение (полный наезд второй), иначе тест ничего не проверяет");

        var afterTint = renderer.sharedMaterials;
        TestContext.WriteLine($"submeshCount={afterTint.Length}");
        for (int i = 0; i < afterTint.Length; i++)
            TestContext.WriteLine($"после тонировки (invalid) slot[{i}] _BaseColor={afterTint[i].GetColor("_BaseColor")}");

        Assert.AreNotEqual(bodyOriginal, afterTint[0].GetColor("_BaseColor"),
            "пласть (слот 0) обязана покраснеть — иначе тонировка вообще не сработала и тест пуст");
        Assert.AreEqual(grooveOriginal, afterTint[1].GetColor("_BaseColor"),
            "паз (слот 1) обязан остаться тёмным при тонировке пласти валидностью");

        Object.DestroyImmediate(blocker);
        _highlighter!.RefreshHighlights();
        var valid = ConstraintValidator.Validate(PartRegistry.GetAll());
        Assume.That(valid.violations.Contains(facadeElement), Is.False,
            "после удаления второй детали нарушение обязано снять, иначе снятие тонировки не проверено");

        var afterUntint = renderer.sharedMaterials;
        for (int i = 0; i < afterUntint.Length; i++)
            TestContext.WriteLine($"после снятия тонировки (valid) slot[{i}] _BaseColor={afterUntint[i].GetColor("_BaseColor")}");

        Assert.AreEqual(grooveOriginal, afterUntint[1].GetColor("_BaseColor"),
            "после снятия тонировки паз обязан вернуться к исходному тёмному материалу");

        var blocker2 = Spawn(ElementFactory.CreateAssembledFacade(
            new Vector3Int(600, 716, 18), "Blocker2", Vector3.zero));
        _highlighter!.RefreshHighlights();
        _highlighter!.RefreshHighlights();
        var retinted = renderer.sharedMaterials;
        for (int i = 0; i < retinted.Length; i++)
            TestContext.WriteLine($"после повторной тонировки поверх уже тонированной slot[{i}] "
                + $"_BaseColor={retinted[i].GetColor("_BaseColor")}");

        Assert.AreEqual(grooveOriginal, retinted[1].GetColor("_BaseColor"),
            "паз обязан остаться тёмным и при повторной тонировке поверх уже тонированной детали");
    }

    [Test]
    public void Cooktop_Invalid_TurnsRedThroughItsChildren_BecauseTheRootCarriesNoRenderer()
    {
        var go = Spawn(ElementFactory.CreateCooktop("CooktopTint", Vector3.zero));
        var cooktop = go.GetComponent<CooktopElement>()!;

        Assert.IsNull(go.GetComponent<MeshRenderer>(),
            "варочная собрана из дочерних коробок: на корне рендерера нет, красить её через корень нечем");
        var children = go.GetComponentsInChildren<MeshRenderer>();
        Assert.Greater(children.Length, 0, "дочерние коробки должны существовать, иначе тест пустой");
        var before = children[0].sharedMaterial;

        _highlighter!.PaintCooktopChildren(cooktop, isValid: false);

        Assert.AreNotEqual(before, children[0].sharedMaterial,
            "без этой ветки варочная не могла бы покраснеть вообще, и наезд её выреза на боковину "
            + "был бы виден только по самой боковине");
        foreach (var mr in children)
            Assert.AreEqual(children[0].sharedMaterial, mr.sharedMaterial,
                "красным становятся ВСЕ дочерние коробки, а не первая попавшаяся");
    }
}
