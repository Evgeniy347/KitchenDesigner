using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using KitchenDesigner.Core;

/// <summary>ОДИН проход по всем объявленным типам элементов, посчитанный один раз на весь
/// прогон EditMode и прочитанный несколькими сторожами.
///
/// До 2026-09-10 таких проходов было три (<see cref="SpecSectionsTests"/> и два в
/// <see cref="SpecificationCoverageGuardTests"/>), каждый со своим спавном всех ~38 типов —
/// набор EditMode вырос со 185 до 218 секунд при бюджете 170. Проход дорогой ровно один раз,
/// поэтому он собирает про каждый тип ВСЁ, что спрашивают сторожа: объявленные маршруты в
/// ведомость, число выданных строк и набор разделов.
///
/// Строки берутся у настоящего <see cref="SpecificationManager.Build"/>, а не у наличия
/// интерфейса: <c>GetSpecItems</c> с одним <c>yield break</c> реализует
/// <see cref="IQuantifies"/> и при этом не даёт ни одной строки — ровно тот дефект, ради
/// которого сторожа и заводили (AGENTS.md → TEST-DESIGN → «A test that cannot fail is
/// worthless»).</summary>
public static class SpecificationSweep
{
    public readonly struct Row
    {
        public readonly Type type;
        public readonly SpecRoute declaredRoutes;
        public readonly int lineCount;
        public readonly string[] sections;

        public Row(Type type, SpecRoute declaredRoutes, int lineCount, string[] sections)
        {
            this.type = type;
            this.declaredRoutes = declaredRoutes;
            this.lineCount = lineCount;
            this.sections = sections;
        }
    }

    private static List<Row>? _rows;

    public static IReadOnlyList<Row> Rows => _rows ??= Run();

    public static Row Of(Type type) =>
        Rows.First(r => r.type == type);

    private static List<Row> Run()
    {
        var rows = new List<Row>();
        foreach (var type in EveryElementType.Declared())
        {
            EveryElementType.ClearScene();
            var element = EveryElementType.Spawn(type, "Spec" + type.Name);

            var routes = ElementSpecCoverage.Declared(
                element.GetComponents<IQuantifies>().Length > 0,
                element is ISpecificationParts,
                element.IsFlatBoardElement);

            var result = SpecificationManager.Build(new[] { element });

            rows.Add(new Row(type, routes, result.lines.Count,
                result.lines.Select(l => l.section).Distinct().OrderBy(s => s, StringComparer.Ordinal)
                    .ToArray()));
        }
        EveryElementType.ClearScene();
        return rows;
    }
}

/// <summary>Сторож против того самого дефекта: «элемент, который не умеет считать себя,
/// в ведомость не попадает» защищает от подсчёта фундамента как ЛДСП, но и
/// молча роняет из ведомости всё, что забыли объявить — так десять радиусных полок
/// и весь список покупных изделий (мойка, варочная, духовка, посудомойка, три
/// винтовые опоры, розетка, выключатель, девять светильников, смеситель, ванна)
/// исчезли из спецификации замороженной кухни без единого предупреждения.
///
/// Требование здесь — не «объявлен интерфейс», а «<see cref="SpecificationManager.Build"/>
/// выдал хотя бы одну строку». Разница не теоретическая: прежний сторож считал покрытым
/// любой тип с <c>GetComponents&lt;IQuantifies&gt;().Length &gt; 0</c>, поэтому
/// <c>GetSpecItems</c> из одного <c>yield break</c> — или с ранним <c>if</c>, гасящим выдачу
/// на какой-то геометрии, — удовлетворял ВСЕХ трёх сторожей, а элемент исчезал из ведомости.
///
/// Второе требование — ИСКЛЮЧИТЕЛЬНОСТЬ маршрута. <see cref="SpecificationManager.Build"/>
/// строго упорядочен: <see cref="IQuantifies"/> выигрывает и делает <c>continue</c>, дальше
/// <see cref="ISpecificationParts"/>, дальше <c>IsFlatBoardElement</c>. Тип, у которого истинны
/// два пути, проходил старого сторожа-дизъюнкцию, пока его строки по второму пути молча
/// выбрасывались; сработало бы это в тот день, когда фасаду добавят <see cref="IQuantifies"/>
/// ради строки «петли, шт» и тихо потеряют его площадь ЛДСП.
///
/// Список типов берётся из <see cref="EveryElementType"/> (единственное
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

    /// <summary>Тип объявил два маршрута из трёх, и это ИЗВЕСТНО и осознанно: указан ровно
    /// тот набор, который разрешён, поэтому третий маршрут (или другая пара) всё равно
    /// покраснеет. Запись обязана называть, какой маршрут мёртв и почему это не потеря.</summary>
    private static readonly Dictionary<Type, (SpecRoute routes, string reason)> AllowedDualRoute =
        new Dictionary<Type, (SpecRoute, string)>
    {
        { typeof(AssembledFacadeElement),
            (SpecRoute.SpecificationParts | SpecRoute.FlatBoard,
            "наследует IsFlatBoardElement=true от FacadeElement, но ISpecificationParts "
            + "выигрывает раньше и выдаёт детали рамки со стеклом; строка по площади ЦЕЛЬНОГО "
            + "фасада была бы двойным счётом тех же деталей, поэтому мёртвый листовой маршрут "
            + "здесь — намерение, а не потеря") },
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
    public void EveryDeclaredElementType_ProducesAtLeastOneSpecLine_OrIsExplicitlyExcluded()
    {
        var violations = new List<string>();

        foreach (var row in SpecificationSweep.Rows)
        {
            if (ExcludedFromSpecification.ContainsKey(row.type)) continue;
            if (ElementSpecCoverage.IsCovered(row.lineCount)) continue;

            violations.Add(row.declaredRoutes == SpecRoute.None
                ? $"{row.type.Name} (не объявил ни одного маршрута)"
                : $"{row.type.Name} (маршрут {ElementSpecCoverage.Taken(row.declaredRoutes)} "
                  + "объявлен, но выдал 0 строк)");
        }

        Assert.IsEmpty(violations,
            "SpecificationManager.Build на одном таком элементе не дал НИ ОДНОЙ строки — "
            + "элемент выпадет из ведомости молча, как выпали десять радиусных полок и весь "
            + "список покупных изделий. Объявленного интерфейса мало: GetSpecItems с одним "
            + "yield break реализует IQuantifies и не выдаёт ничего. Либо тип обязан выдать "
            + "строку, либо стоять в ExcludedFromSpecification С ПРИЧИНОЙ: "
            + string.Join(", ", violations));
    }

    /// <summary>Противоположный вход: тип В списке исключений обязан выдавать РОВНО НОЛЬ
    /// строк. Иначе список исключений — просто место, куда убирают из-под сторожа, и
    /// «покрыт» от «списан» опять не отличить.</summary>
    [Test]
    public void EveryExcludedType_ProducesNoSpecLineAtAll()
    {
        var violations = new List<string>();

        foreach (var row in SpecificationSweep.Rows)
            if (ExcludedFromSpecification.ContainsKey(row.type) && row.lineCount > 0)
                violations.Add($"{row.type.Name}: {row.lineCount} стр.");

        Assert.IsEmpty(violations,
            "эти типы значатся в списке исключений («в ведомость не входят»), но Build выдал "
            + "по ним строки — причина в списке устарела, запись пора убрать: "
            + string.Join(", ", violations));
    }

    /// <summary>Маршрут в ведомость обязан быть ОДИН. `Build` берёт первый по своему порядку
    /// и делает `continue`, поэтому второй объявленный маршрут — мёртвый код, чьи строки
    /// молча не выдаются.</summary>
    [Test]
    public void EveryDeclaredElementType_TakesExactlyOneRouteIntoTheSpecification()
    {
        var violations = new List<string>();

        foreach (var row in SpecificationSweep.Rows)
        {
            var dead = ElementSpecCoverage.Dead(row.declaredRoutes);
            if (dead == SpecRoute.None) continue;
            if (AllowedDualRoute.TryGetValue(row.type, out var allowed)
                && allowed.routes == row.declaredRoutes) continue;

            violations.Add($"{row.type.Name}: объявлено {row.declaredRoutes}, "
                + $"Build берёт {ElementSpecCoverage.Taken(row.declaredRoutes)}, "
                + $"мёртв {dead}");
        }

        Assert.IsEmpty(violations,
            "у этих типов истинны два маршрута из трёх, а Build берёт ПЕРВЫЙ и делает "
            + "continue — остальные мертвы, и их строки в ведомость не попадут никогда. "
            + "Либо снять лишний маршрут, либо завести запись в AllowedDualRoute с причиной, "
            + "называющей мёртвый маршрут: "
            + string.Join(", ", violations));
    }

    /// <summary>Сторожа проверяем сторожем: запись в AllowedDualRoute обязана описывать
    /// НАСТОЯЩУЮ пару маршрутов живого типа, а не остаться после того, как маршрут убрали.</summary>
    [Test]
    public void EveryAllowedDualRoute_StillDescribesTheTypesRealRoutes()
    {
        foreach (var pair in AllowedDualRoute)
        {
            var row = SpecificationSweep.Of(pair.Key);
            Assert.AreEqual(pair.Value.routes, row.declaredRoutes,
                $"{pair.Key.Name} значится в AllowedDualRoute как {pair.Value.routes}, а на "
                + $"деле объявляет {row.declaredRoutes} — запись описывает не то, что есть, и "
                + "перестала быть разрешением именно этой пары");
            Assert.AreNotEqual(SpecRoute.None, ElementSpecCoverage.Dead(pair.Value.routes),
                $"{pair.Key.Name} в AllowedDualRoute, но мёртвого маршрута у него нет — "
                + "запись лишняя");
            Assert.IsNotEmpty(pair.Value.reason);
        }
    }

    /// <summary>Противоположный вход: столы (радиусный и прямоугольный) закрыли тот же дефект
    /// через ISpecificationParts — считают столешницу САМИ, а не прячутся в списке исключений.
    /// Ножки стола (LegSet, точёный брус) не покупное изделие и не листовая деталь — считать
    /// их пока не с чем, у них просто нет отдельной строки.</summary>
    [Test]
    public void TableTypes_AreCoveredThroughSpecificationParts_NotThroughTheExclusionList()
        => AssertCoveredThrough(SpecRoute.SpecificationParts,
            "обязан считать столешницу сам через ISpecificationParts",
            typeof(TableElement), typeof(RadiusTableElement));

    /// <summary>Противоположный вход: покупная сантехника той же природы, что мойка и
    /// ванна — унитаз, подвесной унитаз и душевая стойка обязаны считать себя сами через
    /// IQuantifies, а не прятаться в списке исключений.</summary>
    [Test]
    public void PurchasedSanitaryTypes_AreCoveredThroughIQuantifies_NotThroughTheExclusionList()
        => AssertCoveredThrough(SpecRoute.Quantifies,
            "покупное изделие той же природы, что мойка и ванна",
            typeof(ToiletElement), typeof(WallHungToiletElement), typeof(ShowerColumnElement));

    /// <summary>Противоположный вход: цельная мебель (табурет, стул, диван, пуф,
    /// кровать) и проёмы стены (окно, дверь) — по ответу пользователя от
    /// 2026-09-10 такое же покупное изделие штуками, как мойка и унитаз.</summary>
    [Test]
    public void PurchasedFurnitureAndOpenings_AreCoveredThroughIQuantifies_NotThroughTheExclusionList()
        => AssertCoveredThrough(SpecRoute.Quantifies,
            "цельная мебель или проём, считается покупным изделием штуками",
            typeof(StoolElement), typeof(ChairElement), typeof(SofaElement),
            typeof(PouffeElement), typeof(BedElement), typeof(WindowElement), typeof(DoorElement));

    private static void AssertCoveredThrough(SpecRoute expected, string what, params Type[] types)
    {
        foreach (var type in types)
        {
            Assert.IsFalse(ExcludedFromSpecification.ContainsKey(type),
                $"{type.Name} — {what} и обязан считать себя сам, а не прятаться в списке "
                + "исключений");

            var row = SpecificationSweep.Of(type);
            Assert.AreEqual(expected, ElementSpecCoverage.Taken(row.declaredRoutes),
                $"{type.Name} обязан попадать в ведомость маршрутом {expected}");
            Assert.Greater(row.lineCount, 0,
                $"{type.Name} объявил маршрут {expected}, но Build не выдал по нему ни одной "
                + "строки — объявленного интерфейса мало");
        }
    }
}
