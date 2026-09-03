using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using KitchenDesigner.Core.MCP.Contract;
using KitchenDesigner.Tests.Geometry;

/// <summary>JSON Schema для tools/list раньше жила в Zod внутри Node-моста
/// (mcp-server/src/tools.generated.ts, генератор tools/McpContractGen). Приложение
/// отдаёт tools/list само, значит схему строит C#: McpJsonSchema читает те же поля
/// Contract/Params* и те же атрибуты [McpParam]/[McpIgnore].
///
/// Тесты живут на быстром пути (0,3 с вместо холодного Unity) ровно потому, что
/// Contract/ не знает ни про UnityEngine, ни про Newtonsoft — это же правило
/// сторожит McpContractSourceTests, и Pure.csproj теперь линкует те же файлы.</summary>
public class McpJsonSchemaTests
{
    [Serializable]
    private class RenamedParams
    {
        [McpParam("Имя на проводе отличается от имени поля C#.", Required = true, AgentName = "outer_name")]
        public string wireName = string.Empty;
    }

    [Serializable]
    private class DoublyMarkedParams
    {
        [McpIgnore]
        [McpParam("Поле, у которого есть и описание, и запрет показывать его агенту.")]
        public string legacyAlias = string.Empty;

        [McpParam("Соседнее поле, которое обязано остаться.")]
        public string keep = string.Empty;
    }

    [Serializable]
    private class UnsupportedParams
    {
        [McpParam("Тип, которого нет в таблице соответствий.")]
        public Guid ticket;
    }

    private static McpToolDef Tool(Type paramsType) =>
        new McpToolDef("probe", "Probe", "fixture", McpToolKind.Read, paramsType);

    private static McpToolDef Registry(string name) =>
        McpToolRegistry.Tools.First(t => t.Name == name);

    private static Dictionary<string, object> Property(McpToolDef tool, string name)
    {
        var properties = (Dictionary<string, object>)McpJsonSchema.ForTool(tool)["properties"];
        Assert.IsTrue(properties.ContainsKey(name),
            "поля " + name + " нет в схеме " + tool.Name + "; есть: " + string.Join(", ", properties.Keys));
        return (Dictionary<string, object>)properties[name];
    }

    [Test]
    public void EveryTool_YieldsAnObjectSchema_EvenWithoutParams()
    {
        foreach (var tool in McpToolRegistry.Tools)
        {
            var schema = McpJsonSchema.ForTool(tool);
            Assert.AreEqual("object", schema["type"],
                "MCP-клиент отказывается регистрировать инструмент, у которого inputSchema.type "
                + "не \"object\": " + tool.Name);
            Assert.IsInstanceOf<Dictionary<string, object>>(schema["properties"],
                "properties обязаны быть объектом даже у инструмента без параметров (" + tool.Name
                + "), иначе клиент видит схему как сломанную");
        }
    }

    [Test]
    public void ToolWithoutParams_HasEmptyProperties_AndNoRequired()
    {
        var schema = McpJsonSchema.ForTool(Registry("ping"));
        var properties = (Dictionary<string, object>)schema["properties"];

        CollectionAssert.IsEmpty(properties, "у ping нет параметров");
        Assert.IsFalse(schema.ContainsKey("required"),
            "пустой массив required — не то же самое, что его отсутствие: клиенты показывают "
            + "такой инструмент как требующий аргументов");
    }

    [Test]
    public void RequiredFields_LandInRequired_AndOptionalOnesDoNot()
    {
        var schema = McpJsonSchema.ForTool(Registry("edit_elements"));
        var required = ((List<object>)schema["required"]).Cast<string>().ToList();

        CollectionAssert.Contains(required, "ops",
            "ops помечен Required = true — без него edit_elements бессмыслен");
        CollectionAssert.DoesNotContain(required, "dry_run",
            "dry_run необязателен; попав в required, он заставил бы агента слать его всегда");
    }

    [Test]
    public void EnumMinMaxAndDescription_SurviveTheMapping()
    {
        var mode = Property(Registry("edit_elements"), "ops");
        var opItems = (Dictionary<string, object>)mode["items"];
        var opProperties = (Dictionary<string, object>)opItems["properties"];
        var modeSchema = (Dictionary<string, object>)opProperties["mode"];

        Assert.AreEqual("string", modeSchema["type"], "z.enum отображается в строку с перечислением");
        CollectionAssert.Contains((List<object>)modeSchema["enum"], "front_left",
            "без списка значений агент подбирает режим открывания наугад");
        Assert.IsTrue(((string)modeSchema["description"]).Length > 0,
            "описание поля — единственное, что объясняет агенту смысл значения");

        var width = Property(Registry("resize_floor"), "width");
        Assert.AreEqual("integer", width["type"], "int отображается в integer, а не в number");
        Assert.AreEqual(1L, width["minimum"], "Min = 1 у ширины пола обязан доехать до схемы");
        Assert.IsFalse(width.ContainsKey("maximum"), "верхней границы у ширины пола не задано");
    }

    [Test]
    public void StringArray_CarriesItemMinLengthAndMinItems()
    {
        var names = Property(Registry("get_element_gaps"), "names");

        Assert.AreEqual("array", names["type"]);
        var items = (Dictionary<string, object>)names["items"];
        Assert.AreEqual("string", items["type"]);
        Assert.AreEqual(1L, items["minLength"],
            "пустая строка в names — это запрос про элемент без имени; Zod резал её z.string().min(1)");
        Assert.AreEqual(1L, names["minItems"],
            "Min = 1 на массиве означает minItems, а не minLength");
    }

    [Test]
    public void NestedOpArrays_BecomeItemsWithTheirOwnRequired()
    {
        var ops = Property(Registry("edit_elements"), "ops");
        var items = (Dictionary<string, object>)ops["items"];
        var required = ((List<object>)items["required"]).Cast<string>().ToList();

        Assert.AreEqual("object", items["type"], "элемент массива ops — объект EditOp");
        CollectionAssert.Contains(required, "name",
            "EditOp.name помечен Required: обязательность вложенного поля должна оказаться "
            + "внутри items, а не в required самого инструмента");
        CollectionAssert.DoesNotContain(
            ((List<object>)McpJsonSchema.ForTool(Registry("edit_elements"))["required"]).Cast<string>(),
            "name",
            "поле вложенного EditOp не имеет права всплыть в required верхнего уровня");
    }

    [Test]
    public void AgentName_RenamesTheSchemaKey_AndFillsTheRenameTable()
    {
        var tool = Tool(typeof(RenamedParams));
        var properties = (Dictionary<string, object>)McpJsonSchema.ForTool(tool)["properties"];
        var rename = McpJsonSchema.RenameTable(tool);

        CollectionAssert.Contains(properties.Keys, "outer_name",
            "агент видит имя из AgentName, а не имя поля C#");
        CollectionAssert.DoesNotContain(properties.Keys, "wireName");
        Assert.AreEqual("wireName", rename["outer_name"],
            "роутер обязан переименовать ключ обратно перед отправкой в McpCommandHandler — "
            + "иначе ToObjectStrict отвергнет неизвестное поле");
    }

    [Test]
    public void RenameTable_IsEmpty_WhenNoFieldAsksForAnotherName()
    {
        CollectionAssert.IsEmpty(McpJsonSchema.RenameTable(Registry("edit_elements")),
            "лишняя запись в таблице переименования молча переименует поле, которое агент "
            + "прислал правильно");
    }

    [Test]
    public void IgnoredFields_AreAbsent()
    {
        var items = (Dictionary<string, object>)Property(Registry("edit_elements"), "ops")["items"];
        var opProperties = (Dictionary<string, object>)items["properties"];

        CollectionAssert.DoesNotContain(opProperties.Keys, "dimX",
            "EditOp.dimX помечен [McpIgnore] — это внутреннее имя старого провода, "
            + "агенту его показывать нельзя");
        CollectionAssert.Contains(opProperties.Keys, "width",
            "а width рядом с ним обязан остаться — иначе тест зеленеет от того, что схема пуста");
    }

    /// <summary>Отдельный тест, потому что в нынешнем контракте ни одно поле не носит
    /// [McpIgnore] и [McpParam] одновременно: dimX/dimY/dimZ помечены только запретом,
    /// и проверка на [McpIgnore] на настоящих Params* никогда не срабатывает. Убери её —
    /// IgnoredFields_AreAbsent останется зелёным. Фикстура делает ветку достижимой,
    /// чтобы запрет не оказался мёртвым кодом в тот день, когда описание к такому полю
    /// всё-таки допишут.</summary>
    [Test]
    public void McpIgnore_WinsOverMcpParam_OnTheSameField()
    {
        var properties = (Dictionary<string, object>)
            McpJsonSchema.ForTool(Tool(typeof(DoublyMarkedParams)))["properties"];

        CollectionAssert.DoesNotContain(properties.Keys, "legacyAlias",
            "[McpIgnore] означает «этого поля агент не видит» независимо от того, есть ли "
            + "рядом описание");
        CollectionAssert.Contains(properties.Keys, "keep");
        CollectionAssert.IsEmpty(McpJsonSchema.RenameTable(Tool(typeof(DoublyMarkedParams)))
            .Keys.Where(k => k == "legacyAlias").ToList(),
            "скрытое поле не имеет права попасть и в таблицу переименования");
    }

    [Test]
    public void UnsupportedFieldType_Throws_NamingTheField()
    {
        var error = Assert.Throws<InvalidOperationException>(
            () => McpJsonSchema.ForTool(Tool(typeof(UnsupportedParams))));

        StringAssert.Contains("ticket", error!.Message,
            "молчаливый пропуск неизвестного типа выдаёт агенту схему без поля, которое "
            + "обработчик всё равно ждёт; в сообщении обязано быть имя поля");
        StringAssert.Contains("UnsupportedParams", error.Message,
            "и имя класса — иначе искать поле придётся по всему контракту");
    }

    [Test]
    public void Annotations_FollowTheKind()
    {
        var read = McpJsonSchema.Annotations(Registry("get_scene_tree"));
        Assert.AreEqual(true, read["readOnlyHint"], "чтение сцены не меняет её");
        Assert.AreEqual(false, read["openWorldHint"]);

        var write = McpJsonSchema.Annotations(Registry("edit_elements"));
        Assert.AreEqual(false, write["readOnlyHint"]);
        Assert.AreEqual(false, write["destructiveHint"],
            "edit_elements обратим через Ctrl+Z, значит не destructive");

        var destructive = McpJsonSchema.Annotations(Registry("delete_elements"));
        Assert.AreEqual(true, destructive["destructiveHint"],
            "клиент показывает удаление отдельным предупреждением — по этому флагу");

        var openWorld = McpToolRegistry.Tools.FirstOrDefault(t => t.OpenWorld);
        Assert.IsNotNull(openWorld, "в реестре обязан быть хотя бы один OpenWorld-инструмент, "
            + "иначе четвёртая строка таблицы аннотаций никем не проверена");
        var open = McpJsonSchema.Annotations(openWorld!);
        Assert.AreEqual(true, open["openWorldHint"]);
        Assert.IsFalse(open.ContainsKey("destructiveHint"),
            "ADVANCED_OPEN_WORLD в Node не объявлял destructiveHint вовсе");
    }

    /// <summary>Временный тест: он держит новую схему рядом с той, что до сих пор
    /// генерируется в Zod. Удаляется вместе с mcp-server/ и tools/McpContractGen.</summary>
    [Test]
    public void SchemaFieldSets_MatchTheZodEmitter_ToolByTool()
    {
        var zod = ZodFieldsByTool();
        var diffs = new List<string>();

        foreach (var tool in McpToolRegistry.Tools)
        {
            var mine = ((Dictionary<string, object>)McpJsonSchema.ForTool(tool)["properties"])
                .Keys.OrderBy(k => k, StringComparer.Ordinal).ToList();
            var theirs = zod.TryGetValue(tool.Name, out var fields)
                ? fields.OrderBy(k => k, StringComparer.Ordinal).ToList()
                : new List<string>();

            if (!mine.SequenceEqual(theirs, StringComparer.Ordinal))
                diffs.Add(tool.Name + ": C# [" + string.Join(", ", mine) + "] vs Zod ["
                    + string.Join(", ", theirs) + "]");
        }

        CollectionAssert.IsEmpty(diffs,
            "набор параметров, который агент увидит из приложения, разошёлся с тем, что до сих "
            + "пор отдаёт Node-мост. Пока живут оба, расхождение означает, что один из них врёт:\n"
            + string.Join("\n", diffs));
    }

    private static Dictionary<string, List<string>> ZodFieldsByTool()
    {
        var path = Path.Combine(RepoPaths.Subdir("mcp-server", "src"), "tools.generated.ts");
        Assert.IsTrue(File.Exists(path), "нет " + path + ": тест паритета смотрит в пустоту");

        var byTool = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        var name = new Regex("^    name: \"(?<n>[^\"]+)\",$");
        var field = new Regex("^      (?<f>\\w+): z\\.");
        string? current = null;
        var inSchema = false;

        foreach (var line in File.ReadAllLines(path))
        {
            var nameMatch = name.Match(line);
            if (nameMatch.Success)
            {
                current = nameMatch.Groups["n"].Value;
                byTool[current] = new List<string>();
                inSchema = false;
                continue;
            }
            if (line == "    inputSchema: {") { inSchema = true; continue; }
            if (inSchema && line == "    },") { inSchema = false; continue; }
            if (!inSchema || current == null) continue;

            var fieldMatch = field.Match(line);
            if (fieldMatch.Success) byTool[current].Add(fieldMatch.Groups["f"].Value);
        }

        Assert.GreaterOrEqual(byTool.Count, McpToolRegistry.Tools.Count,
            "разбор tools.generated.ts нашёл меньше инструментов, чем есть в реестре — "
            + "сравнивать нечего, и зелёный тест ничего бы не значил");
        return byTool;
    }
}
