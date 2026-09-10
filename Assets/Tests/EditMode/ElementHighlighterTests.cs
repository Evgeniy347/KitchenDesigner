using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using KitchenDesigner.Core;

public class ElementHighlighterTests
{
    private readonly List<GameObject> _spawned = new List<GameObject>();
    private ElementHighlighter? _highlighter;
    private bool _violationTintBefore;

    [SetUp]
    public void SetUp()
    {
        LogAssert.ignoreFailingMessages = true;
        _violationTintBefore = ElementHighlighter.ViolationTintVisible;
        PartRegistry.Clear();

        var host = new GameObject("Highlighter");
        _spawned.Add(host);
        _highlighter = host.AddComponent<ElementHighlighter>();
    }

    [TearDown]
    public void TearDown()
    {
        ElementHighlighter.ViolationTintVisible = _violationTintBefore;
        foreach (var go in _spawned) if (go != null) Object.DestroyImmediate(go);
        _spawned.Clear();
        foreach (var e in Object.FindObjectsByType<KitchenElement>())
            if (e != null) Object.DestroyImmediate(e.gameObject);
        PartRegistry.Clear();
        ValidityTint.Clear();
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

    /// <summary>Подложка-план (<c>BasePlate</c>) раньше стояла в списке
    /// исключений «держит свой материал, что бы ни сказала валидация»: тон был
    /// СПЛОШНОЙ заливкой, и красная плита 3×3 м забивала сцену. Причина
    /// исчезла вместе со сплошной заливкой — тон теперь подмешивается к
    /// собственному цвету, — а правило у пользователя одно: любой объект с
    /// нарушением затонирован. Исключений не осталось ни одного.</summary>
    [Test]
    public void BasePlate_IsTintedLikeAnythingElse_TheRuleHasNoExceptions()
    {
        var plate = MakePart(new Vector3Int(3000, 18, 3000), "Plate");
        plate.gameObject.AddComponent<BasePlate>();
        var plateMat = plate.GetComponent<MeshRenderer>().sharedMaterial;
        MakePart(new Vector3Int(3000, 18, 3000), "PlateTwin");

        Assume.That(ConstraintValidator.Validate(PartRegistry.GetAll()).violations.Contains(plate),
            Is.True, "вторая плита стоит ровно на первой — иначе тонировать нечего");

        _highlighter!.RefreshHighlights();

        Assert.AreNotEqual(plateMat, plate.GetComponent<MeshRenderer>().sharedMaterial,
            "подложка с нарушением обязана затониться: правило без исключений");
    }

    [Test]
    public void ValidPart_KeepsItsOwnMaterialUntouched_ThereIsNoGreenTintAnyMore()
    {
        var plate = MakePart(new Vector3Int(3000, 18, 3000), "AnchorPlate");
        plate.transform.position = new Vector3(0f, -0.009f, 0f);
        plate.gameObject.AddComponent<BasePlate>();

        var e = MakePart(new Vector3Int(600, 18, 500), "ValidPart");
        e.transform.position = new Vector3(0f, 0.009f, 0f);
        var ownDecor = e.GetComponent<MeshRenderer>().sharedMaterial;

        Assume.That(ConstraintValidator.Validate(PartRegistry.GetAll()).violations.Contains(e), Is.False,
            "деталь должна быть валидной, иначе проверяется тон нарушения, а не его отсутствие");

        ElementHighlighter.ViolationTintVisible = true;
        _highlighter!.ApplyForElement(e);

        Assert.AreEqual(ownDecor, e.GetComponent<MeshRenderer>().sharedMaterials[0],
            "валидная деталь больше НЕ красится: зелёный тон валидности удалён вместе с "
            + "кнопкой «Тонировка». Подсветка, взявшаяся перекрашивать валидный объект, "
            + "снова спрячет его декор — ровно то, на что жаловался пользователь");
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

    /// <summary>У варочной на корне рендерера нет — тело собрано из дочерних
    /// коробок, и раньше её красила отдельная ветка
    /// (<c>PaintCooktopChildren</c>). Ветка ушла: общий обход
    /// <c>ElementRenderers.BodyOf</c> и так видит детей, а тон, подмешанный к
    /// материалу КАЖДОГО ребёнка, возвращается порендерно и не требует, чтобы
    /// элемент умел перекрасить себя сам. Вопрос теста поэтому другой: каждая
    /// дочерняя коробка носит тон нарушения — и своя стеклокерамика, и свой
    /// корпус, а не один общий красный на всех.</summary>
    [Test]
    public void Cooktop_Invalid_TintsEveryChildBox_ThoughTheRootCarriesNoRenderer()
    {
        var go = Spawn(ElementFactory.CreateCooktop("CooktopTint", Vector3.zero));
        var cooktop = go.GetComponent<CooktopElement>()!;
        Spawn(ElementFactory.CreateCooktop("CooktopTwin", Vector3.zero));

        Assert.IsNull(go.GetComponent<MeshRenderer>(),
            "варочная собрана из дочерних коробок: на корне рендерера нет, красить её через корень нечем");
        var children = ElementRenderers.BodyOf(cooktop);
        Assert.Greater(children.Count, 0, "дочерние коробки должны существовать, иначе тест пустой");
        var before = new Material[children.Count];
        for (int i = 0; i < children.Count; i++) before[i] = children[i].sharedMaterial;

        Assume.That(ConstraintValidator.Validate(PartRegistry.GetAll()).violations.Contains(cooktop),
            Is.True, "вторая варочная стоит ровно на первой — иначе тонировать нечего");

        _highlighter!.ApplyForElement(cooktop);

        for (int i = 0; i < children.Count; i++)
        {
            if (before[i] == null) continue;
            Assert.AreNotEqual(before[i], children[i].sharedMaterial,
                "без обхода детей наезд выреза варочной на боковину был бы виден только "
                + "по самой боковине: " + ElementRenderers.PathOf(cooktop, children[i]));
            Assert.AreEqual(before[i], ValidityTint.OwnOf(children[i].sharedMaterial),
                "тон обязан помнить СВОЙ источник порендерно — иначе снятие нарушения "
                + "вернёт чужой материал: " + ElementRenderers.PathOf(cooktop, children[i]));
        }
    }
}
