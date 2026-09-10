using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using KitchenDesigner.Core;

/// <summary>Сторож против того самого дефекта: «элемент, который не умеет считать себя,
/// в ведомость не попадает» защищает от подсчёта фундамента как ЛДСП, но и
/// молча роняет из ведомости всё, что забыли объявить — так десять радиусных полок
/// и весь список покупных изделий (мойка, варочная, духовка, посудомойка, три
/// винтовые опоры, розетка, выключатель, девять светильников, смеситель, ванна)
/// исчезли из спецификации замороженной кухни без единого предупреждения.
///
/// Каждый КОНКРЕТНЫЙ тип <see cref="KitchenElement"/> в сборке обязан либо уметь
/// посчитать себя (<see cref="IQuantifies"/> / <see cref="ISpecificationParts"/> /
/// <c>IsFlatBoardElement == true</c>), либо стоять в списке исключений ниже — С
/// ПРИЧИНОЙ. Список типов берётся из <see cref="EveryElementType"/> (единственное
/// место, где типы перечислены руками, и оно уже сверяется с объявлениями классов
/// в сборке своим собственным тестом), поэтому новый тип элемента сначала уронит
/// именно этот тест, а не тихо проскочит мимо ведомости.</summary>
public class SpecificationCoverageGuardTests
{
    [TearDown]
    public void TearDown() => EveryElementType.ClearScene();

    /// <summary>Тип не считает себя сам — и это ОСОЗНАННЫЙ выбор с причиной, а не
    /// пропуск. Ничего из этого списка не годится в производственный код: он
    /// существует только затем, чтобы новый безымянный пропуск было не с чем
    /// перепутать.</summary>
    private static readonly Dictionary<Type, string> ExcludedFromSpecification =
        new Dictionary<Type, string>
    {
        { typeof(TableElement),
            "прямоугольный стол — столешница не разложена на листовые детали; тот же "
            + "по форме дефект, что был у радиусного стола, чинить отдельным проходом" },
        { typeof(StoolElement), "цельная точёная мебель без разбивки на детали — вне этого дефекта" },
        { typeof(ChairElement), "цельная мебель со спинкой без разбивки на детали — вне этого дефекта" },
        { typeof(SofaElement), "мягкая мебель, разбивки на детали нет — вне этого дефекта" },
        { typeof(PouffeElement), "мягкая мебель, разбивки на детали нет — вне этого дефекта" },
        { typeof(BedElement), "цельная мебель без разбивки на детали — вне этого дефекта" },
        { typeof(PillarElement),
            "процедурная точёная опора-колонна (не хозяйственная фурнитура винтовой опоры) — "
            + "ни листовая деталь, ни покупное изделие; вне этого дефекта" },
        { typeof(FloorElement),
            "напольное покрытие помещения, а не деталь и не штучное изделие — не входит в "
            + "ведомость по конструкции" },
        { typeof(WindowElement),
            "проём стены со своей рамой и створкой, не разложен на детали — вне этого дефекта" },
        { typeof(DoorElement),
            "проём стены со своим полотном, не разложен на детали — вне этого дефекта" },
        { typeof(ToiletElement),
            "покупная сантехника той же природы, что мойка и ванна, но не значится в "
            + "замороженной кухне — тот же дефект, чинить отдельным проходом" },
        { typeof(WallHungToiletElement),
            "покупная сантехника той же природы — см. причину у ToiletElement" },
        { typeof(ShowerColumnElement),
            "покупная сантехника той же природы — см. причину у ToiletElement" },
    };

    [Test]
    public void EveryExcludedType_StillExistsAsAConcreteElementType()
    {
        var declared = EveryElementType.Declared();
        foreach (var type in ExcludedFromSpecification.Keys)
        {
            Assert.IsTrue(typeof(KitchenElement).IsAssignableFrom(type),
                $"{type.Name} значится в списке исключений спецификации, но больше не "
                + "наследует KitchenElement — запись устарела и её пора убрать");
            CollectionAssert.Contains(declared, type,
                $"{type.Name} значится в списке исключений, но рефлексия по сборке его не "
                + "находит — тип удалён или переименован, а запись осталась мёртвой");
        }
    }

    [Test]
    public void EveryDeclaredElementType_EitherCountsItselfOrIsExplicitlyExcluded()
    {
        var violations = new List<string>();

        foreach (var type in EveryElementType.Declared())
        {
            EveryElementType.ClearScene();
            var element = EveryElementType.Spawn(type, "Cov" + type.Name);

            bool selfQuantifies = element.GetComponents<IQuantifies>().Length > 0;
            bool isSpecParts = element is ISpecificationParts;
            bool isFlatBoard = element.IsFlatBoardElement;
            bool isExcluded = ExcludedFromSpecification.ContainsKey(type);

            if (!ElementSpecCoverage.IsCovered(selfQuantifies, isSpecParts, isFlatBoard, isExcluded))
                violations.Add(type.Name);
        }

        Assert.IsEmpty(violations,
            "эти типы не умеют считать себя в ведомости (нет IQuantifies / "
            + "ISpecificationParts / IsFlatBoardElement=true) и не значатся в списке "
            + "исключений выше — они выпадут из ведомости МОЛЧА, как выпали десять "
            + "радиусных полок и весь список покупных изделий: "
            + string.Join(", ", violations));
    }

    /// <summary>Противоположный вход: тип, который явно объявил ISpecificationParts
    /// (RadiusTableElement), обязан пройти проверку САМ, без строки в списке
    /// исключений — иначе тест выше не отличает «покрыт» от «списан».</summary>
    [Test]
    public void RadiusTableElement_IsCoveredThroughSpecificationParts_NotThroughTheExclusionList()
    {
        Assert.IsFalse(ExcludedFromSpecification.ContainsKey(typeof(RadiusTableElement)),
            "радиусный стол обязан считать себя сам через ISpecificationParts, а не "
            + "прятаться в списке исключений");

        EveryElementType.ClearScene();
        var element = EveryElementType.Spawn(typeof(RadiusTableElement), "CovRadiusTable");
        Assert.IsTrue(element is ISpecificationParts);
    }
}
