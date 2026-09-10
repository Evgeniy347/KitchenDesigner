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
        { typeof(PillarElement),
            "процедурная точёная опора-колонна (не хозяйственная фурнитура винтовой опоры) — "
            + "ни листовая деталь, ни покупное изделие; вне этого дефекта" },
        { typeof(FloorElement),
            "напольное покрытие помещения, а не деталь и не штучное изделие — не входит в "
            + "ведомость по конструкции" },
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

    /// <summary>Противоположный вход: прямоугольный стол закрыл тот же дефект, что и
    /// радиусный — столешница считается через ISpecificationParts, а не списана в
    /// исключения. Ножки стола (LegSet, точёный брус) не покупное изделие и не
    /// листовая деталь — считать их пока не с чем, ровно как у радиусного стола,
    /// поэтому в исключения попадать не должны: у них просто нет отдельной строки.</summary>
    [Test]
    public void TableElement_IsCoveredThroughSpecificationParts_NotThroughTheExclusionList()
    {
        Assert.IsFalse(ExcludedFromSpecification.ContainsKey(typeof(TableElement)),
            "прямоугольный стол обязан считать столешницу сам через ISpecificationParts, "
            + "а не прятаться в списке исключений");

        EveryElementType.ClearScene();
        var element = EveryElementType.Spawn(typeof(TableElement), "CovTable");
        Assert.IsTrue(element is ISpecificationParts);
    }

    /// <summary>Противоположный вход: покупная сантехника той же природы, что мойка и
    /// ванна (уже считаются штуками через IQuantifies) — унитаз, подвесной унитаз и
    /// душевая стойка обязаны считать себя сами тем же механизмом, а не прятаться в
    /// списке исключений.</summary>
    [Test]
    public void PurchasedSanitaryTypes_AreCoveredThroughIQuantifies_NotThroughTheExclusionList()
    {
        var purchasedTypes = new[]
        {
            typeof(ToiletElement), typeof(WallHungToiletElement), typeof(ShowerColumnElement),
        };

        foreach (var type in purchasedTypes)
        {
            Assert.IsFalse(ExcludedFromSpecification.ContainsKey(type),
                $"{type.Name} — покупное изделие той же природы, что мойка и ванна, и "
                + "обязано считать себя само через IQuantifies, а не прятаться в списке "
                + "исключений");

            EveryElementType.ClearScene();
            var element = EveryElementType.Spawn(type, "Cov" + type.Name);
            Assert.Greater(element.GetComponents<IQuantifies>().Length, 0,
                $"{type.Name} обязан реализовать IQuantifies");
        }
    }

    /// <summary>Противоположный вход: цельная мебель (табурет, стул, диван, пуф,
    /// кровать) и проёмы стены (окно, дверь) — по ответу пользователя от
    /// 2026-09-10 такое же покупное изделие штуками, как мойка и унитаз, и
    /// обязаны считать себя сами через IQuantifies, а не прятаться в списке
    /// исключений.</summary>
    [Test]
    public void PurchasedFurnitureAndOpenings_AreCoveredThroughIQuantifies_NotThroughTheExclusionList()
    {
        var purchasedTypes = new[]
        {
            typeof(StoolElement), typeof(ChairElement), typeof(SofaElement),
            typeof(PouffeElement), typeof(BedElement), typeof(WindowElement), typeof(DoorElement),
        };

        foreach (var type in purchasedTypes)
        {
            Assert.IsFalse(ExcludedFromSpecification.ContainsKey(type),
                $"{type.Name} — цельная мебель или проём, считается покупным изделием "
                + "штуками и обязано считать себя само через IQuantifies, а не прятаться "
                + "в списке исключений");

            EveryElementType.ClearScene();
            var element = EveryElementType.Spawn(type, "Cov" + type.Name);
            Assert.Greater(element.GetComponents<IQuantifies>().Length, 0,
                $"{type.Name} обязан реализовать IQuantifies");
        }
    }
}
