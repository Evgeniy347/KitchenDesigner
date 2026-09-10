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
    /// корпус, а не один общий красный на всех.
    ///
    /// Две варочные одна на другой нарушение НЕ вносят: `CooktopElement` несёт
    /// `ElementKind.Recessed`, а `ValidationElement.IgnoredInPairs` выключает
    /// ОБЫЧНУЮ проверку наложения для Decor|Recessed целиком
    /// (`ValidationCore.ProcessPair`, ранний `return` до AABB-теста) — варочная
    /// обязана легально лежать в проёме столешницы. Единственный путь довести
    /// её до нарушения — тот же, что и в
    /// `CooktopElementTests.Validator_CooktopOverSidePanel_IsViolation`:
    /// присосать к столешнице (`SnapToPart`) и подсунуть под неё боковину,
    /// на которую наедет короб выреза (`CheckExtraBodyAgainstNeighbour` +
    /// `IsCarcass`, обходящий тот же ранний `IgnoredInPairs`).</summary>
    [Test]
    public void Cooktop_Invalid_TintsEveryChildBox_ThoughTheRootCarriesNoRenderer()
    {
        const float ToU = AppConstants.MM_TO_UNITS;
        const int TopThicknessMM = 38;
        const float UnderTopY = -0.369f;

        var topGo = Spawn(GameObject.CreatePrimitive(PrimitiveType.Cube));
        var top = topGo.AddComponent<KitchenElement>();
        top.PartName = "Countertop";
        top.transform.rotation = ManagedRotation.Euler(-90f, 0f, 0f);
        top.DimensionsMM = new Vector3Int(2000, 1200, TopThicknessMM);
        PartRegistry.Register(top);

        var sideGo = Spawn(GameObject.CreatePrimitive(PrimitiveType.Cube));
        var side = sideGo.AddComponent<KitchenElement>();
        side.PartName = "Side";
        side.transform.rotation = ManagedRotation.Euler(0f, 90f, 0f);
        side.DimensionsMM = new Vector3Int(560, 700, 18);
        side.transform.position = new Vector3(0f, UnderTopY, 0f);
        PartRegistry.Register(side);

        float topSurfaceY = top.transform.position.y + TopThicknessMM * 0.5f * ToU;
        var go = Spawn(ElementFactory.CreateCooktop("CooktopTint", new Vector3(0f, topSurfaceY + 0.02f, 0f)));
        var cooktop = go.GetComponent<CooktopElement>()!;
        cooktop.SnapToPart();
        Assert.IsTrue(cooktop.IsAttached,
            "предусловие: варочная обязана сесть на столешницу — иначе короб выреза не строится "
            + "и наезжать на боковину нечему");

        Assert.IsNull(go.GetComponent<MeshRenderer>(),
            "варочная собрана из дочерних коробок: на корне рендерера нет, красить её через корень нечем");
        var children = ElementRenderers.BodyOf(cooktop);
        Assert.Greater(children.Count, 0, "дочерние коробки должны существовать, иначе тест пустой");
        var before = new Material[children.Count];
        for (int i = 0; i < children.Count; i++) before[i] = children[i].sharedMaterial;

        Assert.IsTrue(ConstraintValidator.Validate(PartRegistry.GetAll()).violations.Contains(cooktop),
            "предусловие: короб выреза обязан наехать на боковину под столешницей — иначе "
            + "тонировать нечего");

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

    private KitchenElement MakeWall(Vector3Int dims, string name)
    {
        var go = Spawn(ElementFactory.CreateWall(dims, name, Vector3.zero));
        return go.GetComponent<KitchenElement>()!;
    }

    /// <summary>Стена, у которой есть свой материал, и пустая сцена вокруг:
    /// оба флага сняты, тонировать нечего. Дальше тесты этого раздела двигают
    /// ровно один флаг за раз.</summary>
    private KitchenElement PlainWall(string name)
    {
        var wall = MakeWall(new Vector3Int(3000, 2500, 100), name);
        Assume.That(wall.GetComponent<MeshRenderer>()!.sharedMaterial, Is.Not.Null,
            "фабрика обязана дать стене её материал: и тон нарушения, и прозрачность "
            + "ВЫВОДЯТСЯ из материала рендерера, а выводить из ничего нельзя");
        Assume.That(PhotoMode.Active, Is.False, "фоторежим гасит и прозрачность, и тон");
        Assume.That(ModuleEditMode.IsActive, Is.False, "режим модуля глушит всё остальное");
        Assume.That(ConstraintValidator.Validate(PartRegistry.GetAll()).violations.Contains(wall),
            Is.False, "одинокая стена обязана быть валидной, иначе проверяется тон нарушения");
        ElementHighlighter.ViolationTintVisible = true;
        return wall;
    }

    /// <summary>«Прозрачный» обязан выключаться обратно, и выключение ставит на
    /// рендерер РОВНО тот материал, который на нём был. Прежний возврат звал
    /// <c>MaterialManager.ApplyOwnDecor</c>, а тот на дефолтном
    /// <c>MaterialId</c> красит серым ЛДСП — стена возвращалась не к себе, а к
    /// заводскому декору. Сравнение по ссылке здесь и есть вопрос: «тот же
    /// самый материал?», а не «похожий на вид».
    ///
    /// Прежняя версия теста собирала стену руками — <c>GameObject</c> с пустым
    /// <c>MeshRenderer</c>, без материала вовсе, — и проходила только потому,
    /// что подсветка клала на неё ПРЕДСОБРАННЫЙ прозрачный материал, а
    /// возвращала <c>ApplyOwnDecor</c>. Обе опоры удалены вместе со сплошной
    /// заливкой, поэтому стена здесь настоящая, из фабрики.</summary>
    [Test]
    public void WallTransparency_CanBeTurnedBackOff()
    {
        var wall = PlainWall("СтенаПрозрачность");
        var renderer = wall.GetComponent<MeshRenderer>()!;
        var own = renderer.sharedMaterial;

        wall.Transparent = true;
        _highlighter!.ApplyForElement(wall);

        Assert.AreEqual((int)UnityEngine.Rendering.RenderQueue.Transparent,
            renderer.sharedMaterial.renderQueue, "стена стала прозрачной");
        Assert.AreEqual(own, ValidityTint.OwnOf(renderer.sharedMaterial),
            "прозрачная краска обязана помнить источник — иначе возвращать будет нечего");

        wall.Transparent = false;
        _highlighter!.ApplyForElement(wall);

        Assert.AreNotEqual((int)UnityEngine.Rendering.RenderQueue.Transparent,
            renderer.sharedMaterial.renderQueue, "и вернулась обратно");
        Assert.AreEqual(own, renderer.sharedMaterial,
            "возврат ставит ЗАПОМНЕННЫЙ материал, а не красит заново своим декором");
    }

    /// <summary>Противоположный вход к предыдущему тесту: рендерер носит
    /// материал, которого у декора элемента нет вовсе — так делают ванна,
    /// варочная и духовка. Возврат из прозрачности обязан вернуть именно его;
    /// «покрасить своим декором заново» отдало бы серый ЛДСП, и стеклокерамика
    /// стала бы ЛДСП после одного нажатия «Прозрачный».</summary>
    [Test]
    public void Transparency_ReturnsTheMaterialTheRendererWore_NotWhatTheElementDecorWouldRepaint()
    {
        var wall = PlainWall("СтенаЧужойМатериал");
        var renderer = wall.GetComponent<MeshRenderer>()!;
        var decor = renderer.sharedMaterial;

        var painted = new Material(decor);
        painted.name = "Эмаль ванны";
        painted.SetColor("_BaseColor", new Color(0.05f, 0.35f, 0.75f, 1f));
        renderer.sharedMaterial = painted;
        Assume.That(Visibly(ValidityTint.BaseColorOf(decor), ValidityTint.BaseColorOf(painted)),
            Is.True, "материал мимо декора обязан отличаться по цвету, иначе подмену не увидеть");

        wall.Transparent = true;
        _highlighter!.ApplyForElement(wall);
        wall.Transparent = false;
        _highlighter!.ApplyForElement(wall);

        Assert.AreEqual(painted, renderer.sharedMaterial,
            "вернулся не тот материал: " + Describe(ValidityTint.BaseColorOf(painted)) + " → "
            + Describe(ValidityTint.BaseColorOf(renderer.sharedMaterial)));
    }

    /// <summary>Два независимых флага — «прозрачный» и «нарушает» — дают
    /// четыре состояния, и расходятся такие пары именно на СНЯТИИ. Здесь оба
    /// подняты: прозрачный красный, а не выбор одного из двух.</summary>
    [Test]
    public void TransparentAndViolating_IsBothAtOnce_SeeThroughAndRed()
    {
        var wall = PlainWall("СтенаПрозрачнаяИНарушает");
        var renderer = wall.GetComponent<MeshRenderer>()!;
        var own = renderer.sharedMaterial;
        var ownColour = ValidityTint.BaseColorOf(own);

        wall.Transparent = true;
        ElementHighlighter.ApplyMaterial(wall, isValid: false);

        var now = renderer.sharedMaterial;
        var colour = ValidityTint.BaseColorOf(now);
        Assert.AreEqual((int)UnityEngine.Rendering.RenderQueue.Transparent, now.renderQueue,
            "нарушение не отменяет прозрачность: стена сквозная и в нарушении");
        Assert.AreEqual(ValidityTint.SeeThroughAlpha, colour.a, 1e-3f,
            "и остаётся такой же еле заметной, как без нарушения");
        Assert.IsTrue(Visibly(ownColour, colour),
            "прозрачность не отменяет тон: сквозная стена с нарушением обязана краснеть, "
            + Describe(ownColour) + " → " + Describe(colour));
        Assert.AreEqual(own, ValidityTint.OwnOf(now),
            "и помнить источник — снятие любого из двух флагов идёт от него");
    }

    /// <summary>Снятие ОДНОГО флага возвращает к правильному промежуточному
    /// состоянию, а не к серому и не к плотному: нарушение ушло, прозрачность
    /// осталась.</summary>
    [Test]
    public void ViolationDropped_WhileStillTransparent_StaysSeeThroughInItsOwnColour()
    {
        var wall = PlainWall("СтенаСнялиНарушение");
        var renderer = wall.GetComponent<MeshRenderer>()!;
        var own = renderer.sharedMaterial;
        var ownColour = ValidityTint.BaseColorOf(own);

        wall.Transparent = true;
        ElementHighlighter.ApplyMaterial(wall, isValid: false);
        ElementHighlighter.ApplyMaterial(wall, isValid: true);

        var now = renderer.sharedMaterial;
        var colour = ValidityTint.BaseColorOf(now);
        Assert.AreEqual((int)UnityEngine.Rendering.RenderQueue.Transparent, now.renderQueue,
            "«Прозрачный» никто не выключал — стена обязана остаться сквозной");
        Assert.AreEqual(ValidityTint.SeeThroughAlpha, colour.a, 1e-3f);
        Assert.IsFalse(Visibly(ownColour, colour),
            "и вернуться к СВОЕМУ цвету: ни красного тона, ни серого ЛДСП, "
            + Describe(ownColour) + " → " + Describe(colour));
        Assert.AreEqual(own, ValidityTint.OwnOf(now),
            "источник тот же — прозрачная краска после снятия нарушения выводится от него");
    }

    /// <summary>Тот же переход с другой стороны: прозрачность выключили,
    /// нарушение осталось. Стена обязана стать плотной и остаться красной —
    /// «вернули как было» здесь спрятало бы нарушение.</summary>
    [Test]
    public void TransparencyDropped_WhileStillViolating_BecomesOpaqueAndStaysRed()
    {
        var wall = PlainWall("СтенаСнялиПрозрачность");
        var renderer = wall.GetComponent<MeshRenderer>()!;
        var own = renderer.sharedMaterial;
        var ownColour = ValidityTint.BaseColorOf(own);

        wall.Transparent = true;
        ElementHighlighter.ApplyMaterial(wall, isValid: false);
        wall.Transparent = false;
        ElementHighlighter.ApplyMaterial(wall, isValid: false);

        var now = renderer.sharedMaterial;
        var colour = ValidityTint.BaseColorOf(now);
        Assert.AreNotEqual((int)UnityEngine.Rendering.RenderQueue.Transparent, now.renderQueue,
            "«Прозрачный» выключен — стена обязана стать плотной");
        Assert.AreEqual(ownColour.a, colour.a, 1e-3f,
            "и перестать быть еле заметной: альфа берётся у своего материала, а не "
            + "остаётся сквозной " + ValidityTint.SeeThroughAlpha.ToString("0.00"));
        Assert.IsTrue(Visibly(ownColour, colour),
            "нарушение никто не снимал: тон обязан остаться, " + Describe(ownColour)
            + " → " + Describe(colour));
        Assert.AreEqual(own, ValidityTint.OwnOf(now),
            "источник остаётся тем же — вторая тонировка берётся от него, а не от "
            + "прозрачной краски, иначе цвет уползает с каждым нажатием");
    }

    private const float MinColorStep = 0.05f;

    private static bool Visibly(Color a, Color b) =>
        Mathf.Abs(a.r - b.r) > MinColorStep
        || Mathf.Abs(a.g - b.g) > MinColorStep
        || Mathf.Abs(a.b - b.b) > MinColorStep;

    private static string Describe(Color c) =>
        "(" + c.r.ToString("0.00") + " " + c.g.ToString("0.00") + " " + c.b.ToString("0.00")
        + " a" + c.a.ToString("0.00") + ")";
}
