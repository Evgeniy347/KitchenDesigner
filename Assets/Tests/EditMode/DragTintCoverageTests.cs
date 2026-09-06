using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using KitchenDesigner.Core;

/// <summary>Зелёная подсветка ПЕРЕТАСКИВАНИЯ обязана накрывать элемент
/// целиком и обязана сходить с него целиком.
///
/// Тот же дефект, что был у выделения, и та же причина:
/// <c>ElementMover.SaveDragMaterial</c> красил через
/// <c>GetComponent&lt;MeshRenderer&gt;()</c> — ОДИН рендерер на корне. У стула
/// зеленело сиденье, у стола столешница, у двери, окна, унитаза, розетки и
/// выключателя не зеленело ничего: меша на корне у них нет. Выделение починили,
/// перетаскивание оставили — поэтому сторож отдельный, а не строчка в чужом.
///
/// Список типов не выписан руками: он берётся из <c>EveryElementType.Makers</c>,
/// то есть заводится настоящей фабрикой, и новый тип попадает под проверку сам.
/// Что считается телом элемента, решает <c>ElementRenderers.BodyOf</c> — та же
/// функция, что зовёт боевой код; второй обход дерева внутри теста сошёлся бы
/// сам с собой и не проверил бы ничего.
///
/// Вторая половина сторожа — ВОЗВРАТ. У выделения наложение и возврат чинили
/// разными заходами, потому что первый сторож спрашивал только про наложение.
/// Здесь оба вопроса заданы сразу, и третий тест задаёт возврату тот вопрос,
/// на котором он ломается молча: после чтения <c>renderer.material</c> Unity
/// подменяет материал копией, и сравнение по ССЫЛКЕ принимает копию собственной
/// краски за чужую (CONVENTIONS.md → «Reading `renderer.material` is a MUTATION,
/// not an observation»).</summary>
public class DragTintCoverageTests
{
    private ProjectLoadStateGuard? _globals;
    private GameObject? _moverGo;
    private ElementMover? _mover;

    [SetUp]
    public void SetUp()
    {
        IgnoreMaterialLeakLog();
        _globals = ProjectLoadStateGuard.Capture();
        EveryElementType.ClearScene();
        MaterialManager.ClearCache();

        _moverGo = new GameObject("ElementMover сторожа");
        _mover = _moverGo.AddComponent<ElementMover>();
    }

    [TearDown]
    public void TearDown()
    {
        IgnoreMaterialLeakLog();
        if (_moverGo != null) UnityEngine.Object.DestroyImmediate(_moverGo);
        _moverGo = null;
        _mover = null;

        EveryElementType.ClearScene();
        MaterialManager.ClearCache();
        MaterialCatalog.Reset();
        _globals?.Restore();
        LogAssert.ignoreFailingMessages = false;
    }

    /// <summary>Подсветка зовёт <c>renderer.material</c>, а Unity в EditMode
    /// пишет об этом ошибку про утечку материала в сцену; <c>Destroy</c> вне
    /// Play mode тоже ругается в лог. Флаг сбрасывается перед телом каждого
    /// теста, поэтому его ставят и в SetUp, и в тесте.</summary>
    private static void IgnoreMaterialLeakLog() => LogAssert.ignoreFailingMessages = true;

    [Test]
    public void DraggingAnElement_TintsEveryOneOfItsRenderers_NotOnlyTheRootOne()
    {
        IgnoreMaterialLeakLog();
        var offenders = new List<string>();
        var covered = 0;

        foreach (var maker in EveryElementType.Makers)
        {
            var element = EveryElementType.Spawn(maker.type, "драг-" + maker.type.Name);
            var body = ElementRenderers.BodyOf(element);

            if (body.Count == 0)
            {
                offenders.Add(maker.type.Name + ": НИ ОДНОГО MeshRenderer в теле элемента — "
                    + "подсвечивать нечего, объект не может позеленеть в принципе");
                Reset();
                continue;
            }

            var before = new Material[body.Count];
            for (int i = 0; i < body.Count; i++) before[i] = body[i].sharedMaterial;

            covered++;
            _mover!.SaveDragMaterial(element);

            var missed = new List<string>();
            for (int i = 0; i < body.Count; i++)
                if (ReferenceEquals(body[i].sharedMaterial, before[i]))
                    missed.Add(ElementRenderers.PathOf(element, body[i]));

            if (missed.Count > 0)
                offenders.Add(maker.type.Name + ": без подсветки перетаскивания остались "
                    + missed.Count + " из " + body.Count + " — " + string.Join(", ", missed));

            _mover!.RestoreDragMaterial();
            Reset();
        }

        Assert.Greater(covered, 0,
            "ни у одного типа не нашлось рендерера — сторож проверил пустоту и "
            + "позеленел бы на любом коде");

        Fail("Перетаскивание обязано красить элемент целиком, включая дочерние меши.\n"
            + "Дефект ничего не логирует и ничем не падает: у составных элементов "
            + "зеленел только корневой меш, а у типов без меша на корне — ничего.\n", offenders);
    }

    [Test]
    public void DroppingAnElement_GivesEveryRendererItsOwnMaterialBack()
    {
        IgnoreMaterialLeakLog();
        var offenders = new List<string>();
        var covered = 0;

        foreach (var maker in EveryElementType.Makers)
        {
            var element = EveryElementType.Spawn(maker.type, "возврат-" + maker.type.Name);
            var body = ElementRenderers.BodyOf(element);
            if (body.Count == 0)
            {
                Reset();
                continue;
            }

            var before = new Material[body.Count];
            for (int i = 0; i < body.Count; i++) before[i] = body[i].sharedMaterial;

            covered++;
            _mover!.SaveDragMaterial(element);
            _mover!.RestoreDragMaterial();

            var stuck = new List<string>();
            for (int i = 0; i < body.Count; i++)
                if (!ReferenceEquals(body[i].sharedMaterial, before[i]))
                    stuck.Add(ElementRenderers.PathOf(element, body[i]));

            if (stuck.Count > 0)
                offenders.Add(maker.type.Name + ": после броска не вернулись "
                    + stuck.Count + " из " + body.Count + " — " + string.Join(", ", stuck));

            Reset();
        }

        Assert.Greater(covered, 0,
            "ни у одного типа не нашлось рендерера — сторож проверил пустоту и "
            + "позеленел бы на любом коде");

        Fail("Бросок обязан вернуть КАЖДОМУ рендереру его материал: зелёный, "
            + "застрявший на детали, переживёт конец перетаскивания и останется "
            + "на объекте навсегда.\n", offenders);
    }

    /// <summary>Тот же возврат, но краску перед ним ЧИТАЮТ.
    ///
    /// Чтение <c>renderer.material</c> — мутация: Unity подменяет материал
    /// копией и отдаёт копию. Возврат, узнающий свою краску по ССЫЛКЕ, после
    /// такого чтения видит чужой материал и молча ничего не возвращает. В
    /// приложении это делал сам <c>SaveDragMaterial</c> строкой
    /// <c>_dragOriginalMaterial = renderer.material</c> — то есть дыра
    /// открывалась в начале КАЖДОГО перетаскивания.
    ///
    /// Поэтому здесь возврат проверяется после честного чтения, а само чтение
    /// подтверждается <c>Assume</c>: если Unity когда-нибудь перестанет копировать,
    /// тест обязан стать неопределённым, а не ложно зелёным.</summary>
    [Test]
    public void DroppingAnElement_GivesTheMaterialBack_EvenAfterSomethingReadRendererMaterial()
    {
        IgnoreMaterialLeakLog();
        var offenders = new List<string>();
        var covered = 0;

        foreach (var maker in EveryElementType.Makers)
        {
            var element = EveryElementType.Spawn(maker.type, "чтение-" + maker.type.Name);
            var body = ElementRenderers.BodyOf(element);
            if (body.Count == 0)
            {
                Reset();
                continue;
            }

            var before = new Material[body.Count];
            for (int i = 0; i < body.Count; i++) before[i] = body[i].sharedMaterial;

            covered++;
            _mover!.SaveDragMaterial(element);

            foreach (var renderer in body)
            {
                var painted = renderer.sharedMaterial;
                var copy = renderer.material;
                if (painted != null)
                    Assume.That(ReferenceEquals(copy, painted), Is.False,
                        "чтение renderer.material обязано подменить материал копией — "
                        + "иначе этот тест проверяет не ту ситуацию, ради которой написан");
            }

            _mover!.RestoreDragMaterial();

            var stuck = new List<string>();
            for (int i = 0; i < body.Count; i++)
                if (!ReferenceEquals(body[i].sharedMaterial, before[i]))
                    stuck.Add(ElementRenderers.PathOf(element, body[i]));

            if (stuck.Count > 0)
                offenders.Add(maker.type.Name + ": после чтения материала не вернулись "
                    + stuck.Count + " из " + body.Count + " — " + string.Join(", ", stuck));

            Reset();
        }

        Assert.Greater(covered, 0,
            "ни у одного типа не нашлось рендерера — сторож проверил пустоту и "
            + "позеленел бы на любом коде");

        Fail("Возврат обязан узнавать свою краску по ИМЕНИ, а не по адресу.\n"
            + "Сравнение по ссылке после чтения renderer.material принимает копию "
            + "собственной краски за чужую и оставляет зелёное навсегда.\n", offenders);
    }

    /// <summary>Сторож самого сторожа. Если тело элемента перестанет содержать
    /// составные объекты — обход сузился, а не мебель упростилась, — три теста
    /// выше позеленеют, ничего не проверив.</summary>
    [Test]
    public void TheBodyWalk_SeesTheChildrenOfADraggedCompositeElement_OtherwiseItProvesNothing()
    {
        IgnoreMaterialLeakLog();

        var chair = EveryElementType.Spawn(typeof(ChairElement), "обход-драг-стул");
        Assert.Greater(ElementRenderers.BodyOf(chair).Count, 1,
            "у стула ножки и спинка — отдельные объекты со своими MeshRenderer; "
            + "обход, вернувший один рендерер, видит только корень");
        Reset();

        var door = EveryElementType.Spawn(typeof(DoorElement), "обход-драг-дверь");
        Assert.IsNull(door.gameObject.GetComponent<MeshRenderer>(),
            "у двери меша на корне нет — именно поэтому при перетаскивании она "
            + "не зеленела ничем");
        Assert.Greater(ElementRenderers.BodyOf(door).Count, 1,
            "коробка и наличники двери — дочерние объекты; без них проверять нечего");
        Reset();
    }

    private void Reset() => EveryElementType.ClearScene();

    private static void Fail(string header, List<string> offenders)
    {
        if (offenders.Count == 0) return;
        Assert.Fail(header + offenders.Count + " тип(ов):\n  " + string.Join("\n  ", offenders));
    }
}
