using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
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
/// Список типов не выписан руками: он выводится из сборки, и новый тип попадает
/// под проверку сам. Что считается телом элемента, решает
/// <c>ElementRenderers.BodyOf</c> — та же функция, что зовёт боевой код; второй
/// обход дерева внутри теста сошёлся бы сам с собой и не проверил бы ничего.
///
/// Вторая половина сторожа — ВОЗВРАТ. У выделения наложение и возврат чинили
/// разными заходами, потому что первый сторож спрашивал только про наложение.
/// Здесь оба вопроса заданы сразу, и третий тест задаёт возврату тот вопрос,
/// на котором он ломается молча: после чтения <c>renderer.material</c> Unity
/// подменяет материал копией, и сравнение по ССЫЛКЕ принимает копию собственной
/// краски за чужую (CONVENTIONS.md → «Reading `renderer.material` is a MUTATION,
/// not an observation»).
///
/// Спавн всех типов настоящей фабрикой — единственное, что здесь дорого, и он
/// НЕ свой: три теста ниже читали три собственных прохода (~111 спавнов), теперь
/// все три читают показания одного общего <see cref="ElementSurfaceSweep"/>.
/// Вопросы не изменились ни на один; настоящий <c>SaveDragMaterial</c> и
/// настоящий <c>RestoreDragMaterial</c> зовутся внутри прохода, как и прежде.</summary>
public class DragTintCoverageTests
{
    [Test]
    public void DraggingAnElement_TintsEveryOneOfItsRenderers_NotOnlyTheRootOne()
    {
        var offenders = new List<string>();
        var covered = ElementSurfaceSweep.Rows.Count(r => r.BodyCount > 0);

        foreach (var row in ElementSurfaceSweep.Rows)
        {
            if (row.BodyCount == 0)
            {
                offenders.Add(row.Name + ": НИ ОДНОГО MeshRenderer в теле элемента — "
                    + "подсвечивать нечего, объект не может позеленеть в принципе");
                continue;
            }

            if (row.DragMissed.Count > 0)
                offenders.Add(row.Name + ": без подсветки перетаскивания остались "
                    + row.DragMissed.Count + " из " + row.BodyCount + " — "
                    + string.Join(", ", row.DragMissed));
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
        var offenders = new List<string>();
        var covered = ElementSurfaceSweep.Rows.Count(r => r.BodyCount > 0);

        foreach (var row in ElementSurfaceSweep.Rows)
            if (row.DragStuck.Count > 0)
                offenders.Add(row.Name + ": после броска не вернулись "
                    + row.DragStuck.Count + " из " + row.BodyCount + " — "
                    + string.Join(", ", row.DragStuck));

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
        var offenders = new List<string>();
        var covered = ElementSurfaceSweep.Rows.Count(r => r.BodyCount > 0);

        var noCopy = ElementSurfaceSweep.Rows.Where(r => r.BodyCount > 0 && !r.CopyOnRead)
            .Select(r => r.Name).ToList();
        Assume.That(noCopy, Is.Empty,
            "чтение renderer.material обязано подменить материал копией — иначе этот тест "
            + "проверяет не ту ситуацию, ради которой написан. Не подменило у: "
            + string.Join(", ", noCopy));

        foreach (var row in ElementSurfaceSweep.Rows)
            if (row.DragStuckAfterRead.Count > 0)
                offenders.Add(row.Name + ": после чтения материала не вернулись "
                    + row.DragStuckAfterRead.Count + " из " + row.BodyCount + " — "
                    + string.Join(", ", row.DragStuckAfterRead));

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
        var chair = ElementSurfaceSweep.Of(typeof(ChairElement));
        Assert.Greater(chair.BodyCount, 1,
            "у стула ножки и спинка — отдельные объекты со своими MeshRenderer; "
            + "обход, вернувший один рендерер, видит только корень");

        var door = ElementSurfaceSweep.Of(typeof(DoorElement));
        Assert.IsFalse(door.HasRootRenderer,
            "у двери меша на корне нет — именно поэтому при перетаскивании она "
            + "не зеленела ничем");
        Assert.Greater(door.BodyCount, 1,
            "коробка и наличники двери — дочерние объекты; без них проверять нечего");
    }

    private static void Fail(string header, List<string> offenders)
    {
        if (offenders.Count == 0) return;
        Assert.Fail(header + offenders.Count + " тип(ов):\n  " + string.Join("\n  ", offenders));
    }
}
