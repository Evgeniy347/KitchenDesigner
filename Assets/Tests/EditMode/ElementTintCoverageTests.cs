using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
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
/// Поэтому список типов здесь не выписан руками — он берётся из сборки, и новый
/// тип попадает под проверку сам. Ни один тип не назван в утверждениях: падение
/// перечисляет типы и ИМЕНА рендереров, которые остались нетронутыми.
///
/// Что считается телом элемента, решает одна функция —
/// <c>ElementRenderers.BodyOf</c> в боевом коде, — и её же зовёт общий проход.
/// Второе описание того же контура (свой обход дерева внутри теста) сошлось бы
/// само с собой и не проверило бы ничего. Чинящая подсветку правка обязана
/// звать ту же функцию.
///
/// Спавн всех типов настоящей фабрикой — единственное, что здесь дорого, и он
/// НЕ свой: четыре теста ниже читали четыре собственных прохода (~148 спавнов),
/// теперь все четыре читают показания одного общего
/// <see cref="ElementSurfaceSweep"/> — один проход на весь прогон EditMode.
/// Вопросы не изменились ни на один.</summary>
public class ElementTintCoverageTests
{
    [Test]
    public void SelectingAnElement_TintsEveryOneOfItsRenderers_NotOnlyTheRootOne()
    {
        var offenders = new List<string>();

        foreach (var row in ElementSurfaceSweep.Rows)
        {
            if (row.BodyCount == 0)
            {
                offenders.Add(row.Name + ": НИ ОДНОГО MeshRenderer в теле элемента — "
                    + "подсвечивать нечего, объект не может стать жёлтым в принципе");
                continue;
            }

            if (row.SelectMissed.Count > 0)
                offenders.Add(row.Name + ": без подсветки остались "
                    + row.SelectMissed.Count + " из " + row.BodyCount + " — "
                    + string.Join(", ", row.SelectMissed));
        }

        Fail("Выделение обязано накрывать элемент целиком, включая дочерние меши.\n"
            + "Тонировка ничего не логирует и ничем не падает: пока сторож молчал, "
            + "у составных элементов желтел только корневой меш, а у типов без меша на "
            + "корне не желтело ничего.\n", offenders);
    }

    [Test]
    public void DeselectingAnElement_GivesEveryRendererItsOwnMaterialBack()
    {
        var offenders = new List<string>();

        foreach (var row in ElementSurfaceSweep.Rows)
            if (row.SelectStuck.Count > 0)
                offenders.Add(row.Name + ": после снятия выделения не вернулись "
                    + row.SelectStuck.Count + " из " + row.BodyCount + " — "
                    + string.Join(", ", row.SelectStuck));

        Fail("Снятие выделения обязано вернуть КАЖДОМУ рендереру его материал: "
            + "жёлтый, застрявший на дочернем меше, переживёт клик по пустому месту "
            + "и останется на объекте навсегда.\n", offenders);
    }

    /// <summary>Тот же обход, но вопрос другой: изменился ли ЦВЕТ.
    ///
    /// Сторож выше сравнивает материалы ПО ССЫЛКЕ, и это его потолок: он
    /// зеленеет от любого другого материала на рендерере — от приглушённого
    /// серого «вне редактируемого модуля», от красного «нарушение», от копии,
    /// которую Unity сделала сама. «Материал не тот же самый» и «пользователь
    /// увидел подсветку» — разные утверждения, и второе сильнее.
    ///
    /// Кадровый тест в PlayMode задаёт этот вопрос камерой, но всего двум типам
    /// и ценой запуска Unity в PlayMode. Здесь тот же вопрос задаётся всем
    /// тридцати и бесплатно: цвет читается прямо с материала. Читается именно с
    /// <c>sharedMaterial</c> — обращение к <c>renderer.material</c> подменило бы
    /// материал копией и испортило бы сцену следующему сторожу (CONVENTIONS.md →
    /// «Reading `renderer.material` is a MUTATION, not an observation»).</summary>
    [Test]
    public void SelectingAnElement_ChangesTheCOLOUR_NotOnlyTheMaterialReference()
    {
        var offenders = new List<string>();
        var covered = ElementSurfaceSweep.Rows.Count(r => r.ColourWatched > 0);

        foreach (var row in ElementSurfaceSweep.Rows)
            if (row.SelectPale.Count > 0)
                offenders.Add(row.Name + ": цвет не изменился на "
                    + row.SelectPale.Count + " из " + row.ColourWatched + " — "
                    + string.Join(", ", row.SelectPale));

        Assert.Greater(covered, 0,
            "ни у одного типа не нашлось рендерера с читаемым цветом — сторож "
            + "проверил пустоту и позеленел бы на любом коде");

        Fail("Выделение обязано менять ЦВЕТ, а не только ссылку на материал.\n"
            + "Рендерер, получивший другой материал того же цвета, для сторожа по "
            + "ссылкам неотличим от подсвеченного, а для пользователя — от серого.\n",
            offenders);
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
        var offenders = new List<string>();
        var covered = ElementSurfaceSweep.Rows.Count(r => r.DecorWearing > 0);

        foreach (var row in ElementSurfaceSweep.Rows)
            if (row.DecorMissed.Count > 0)
                offenders.Add(row.Name + ": декор не дошёл до "
                    + row.DecorMissed.Count + " из " + row.DecorWearing + " — "
                    + string.Join(", ", row.DecorMissed));

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
        var chair = ElementSurfaceSweep.Of(typeof(ChairElement));
        Assert.Greater(chair.BodyCount, 1,
            "у стула ножки и спинка — отдельные объекты со своими MeshRenderer; "
            + "обход, вернувший один рендерер, видит только корень");

        var door = ElementSurfaceSweep.Of(typeof(DoorElement));
        Assert.IsFalse(door.HasRootRenderer,
            "у двери меша на корне нет — именно поэтому она не подсвечивалась ничем");
        Assert.Greater(door.BodyCount, 1,
            "коробка и наличники двери — дочерние объекты; без них проверять нечего");
    }

    private static void Fail(string header, List<string> offenders)
    {
        if (offenders.Count == 0) return;
        Assert.Fail(header + offenders.Count + " тип(ов):\n  " + string.Join("\n  ", offenders));
    }
}
