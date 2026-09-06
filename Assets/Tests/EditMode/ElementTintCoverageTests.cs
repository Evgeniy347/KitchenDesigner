using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using KitchenDesigner.Core;

/// <summary>Жёлтая тонировка выделения и назначенный декор обязаны накрывать
/// элемент ЦЕЛИКОМ, а не только тот меш, что оказался на корне.
///
/// Почему сторож перебирает типы, а не чинит шесть случаев поимённо. Дефект
/// не логируется, ничем не падает и не ломает ни одного существующего теста:
/// подсветка зовёт <c>element.GetComponent&lt;MeshRenderer&gt;()</c> — ОДИН
/// рендерер на корне. У стула на корне сиденье, у стола столешница, у дивана и
/// кровати основание; у двери, окна и унитаза корневого меша нет вовсе, и они
/// не подсвечиваются НИКАК. Пользователь увидел шесть симптомов одной строки.
/// Починить их по одному значит оставить седьмой тип открытым: он сломается
/// ровно так же и снова не покажет ничего.
///
/// Поэтому список типов здесь не выписан руками — он берётся из
/// <c>EveryElementType.Makers</c>, то есть заводится настоящей фабрикой, и
/// новый тип попадает под проверку сам. Ни один тип не назван в утверждениях:
/// падение перечисляет типы и ИМЕНА рендереров, которые остались нетронутыми.
///
/// Что считается телом элемента, решает одна функция —
/// <c>ElementRenderers.BodyOf</c> в боевом коде, — и её же зовёт этот набор.
/// Второе описание того же контура (свой обход дерева внутри теста) сошлось бы
/// само с собой и не проверило бы ничего. Чинящая подсветку правка обязана
/// звать ту же функцию.</summary>
public class ElementTintCoverageTests
{
    private const string DecorId = "tint-coverage-decor";

    private ProjectLoadStateGuard? _globals;
    private GameObject? _selectionGo;
    private SelectionManager? _selection;

    [SetUp]
    public void SetUp()
    {
        IgnoreMaterialLeakLog();
        _globals = ProjectLoadStateGuard.Capture();
        EveryElementType.ClearScene();
        MaterialManager.ClearCache();

        MaterialCatalog.Register(new MaterialDef(DecorId, "Декор сторожа", "ЛДСП",
            new Color(0.13f, 0.47f, 0.29f)));

        _selectionGo = new GameObject("SelectionManager сторожа");
        _selection = _selectionGo.AddComponent<SelectionManager>();
        SelectionManager.Instance = _selection;
    }

    [TearDown]
    public void TearDown()
    {
        IgnoreMaterialLeakLog();
        if (_selection != null) _selection.DeselectAll();
        SelectionManager.Instance = null;
        if (_selectionGo != null) UnityEngine.Object.DestroyImmediate(_selectionGo);
        _selectionGo = null;
        _selection = null;

        EveryElementType.ClearScene();
        MaterialManager.ClearCache();
        MaterialCatalog.Reset();
        _globals?.Restore();
        LogAssert.ignoreFailingMessages = false;
    }

    /// <summary>Подсветка зовёт <c>renderer.material</c>, а Unity в EditMode
    /// пишет об этом ошибку про утечку материала в сцену. Флаг сбрасывается
    /// перед телом каждого теста, поэтому его ставят и в SetUp, и в тесте
    /// (так же поступают SelectionManagerTests и MaterialPreviewTests).</summary>
    private static void IgnoreMaterialLeakLog() => LogAssert.ignoreFailingMessages = true;

    [Test]
    public void SelectingAnElement_TintsEveryOneOfItsRenderers_NotOnlyTheRootOne()
    {
        IgnoreMaterialLeakLog();
        var offenders = new List<string>();

        foreach (var maker in EveryElementType.Makers)
        {
            var element = EveryElementType.Spawn(maker.type, "подсветка-" + maker.type.Name);
            var body = ElementRenderers.BodyOf(element);

            if (body.Count == 0)
            {
                offenders.Add(maker.type.Name + ": НИ ОДНОГО MeshRenderer в теле элемента — "
                    + "подсвечивать нечего, объект не может стать жёлтым в принципе");
                Reset();
                continue;
            }

            var before = new Material[body.Count];
            for (int i = 0; i < body.Count; i++) before[i] = body[i].sharedMaterial;

            _selection!.Select(element);

            var missed = new List<string>();
            for (int i = 0; i < body.Count; i++)
                if (ReferenceEquals(body[i].sharedMaterial, before[i]))
                    missed.Add(ElementRenderers.PathOf(element, body[i]));

            if (missed.Count > 0)
                offenders.Add(maker.type.Name + ": без подсветки остались "
                    + missed.Count + " из " + body.Count + " — " + string.Join(", ", missed));

            Reset();
        }

        Fail("Выделение обязано накрывать элемент целиком, включая дочерние меши.\n"
            + "Тонировка ничего не логирует и ничем не падает: пока сторож молчал, "
            + "у составных элементов желтел только корневой меш, а у типов без меша на "
            + "корне не желтело ничего.\n", offenders);
    }

    [Test]
    public void DeselectingAnElement_GivesEveryRendererItsOwnMaterialBack()
    {
        IgnoreMaterialLeakLog();
        var offenders = new List<string>();

        foreach (var maker in EveryElementType.Makers)
        {
            var element = EveryElementType.Spawn(maker.type, "снятие-" + maker.type.Name);
            var body = ElementRenderers.BodyOf(element);
            if (body.Count == 0)
            {
                Reset();
                continue;
            }

            var before = new Material[body.Count];
            for (int i = 0; i < body.Count; i++) before[i] = body[i].sharedMaterial;

            _selection!.Select(element);
            _selection!.DeselectAll();

            var stuck = new List<string>();
            for (int i = 0; i < body.Count; i++)
                if (!ReferenceEquals(body[i].sharedMaterial, before[i]))
                    stuck.Add(ElementRenderers.PathOf(element, body[i]));

            if (stuck.Count > 0)
                offenders.Add(maker.type.Name + ": после снятия выделения не вернулись "
                    + stuck.Count + " из " + body.Count + " — " + string.Join(", ", stuck));

            Reset();
        }

        Fail("Снятие выделения обязано вернуть КАЖДОМУ рендереру его материал: "
            + "жёлтый, застрявший на дочернем меше, переживёт клик по пустому месту "
            + "и останется на объекте навсегда.\n", offenders);
    }

    /// <summary>Тот же вопрос про декор: назначенная текстура доходит до
    /// каждого рендерера, а не до первого попавшегося.
    ///
    /// Судим не по всем мешам подряд, а по тем, что УЖЕ носят декор элемента на
    /// момент постройки: у двери это семь брусков коробки и наличников, у доски
    /// один корневой меш, а стекло и хромированные детали в декоре не участвуют
    /// вовсе и в счёт не идут. Такой вопрос сторож может задать любому типу, не
    /// зная ни одной роли дочерних объектов.</summary>
    [Test]
    public void ApplyingADecor_ReachesEveryRendererThatWasWearingTheOldOne()
    {
        IgnoreMaterialLeakLog();
        var offenders = new List<string>();
        var covered = 0;

        foreach (var maker in EveryElementType.Makers)
        {
            var element = EveryElementType.Spawn(maker.type, "декор-" + maker.type.Name);

            var oldDecor = MaterialManager.GetSharedMaterial(
                MaterialCatalog.Get(element.MaterialId));

            var wearing = new List<MeshRenderer>();
            foreach (var renderer in ElementRenderers.BodyOf(element))
                if (oldDecor != null && ReferenceEquals(renderer.sharedMaterial, oldDecor))
                    wearing.Add(renderer);

            if (wearing.Count == 0)
            {
                Reset();
                continue;
            }

            covered++;
            var def = MaterialCatalog.Get(DecorId);
            ApplyDecorThroughEverySlot(element, def);
            var newDecor = MaterialManager.GetSharedMaterial(def);

            var missed = new List<string>();
            foreach (var renderer in wearing)
                if (!ReferenceEquals(renderer.sharedMaterial, newDecor))
                    missed.Add(ElementRenderers.PathOf(element, renderer));

            if (missed.Count > 0)
                offenders.Add(maker.type.Name + ": декор не дошёл до "
                    + missed.Count + " из " + wearing.Count + " — " + string.Join(", ", missed));

            Reset();
        }

        Assert.Greater(covered, 0,
            "ни у одного типа не нашлось меша, носящего декор элемента — сторож "
            + "проверил пустоту и позеленел бы на любом коде");

        Fail("Декор обязан доходить до каждого меша, который его носил.\n"
            + "Так у двери красился ОДИН наличник: MaterialManager.Apply берёт "
            + "GetComponentInChildren<MeshRenderer>() — первый рендерер поддерева.\n", offenders);
    }

    /// <summary>Сторож самого сторожа. Если тело элемента вдруг перестанет
    /// содержать составные объекты — обход сузился, а не мебель упростилась, —
    /// три теста выше позеленеют, ничего не проверив.</summary>
    [Test]
    public void TheBodyWalk_SeesTheChildrenOfACompositeElement_OtherwiseItProvesNothing()
    {
        IgnoreMaterialLeakLog();

        var chair = EveryElementType.Spawn(typeof(ChairElement), "обход-стул");
        Assert.Greater(ElementRenderers.BodyOf(chair).Count, 1,
            "у стула ножки и спинка — отдельные объекты со своими MeshRenderer; "
            + "обход, вернувший один рендерер, видит только корень");
        Reset();

        var door = EveryElementType.Spawn(typeof(DoorElement), "обход-дверь");
        Assert.IsNull(door.gameObject.GetComponent<MeshRenderer>(),
            "у двери меша на корне нет — именно поэтому она не подсвечивалась ничем");
        Assert.Greater(ElementRenderers.BodyOf(door).Count, 1,
            "коробка и наличники двери — дочерние объекты; без них проверять нечего");
        Reset();
    }

    private static void ApplyDecorThroughEverySlot(KitchenElement element, MaterialDef def)
    {
        if (element is IHasTwoDecorSlots slots)
        {
            MaterialManager.ApplyPrimarySlot(slots, def);
            MaterialManager.ApplySecondarySlot(slots, def);
            return;
        }
        MaterialManager.Apply(element, def);
    }

    private void Reset()
    {
        if (_selection != null) _selection.DeselectAll();
        EveryElementType.ClearScene();
    }

    private static void Fail(string header, List<string> offenders)
    {
        if (offenders.Count == 0) return;
        Assert.Fail(header + offenders.Count + " тип(ов):\n  " + string.Join("\n  ", offenders));
    }
}
