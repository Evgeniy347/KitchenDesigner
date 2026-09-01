using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using KitchenDesigner.Core.MCP.Contract;

public class McpContractSurfaceTests
{
    private const BindingFlags PublicInstance = BindingFlags.Public | BindingFlags.Instance;

    private static readonly Dictionary<string, string> ToolsWhoseNameIsNotAnElement =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["ParamsCreateFloorV2"] = "create_floor именует СОЗДАВАЕМЫЙ пол, а не адресует существующий",
            ["ParamsAddOpening"] = "add_opening именует создаваемый проём",
            ["ParamsCreateModule"] = "create_module именует создаваемый модуль",
            ["ParamsSetSetting"] = "set_setting адресует настройку сцены, а не элемент",
        };

    private static readonly Dictionary<string, string> NumbersWithoutAUnit =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["ParamsLogCount.count"] = "штуки записей, а не физическая величина",
            ["CloneOp.count"] = "штуки копий, а не физическая величина",
            ["TransformOp.x"] = "общая операция set_position/set_rotation/set_scale: метры или градусы решает описание самого инструмента",
            ["TransformOp.y"] = "то же самое",
            ["TransformOp.z"] = "то же самое",
        };

    private static readonly string[] DestructiveTools =
        { "delete_elements", "dissolve_module", "delete_object" };

    private static readonly string[] UnitWords =
        { "MM", "METER", "DEGREE", "mm", "meter", "degree", "%", "0..1" };

    private static List<Type> ReachableParamsTypes()
    {
        var found = new List<Type>();
        foreach (var tool in McpToolRegistry.Tools)
            if (tool.ParamsType != null && !found.Contains(tool.ParamsType))
                found.Add(tool.ParamsType);

        for (int i = 0; i < found.Count; i++)
            foreach (var field in found[i].GetFields(PublicInstance))
            {
                if (!field.FieldType.IsArray) continue;
                var element = field.FieldType.GetElementType();
                if (element == null || element.IsPrimitive || element == typeof(string)) continue;
                if (!found.Contains(element)) found.Add(element);
            }

        return found;
    }

    private static IEnumerable<Type> TopLevelParamsTypes() =>
        McpToolRegistry.Tools.Where(t => t.ParamsType != null).Select(t => t.ParamsType!).Distinct();

    [Test]
    public void TheScan_ReachesTheNestedOpClasses_NotOnlyTheTopLevelParams()
    {
        var reachable = ReachableParamsTypes();
        CollectionAssert.Contains(reachable.Select(t => t.Name).ToList(), "EditOp",
            "обход не спустился в элементы массивов: по одному верхнему уровню сторож зеленеет, "
            + "ничего не проверив — а именно во вложенных op/item живут поля агента");
        Assert.Greater(reachable.Count, TopLevelParamsTypes().Count(),
            "вложенных типов не нашлось вовсе");
    }

    [Test]
    public void EveryPublicParamsField_IsEitherAnAgentParameter_OrExplicitlyIgnored()
    {
        var silent = new List<string>();

        foreach (var type in ReachableParamsTypes())
            foreach (var field in type.GetFields(PublicInstance))
            {
                if (field.GetCustomAttribute<McpParamAttribute>() != null) continue;
                if (field.GetCustomAttribute<McpIgnoreAttribute>() != null) continue;
                silent.Add(type.Name + "." + field.Name);
            }

        CollectionAssert.IsEmpty(silent,
            "поле без атрибута молча выпадает из схемы: генератор и сервер пропускают его "
            + "(if (p == null) continue), агент о нём не узнаёт, а Unity-обработчик его читает — "
            + "именно так параметр «есть в C#, но его нет у клиента». Помечай [McpParam] или "
            + "[McpIgnore] явно:\n" + string.Join("\n", silent));
    }

    [Test]
    public void NestedOpFields_CarryNoRename_BecauseTheSchemaCannotApplyOne()
    {
        var top = TopLevelParamsTypes().ToList();
        var renamed = new List<string>();

        foreach (var type in ReachableParamsTypes())
        {
            if (top.Contains(type)) continue;
            foreach (var field in type.GetFields(PublicInstance))
            {
                var param = field.GetCustomAttribute<McpParamAttribute>();
                if (param != null && !string.IsNullOrEmpty(param.AgentName))
                    renamed.Add(type.Name + "." + field.Name + " -> " + param.AgentName);
            }
        }

        CollectionAssert.IsEmpty(renamed,
            "McpJsonSchema бросает «Nested param rename is not supported», а Zod-генератор "
            + "переименование во вложенном классе просто не эмитит: имя поля вложенного "
            + "op/item ДОЛЖНО сразу быть тем, что видит агент:\n" + string.Join("\n", renamed));
    }

    [Test]
    public void EveryAgentFacingFieldName_IsAlreadyLowerCase()
    {
        var camel = ReachableParamsTypes()
            .SelectMany(t => t.GetFields(PublicInstance)
                .Where(f => f.GetCustomAttribute<McpParamAttribute>() != null)
                .Where(f => f.Name != f.Name.ToLowerInvariant())
                .Select(f => t.Name + "." + f.Name))
            .ToList();

        CollectionAssert.IsEmpty(camel,
            "имя поля попадает к агенту как есть; camelCase рядом со snake_case — "
            + "разнобой в одном контракте:\n" + string.Join("\n", camel));
    }

    [Test]
    public void NoTool_AddressesAnExistingElement_ByASingleName()
    {
        var offenders = new List<string>();

        foreach (var type in TopLevelParamsTypes())
        {
            if (ToolsWhoseNameIsNotAnElement.ContainsKey(type.Name)) continue;
            foreach (var field in type.GetFields(PublicInstance))
            {
                if (field.GetCustomAttribute<McpParamAttribute>() == null) continue;
                if (field.FieldType != typeof(string)) continue;
                if (field.Name == "name" || field.Name == "element_name")
                    offenders.Add(type.Name + "." + field.Name);
            }
        }

        CollectionAssert.IsEmpty(offenders,
            "соглашение поверхности: КАЖДЫЙ адресный инструмент — батч (names[] / ops[] / "
            + "items[]), одноэлементных нет. Одиночное имя возвращает клиента к вызову на "
            + "элемент и ломает атомарность батча:\n" + string.Join("\n", offenders));
    }

    [Test]
    public void EveryToolInTheAllowList_StillNamesALivingParamsType()
    {
        var live = TopLevelParamsTypes().Select(t => t.Name).ToList();
        foreach (var pair in ToolsWhoseNameIsNotAnElement)
            CollectionAssert.Contains(live, pair.Key,
                "исключение «" + pair.Key + "» (" + pair.Value + ") пережило свой тип: "
                + "запись начнёт молча освобождать следующий класс с этим именем");

        var reachable = ReachableParamsTypes()
            .SelectMany(t => t.GetFields(PublicInstance).Select(f => t.Name + "." + f.Name))
            .ToList();
        foreach (var pair in NumbersWithoutAUnit)
            CollectionAssert.Contains(reachable, pair.Key,
                "исключение «" + pair.Key + "» (" + pair.Value + ") пережило своё поле");
    }

    [Test]
    public void EveryNumericParameter_StatesItsUnit_InTheDescription()
    {
        var unitless = new List<string>();

        foreach (var type in ReachableParamsTypes())
            foreach (var field in type.GetFields(PublicInstance))
            {
                var param = field.GetCustomAttribute<McpParamAttribute>();
                if (param == null) continue;

                var fieldType = Nullable.GetUnderlyingType(field.FieldType) ?? field.FieldType;
                if (fieldType != typeof(int) && fieldType != typeof(float) && fieldType != typeof(double))
                    continue;

                var key = type.Name + "." + field.Name;
                if (NumbersWithoutAUnit.ContainsKey(key)) continue;
                if (UnitWords.Any(u => param.Description.Contains(u, StringComparison.Ordinal))) continue;
                unitless.Add(key + " :: " + param.Description);
            }

        CollectionAssert.IsEmpty(unitless,
            "миллиметры против метров — ошибка №1 клиента (guide называет её так же). "
            + "Число без единицы в описании агент трактует наугад:\n" + string.Join("\n", unitless));
    }

    [Test]
    public void OnlyDeletionTools_AreMarkedDestructive()
    {
        var marked = McpToolRegistry.Tools
            .Where(t => t.Kind == McpToolKind.Destructive)
            .Select(t => t.Name).OrderBy(n => n, StringComparer.Ordinal).ToList();

        CollectionAssert.AreEqual(DestructiveTools.OrderBy(n => n, StringComparer.Ordinal).ToList(), marked,
            "Kind — единственный источник аннотаций readOnlyHint/destructiveHint у клиента. "
            + "Инструмент, помеченный Destructive напрасно, заставляет агента спрашивать "
            + "разрешение на безобидную правку; забытая пометка — наоборот, удаляет молча");
    }

    [Test]
    public void OnlyTheBigSceneRead_CarriesTheEtagCache()
    {
        var cached = McpToolRegistry.Tools.Where(t => t.Cached).Select(t => t.Name).ToList();

        CollectionAssert.AreEqual(new[] { "get_all_elements" }, cached,
            "Cached включает ETag-кэш: клиент присылает If-None-Match и получает "
            + "not_modified вместо данных. На мутирующем инструменте это отдало бы кэш "
            + "вместо работы, а снятое с большого чтения — вернуло бы всю сцену на каждый вызов");
    }

    [Test]
    public void EveryGetterTool_IsMarkedRead_SoTheClientKnowsItIsSafe()
    {
        var mislabelled = McpToolRegistry.Tools
            .Where(t => t.Name.StartsWith("get_", StringComparison.Ordinal))
            .Where(t => t.Kind != McpToolKind.Read)
            .Select(t => t.Name + " = " + t.Kind).ToList();

        Assert.IsNotEmpty(McpToolRegistry.Tools.Where(t => t.Name.StartsWith("get_", StringComparison.Ordinal)).ToList(),
            "инструментов get_* не нашлось — сторож ослеп, а не позеленел");
        CollectionAssert.IsEmpty(mislabelled,
            "get_* обязан быть Read: иначе клиент получает readOnlyHint:false и начинает "
            + "спрашивать подтверждение на чтение сцены:\n" + string.Join("\n", mislabelled));
    }
}
