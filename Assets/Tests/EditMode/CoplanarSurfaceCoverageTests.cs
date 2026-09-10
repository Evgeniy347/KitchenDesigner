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

    /// <summary>Пока пусто: причинных дефектов кроме духовки и посудомойки
    /// (обе починены в этой же кампании) не найдено, а ложных срабатываний от
    /// круглых деталей заранее не предугадать без прогона. Запись сюда —
    /// ТОЛЬКО с причиной и только после того, как найденная пара проверена по
    /// мешу, а не по одним числам сенсора.</summary>
    private static readonly Dictionary<Type, string> KnownLegitimateCoplanarSurfaces =
        new Dictionary<Type, string>();

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

    [Test]
    public void EveryDeclaredElementType_ClosedPose_HasNoTwoRenderedSurfacesFightingForTheSamePixel()
    {
        var violationsByType = new List<string>();

        foreach (var type in EveryElementType.Declared())
        {
            EveryElementType.ClearScene();
            var element = EveryElementType.Spawn(type, "Cop" + type.Name);

            var renderers = ElementRenderers.BodyOf(element);
            if (renderers.Count < 2) continue;

            var boxes = renderers
                .Select(r => CoplanarSurfaceDetector.FromWorldBounds(r.bounds))
                .ToArray();
            var fights = CoplanarSurfaceDetector.Fights(
                boxes, i => ElementRenderers.PathOf(element, renderers[i]));

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
