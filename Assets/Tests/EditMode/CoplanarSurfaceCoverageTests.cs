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
/// каждого типа элемента, как `SpecificationCoverageGuardTests` гоняет
/// `IQuantifies`/`ISpecificationParts` по каждому типу из <see cref="EveryElementType"/>.
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
/// Требует настоящий Unity (реальные `GameObject`/`MeshRenderer`), поэтому в
/// `mutation-test.ps1 -TestsOnly` не входит — гоняется в общем прогоне
/// PlayMode/EditMode очередью менеджера, как и `SpecificationCoverageGuardTests`.</summary>
public class CoplanarSurfaceCoverageTests
{
    [TearDown]
    public void TearDown() => EveryElementType.ClearScene();

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

    private static List<string> FightsFor(Type type)
    {
        EveryElementType.ClearScene();
        var element = EveryElementType.Spawn(type, "Cop" + type.Name);

        var renderers = ElementRenderers.BodyOf(element);
        if (renderers.Count < 2) return new List<string>();

        var boxes = renderers
            .Select(r => CoplanarSurfaceDetector.FromWorldBounds(r.bounds))
            .ToArray();
        return CoplanarSurfaceDetector.Fights(
            boxes, i => ElementRenderers.PathOf(element, renderers[i]));
    }

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
            if (FightsFor(type).Count == 0)
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

        foreach (var type in EveryElementType.Declared())
        {
            var fights = FightsFor(type);
            if (fights.Count == 0) continue;
            if (KnownLegitimateCoplanarSurfaces.ContainsKey(type)) continue;

            violationsByType.Add(type.Name + ":\n  " + string.Join("\n  ", fights));
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
}
