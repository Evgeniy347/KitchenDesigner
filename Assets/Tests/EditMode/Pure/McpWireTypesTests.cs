using System;
using System.Linq;
using NUnit.Framework;
using KitchenDesigner.Core.MCP;
using KitchenDesigner.Core.MCP.Contract;

/// <summary>Список типов, которые прогревает <c>McpWarmup</c>. Прогрев нужен потому, что
/// разбивка дымового прогона назвала холод поимённо: у первого <c>create_elements</c>
/// этап <c>accept</c> стоил 57,61 мс против 0,05 мс у второго, а <c>exec->resp</c> —
/// 46,40 мс против 0,38 мс. Это Newtonsoft, впервые строящий контракты на
/// <c>ParamsCreateElements</c> с его <c>CreateItem[]</c> и на <c>ElementInfo</c>.
///
/// Список выводится РЕФЛЕКСИЕЙ, а не перечисляется руками: перечисленный руками он
/// протухнет на первом же новом инструменте, и прогрев тихо перестанет покрывать его
/// параметры. Поэтому проверяется само правило отбора.</summary>
public class McpWireTypesTests
{
    [Test]
    public void EveryParamsType_OfEveryTool_IsOnTheList()
    {
        var all = McpWireTypes.All(typeof(CreateItem));
        var missed = typeof(CreateItem).Assembly.GetTypes()
            .Where(t => t.Namespace == typeof(CreateItem).Namespace
                        && t.IsPublic && t.IsClass && !t.IsAbstract
                        && t.Name.StartsWith(McpWireTypes.ParamsPrefix, StringComparison.Ordinal))
            .Where(t => !all.Contains(t))
            .Select(t => t.Name)
            .ToList();

        Assert.IsEmpty(missed,
            "контракт параметров, не попавший в прогрев, платит свой холод на первом вызове: "
            + string.Join(", ", missed));
    }

    [Test]
    public void NestedTypes_AreFollowed_NotJustTheTopLevelOnes()
    {
        var all = McpWireTypes.All(typeof(CreateItem));

        Assert.Contains(typeof(CreateItem), all.ToList(),
            "CreateItem лежит массивом ВНУТРИ ParamsCreateElements — и именно он, "
            + "а не сам ParamsCreateElements, стоит 57 мс на первом разборе");
        Assert.Contains(typeof(EditOp), all.ToList(),
            "EditOp — такой же вложенный и самый широкий контракт правки");
    }

    [Test]
    public void TheListHasNoDuplicates_SoTheWarmupDoesNotRedoWork()
    {
        var all = McpWireTypes.All(typeof(CreateItem));

        Assert.AreEqual(all.Count, all.Distinct().Count(),
            "тип, попавший в список дважды, — это лишняя работа прогрева; "
            + "обход обязан помнить, где уже был, иначе он ещё и зациклится на самоссылке");
    }

    [Test]
    public void ForeignTypes_StayOut()
    {
        var all = McpWireTypes.All(typeof(CreateItem));

        Assert.IsFalse(all.Any(t => t == typeof(string) || t == typeof(int) || t.IsEnum),
            "примитивы, строки и перечисления контракта не имеют — греть их нечего");
        Assert.IsTrue(all.All(t => t.Namespace != null
                && t.Namespace.StartsWith("KitchenDesigner", StringComparison.Ordinal)),
            "чужие типы (UnityEngine.Vector3Int и прочие) в прогрев MCP не попадают");
    }

    [Test]
    public void TheList_CoversTheWholeSurface_NotAHandfulOfTypes()
    {
        Assert.Greater(McpWireTypes.All(typeof(CreateItem)).Count, 20,
            "у поверхности MCP больше сорока инструментов — пустой или крошечный список "
            + "значит, что правило отбора перестало что-либо находить, и прогрев стал пустышкой");
    }
}
