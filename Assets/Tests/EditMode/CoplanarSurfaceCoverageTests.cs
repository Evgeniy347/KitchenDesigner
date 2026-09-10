using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using KitchenDesigner.Core;

/// <summary>Мерцание на лице дверцы духовки (z-fighting, `808f3113`) — дефект
/// КЛАССА, не свойство духовки: у любой накладки, кончающейся в плоскости
/// несущей детали, тот же риск. `OvenCoplanarSurfaceTests` и
/// `DishwasherCoplanarSurfaceTests` проверяют это на ЧИСТОЙ математике
/// (`OvenBody`/`DishwasherBody`) и гоняются быстрым `dotnet test`; этот класс —
/// тот же сенсор (`CoplanarSurfaceDetector`), но по РЕАЛЬНОЙ построенной сцене
/// каждого типа элемента.
///
/// Источник коробок — не отдельная «Body»-математика (её у большинства типов
/// нет и заводить ради одного сенсора не требуется, `AGENTS.md` →
/// «Накладка, лежащая на детали...»), а `Renderer.bounds` каждого реального
/// меша элемента, собранные через `ElementRenderers.BodyOf` — тот же обход,
/// которым пользуется подсветка валидации, так что новый child-рендерер
/// элемента попадает под сенсор сам, без правки этого файла.
///
/// ОГОВОРКА, важная при разборе первого красного прогона: `Renderer.bounds` —
/// это AABB, а не настоящая поверхность. У ЦИЛИНДРА (труба, резьба винтовой
/// опоры) или скруглённого профиля боковая грань AABB — КАСАТЕЛЬНАЯ линия, а
/// не плоскость: возможен ложный сигнал там, где в реальности поверхность
/// уходит по кривой. Прежде чем заносить находку в исключения ниже, смотри
/// скриншот или меш, а не только числа сенсора.
///
/// Спавн всех типов настоящей фабрикой — единственное, что здесь дорого, и он
/// НЕ свой: показания снимает общий <see cref="ElementSurfaceSweep"/>, один
/// проход на весь прогон EditMode для четырёх наборов сторожей. Вопрос этого
/// класса остался прежним и задаётся тем же сенсором по тем же `Renderer.bounds`.
///
/// Требует настоящий Unity (реальные `GameObject`/`MeshRenderer`), поэтому в
/// `mutation-test.ps1 -TestsOnly` не входит.</summary>
public class CoplanarSurfaceCoverageTests
{
    /// <summary>Первый прогон (2026-09-10) нашёл 4 типа с находками. Мойка и окно —
    /// причинные дефекты, починены в геометрии (`SinkMesh`, `WindowElement`) той же
    /// кампанией: стенки чаши утоплены до дна, рама окна утоплена под откос — по
    /// образцу `AGENTS.md` → «Накладка, лежащая на детали...». Розетка и унитаз — ниже,
    /// с причиной на каждую; ни один из четырёх не оказался ложным срабатыванием от
    /// круглой детали (все грани — плоские торцы или плиты, не касательные AABB).</summary>
    private static readonly Dictionary<Type, string> KnownLegitimateCoplanarSurfaces =
        new Dictionary<Type, string>
    {
        { typeof(SocketElement),
            "SocketAndSwitchLayoutTests.SocketLayout_ThePinHoles_AreSunkIntoTheWellAndNeverTouchEachOther "
            + "запирает именно это: гнёзда под штыри и заземляющие лапки — маленькие декоративные "
            + "врезки, ЦЕЛИКОМ лежащие внутри колодца (не доходят до его края), а не деталь во весь "
            + "габарит несущей поверхности, как было у дверцы духовки. Раздвинуть их значило бы "
            + "нарушить тест, который явно и осознанно запирает эту геометрию" },
        { typeof(ToiletElement),
            "ToiletLayoutTests.FlushButton_IsRecessedIntoTheCisternTop_NotStickingOut запирает "
            + "кнопку смыва вровень с крышкой бачка по причине, названной в самом тесте: "
            + "габарит унитаза (HeightMM) фиксированный, торчащая кнопка вынесла бы его вверх. "
            + "Утопить бачок под кнопку тоже нельзя — его верх и есть заявленная высота прибора. "
            + "Кнопка — маленькая декоративная врезка внутри крышки, не деталь во весь габарит" },
    };

    [Test]
    public void EveryExcludedType_StillExistsAsAConcreteElementType()
    {
        var declared = EveryElementType.Declared();
        foreach (var type in KnownLegitimateCoplanarSurfaces.Keys)
        {
            Assert.IsTrue(typeof(KitchenElement).IsAssignableFrom(type),
                $"{type.Name} значится в списке исключений, но больше не наследует "
                + "KitchenElement — запись устарела");
            CollectionAssert.Contains(declared, type,
                $"{type.Name} значится в списке исключений, но рефлексия по сборке его не "
                + "находит — тип удалён или переименован, а запись осталась мёртвой");
        }
    }

    /// <summary>Противоположный вход: тип В списке исключений обязан ДЕЙСТВИТЕЛЬНО давать
    /// находку сенсора. Иначе список исключений — просто место, куда прячут находку,
    /// которую уже починили иначе, и «законно» от «забыли снять запись» не отличить —
    /// тот же принцип, что в SpecificationCoverageGuardTests.EveryExcludedType_ProducesNoSpecLineAtAll,
    /// только наоборот: там исключённый обязан молчать, здесь — обязан звучать.</summary>
    [Test]
    public void EveryExcludedType_StillTriggersTheSensor()
    {
        var stale = new List<string>();
        foreach (var type in KnownLegitimateCoplanarSurfaces.Keys)
            if (ElementSurfaceSweep.Of(type).CoplanarFights.Count == 0)
                stale.Add(type.Name);

        Assert.IsEmpty(stale,
            "эти типы значатся в KnownLegitimateCoplanarSurfaces («пара законна»), но сенсор "
            + "на них больше НИЧЕГО не находит — причина исключения устарела (геометрию уже "
            + "починили иначе или переставили), запись пора убрать: " + string.Join(", ", stale));
    }

    [Test]
    public void EveryDeclaredElementType_ClosedPose_HasNoTwoRenderedSurfacesFightingForTheSamePixel()
    {
        var violationsByType = new List<string>();

        foreach (var row in ElementSurfaceSweep.Rows)
        {
            if (row.CoplanarFights.Count == 0) continue;
            if (KnownLegitimateCoplanarSurfaces.ContainsKey(row.ElementType)) continue;

            violationsByType.Add(row.Name + ":\n  " + string.Join("\n  ", row.CoplanarFights));
        }

        Assert.IsEmpty(violationsByType,
            "Эти типы элементов строят пару граней, смотрящих в одну сторону из одной "
            + "плоскости и перекрывающихся площадью — на этом месте пользователь увидит "
            + "мерцание (z-fighting), как это было на дверце духовки (808f3113). Разведите "
            + "накладку и несущую деталь на осмысленную величину (толщину накладки), не "
            + "меняя общую толщину узла; если пара законна по конструкции — заведите "
            + "исключение в KnownLegitimateCoplanarSurfaces с причиной. Найдено:\n\n"
            + string.Join("\n\n", violationsByType));
    }

    private const string OnePlate =
        "одна плита (или коробка) одним мешем на корне элемента: спорить в нём нечему";

    private const string OneCombinedMesh =
        "прибор собран ОДНИМ процедурным мешем на корне — отдельных деталей-рендереров, "
        + "накладок и лицевых панелей у него нет";

    private const string OnePipeMesh =
        "труба и фитинг строятся одним процедурным мешем: тело и устья портов лежат в нём "
        + "же, отдельного рендерера ни у одного порта нет";

    /// <summary>Типы, у которых <c>ElementRenderers.BodyOf</c> находит РОВНО ОДИН
    /// рендерер: сенсору не с чем сравнивать, и <see
    /// cref="EveryDeclaredElementType_ClosedPose_HasNoTwoRenderedSurfacesFightingForTheSamePixel"/>
    /// проходит по ним молча. Это не исключение из правила (для того есть
    /// <see cref="KnownLegitimateCoplanarSurfaces"/> — там пара НАЙДЕНА и признана
    /// законной), а перепись слепых зон сенсора.
    ///
    /// Список выписан ИМЕНАМИ, а не заменён порогом «пусть таких будет меньше
    /// половины». Порог тут отвечал не на тот вопрос: половина от 37 объявленных
    /// типов — 18, первый же прогон дал 19, и «сенсор ослеп» от «типов с одним мешем
    /// на один больше половины» было не отличить. Порог можно подкрутить одной
    /// цифрой; список — нельзя. Тип, потерявший второй рендерер (тот самый дефект
    /// «один рендерер вместо всех» из `agents/TEST-DESIGN.md`), краснеет ПО ИМЕНИ, а
    /// тип, у которого второй рендерер появился, краснеет вторым сторожем ниже —
    /// проверка двусторонняя, как того требует
    /// `agents/TEST-DESIGN.md` → «Проверка "значение входит в закрытый список"
    /// обязана быть двусторонней».
    ///
    /// Новый тип в список НЕ попадает по умолчанию: он обязан иметь два рендерера
    /// либо быть внесён сюда руками с причиной.</summary>
    private static readonly Dictionary<Type, string> TypesBuiltAsOneSurface =
        new Dictionary<Type, string>
    {
        { typeof(KitchenElement), OnePlate },
        { typeof(PanelElement), OnePlate },
        { typeof(FacadeElement), OnePlate },
        { typeof(FloorElement), OnePlate },
        { typeof(PillarElement), OnePlate },
        { typeof(DrawerElement), OnePlate },
        { typeof(RadialShelfElement), OnePlate },
        { typeof(BathtubElement), OneCombinedMesh },
        { typeof(BathMixerElement), OneCombinedMesh },
        { typeof(ShowerColumnElement), OneCombinedMesh },
        { typeof(ScrewLegElement), OneCombinedMesh },
        { typeof(LightSourceElement),
            "светильник — источник света с одной лампой-мешем; тела из нескольких "
            + "поверхностей у него нет вовсе" },
        { typeof(PipeElement), OnePipeMesh },
        { typeof(PipeElbowElement), OnePipeMesh },
        { typeof(PipeCouplingElement), OnePipeMesh },
        { typeof(PipeTeeElement), OnePipeMesh },
        { typeof(PipeCapElement), OnePipeMesh },
        { typeof(PipeSupplyElement), OnePipeMesh },
        { typeof(PipeReturnElement), OnePipeMesh },
    };

    /// <summary>Сторож самого сенсора: паре граней вообще должно быть с чем спорить.
    /// Тип с одним рендерером сенсор пропускает молча — и если таким молча станет
    /// ещё один тип, проверка выше позеленеет, ничего про него не проверив.</summary>
    [Test]
    public void TheSensor_HasSomethingToCompare_OnMostTypes_OtherwiseItProvesNothing()
    {
        var blind = ElementSurfaceSweep.Rows
            .Where(r => r.BodyCount < 2 && !TypesBuiltAsOneSurface.ContainsKey(r.ElementType))
            .Select(r => r.Name + " (" + r.BodyCount + " рендерер)")
            .ToList();

        Assert.IsEmpty(blind,
            "у этих типов меньше двух рендереров, и в переписи слепых зон "
            + "(TypesBuiltAsOneSurface) их нет — сенсору на них нечего сравнивать, и он "
            + "зеленеет на любом коде. Это ровно тот дефект «один рендерер вместо всех», "
            + "от которого написан ElementRenderers.BodyOf: сначала проверь, не потерял ли "
            + "тип свои child-рендереры, и только если он ДЕЙСТВИТЕЛЬНО собран одним "
            + "мешем — внеси его в перепись с причиной: " + string.Join(", ", blind));
    }

    /// <summary>Вторая сторона той же переписи: тип, объявленный «одной
    /// поверхностью», обязан ею и остаться. Отрастил второй рендерер — запись
    /// устарела, и сенсору с этого момента есть что сравнивать, о чём он должен
    /// узнать сразу, а не через полгода.</summary>
    [Test]
    public void EveryTypeListedAsOneSurface_StillHasExactlyOneRenderer()
    {
        var declared = EveryElementType.Declared();
        var stale = new List<string>();
        foreach (var type in TypesBuiltAsOneSurface.Keys)
        {
            CollectionAssert.Contains(declared, type,
                type.Name + " значится в переписи одноповерхностных типов, но рефлексия по "
                + "сборке его не находит — тип удалён или переименован, запись мёртвая");
            int count = ElementSurfaceSweep.Of(type).BodyCount;
            if (count != 1) stale.Add(type.Name + ": " + count + " рендереров");
        }

        Assert.IsEmpty(stale,
            "эти типы значатся в TypesBuiltAsOneSurface («собран одним мешем, сравнивать "
            + "нечего»), но рендереров у них теперь не один. Два и больше — сенсор на них "
            + "работает, и запись пора убрать, иначе перепись слепых зон превращается в "
            + "место, куда прячут найденную пару. Ноль — у типа пропало тело целиком, и "
            + "это дефект, а не запись: " + string.Join(", ", stale));
    }
}
