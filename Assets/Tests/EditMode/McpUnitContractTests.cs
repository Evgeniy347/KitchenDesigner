using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using KitchenDesigner.Core;
using KitchenDesigner.Core.MCP;
using KitchenDesigner.Core.MCP.Contract;

/// <summary>Единица измерения — свойство контракта, а не соглашение в описании.
/// McpContractSurfaceTests уже требует, чтобы КАЖДОЕ число называло свою единицу в
/// описании; этого мало — предупреждение спасает ровно до первой невнимательности,
/// и три дефекта ниже подтверждены на живой сцене (docs/TASK-units-standardization.md).
///
/// Сторожа здесь читаются как чек-лист миграции «мм — единственная единица внешнего
/// контракта»: пока они красные, перепутать метры с миллиметрами можно молча, без
/// отказа валидации, со сдвигом элемента на три порядка.</summary>
public class McpUnitContractTests
{
    private const BindingFlags PublicInstance = BindingFlags.Public | BindingFlags.Instance;

    private static readonly Regex MetreWord = new Regex(@"\bMET(ER|RE)S?\b", RegexOptions.IgnoreCase);
    private static readonly Regex MetreAbbreviation = new Regex(@"\(m\)");

    private McpCommandHandler? _handler;
    private readonly List<GameObject> _spawned = new List<GameObject>();

    [SetUp]
    public void Setup()
    {
        _handler = new McpCommandHandler();
        PartRegistry.Clear();
        GroupManager.Clear();
        CommandStack.Clear();
    }

    [TearDown]
    public void Teardown()
    {
        foreach (var go in _spawned)
            if (go != null) UnityEngine.Object.DestroyImmediate(go);
        _spawned.Clear();
        foreach (var e in PartRegistry.GetAll())
            if (e != null) UnityEngine.Object.DestroyImmediate(e.gameObject);
        PartRegistry.Clear();
        GroupManager.Clear();
        CommandStack.Clear();
    }

    private static McpRequest MakeReq(string method, object data) => new McpRequest
    {
        id = "t",
        method = method,
        Params = JObject.Parse(JsonConvert.SerializeObject(data))
    };

    private KitchenElement Make(string name, Vector3 pos, Vector3Int dims)
    {
        var go = new GameObject(name);
        go.transform.position = pos;
        var e = go.AddComponent<KitchenElement>();
        e.PartName = name;
        e.DimensionsMM = dims;
        e.Movable = true;
        PartRegistry.Register(e);
        _spawned.Add(go);
        return e;
    }

    private static bool MentionsMetres(string text) =>
        MetreWord.IsMatch(text) || MetreAbbreviation.IsMatch(text);

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

    private int[] AnchorMm(string name)
    {
        var resp = _handler!.Handle(MakeReq("get", new { names = new[] { name }, fields = new[] { "anchorMm" } }));
        Assert.AreEqual("result", resp.type, "get не ответил: " + JsonConvert.SerializeObject(resp.data));
        var jo = JObject.Parse(JsonConvert.SerializeObject(resp.data));
        return jo["elements"]![0]!["anchorMm"]!.ToObject<int[]>()!;
    }

    [Test]
    public void TheScan_SeesTheContract_AndDoesNotMistakeMillimetresForMetres()
    {
        Assert.Greater(ReachableParamsTypes().Count, 10,
            "типы параметров не нашлись — сторож ослеп, а не позеленел");
        Assert.IsFalse(MentionsMetres("Size along X in MM"),
            "чистое мм-описание принято за метровое");
        Assert.IsFalse(MentionsMetres("screw_base_diameter_mm, parameters of the drawer"),
            "diameter и parameters содержат meter как подстроку — граница слова обязана их отсечь");
        Assert.IsTrue(MentionsMetres("Target X in METERS."),
            "метровое описание не распознаётся — все проверки ниже зелены впустую");
    }

    [Test]
    public void NoAgentFacingNumber_IsMeasuredInMetres()
    {
        var metres = new List<string>();

        foreach (var tool in McpToolRegistry.Tools)
            if (MentionsMetres(tool.Description))
                metres.Add("tool " + tool.Name);

        foreach (var type in ReachableParamsTypes())
            foreach (var field in type.GetFields(PublicInstance))
            {
                var param = field.GetCustomAttribute<McpParamAttribute>();
                if (param == null) continue;
                if (MentionsMetres(param.Description)) metres.Add(type.Name + "." + field.Name);
            }

        CollectionAssert.IsEmpty(metres,
            "метры — утечка внутреннего представления Unity во внешний контракт. Миллиметры "
            + "уже доминируют (вся плановая и bulk-часть, инструкции проекта, целочисленность "
            + "значений), поэтому метровый остров нельзя ни запомнить, ни вывести из типа "
            + "инструмента: create_elements принимает x/y/z в метрах рядом с width/height/depth "
            + "в миллиметрах, а clone_elements.offset_* (метры) стоит рядом с move.dx/dy/dz "
            + "(миллиметры) — два смещения по мировым осям в разных единицах. Ошибка тихая: "
            + "x:433 вместо 0.433 не отвергается, а уезжает на 433 метра.\n"
            + string.Join("\n", metres));
    }

    [Test]
    public void GetAnchor_FedStraightBackIntoEditElements_LeavesTheElementWhereItWas()
    {
        Make("B2_bottom", new Vector3(0.715f, 0.108f, -3.344f), new Vector3Int(564, 16, 552));
        var before = AnchorMm("B2_bottom");

        var edit = _handler!.Handle(MakeReq("edit_elements", new
        {
            ops = new[] { new { name = "B2_bottom", anchor_x_mm = before[0], anchor_z_mm = before[1] } }
        }));
        Assert.AreEqual("result", edit.type,
            "edit_elements отказал: " + JsonConvert.SerializeObject(edit.data));

        CollectionAssert.AreEqual(before, AnchorMm("B2_bottom"),
            "цикл «прочитал → записал» не замыкается, и расхождений два, они складываются: "
            + "get отдаёт anchor в МИЛЛИМЕТРАХ и от МИНИМАЛЬНОГО УГЛА, а edit_elements ждёт "
            + "x/y/z в МЕТРАХ и от ЦЕНТРА. Клиент, честно подставивший прочитанное в запись, "
            + "промахивается и по масштабу, и по началу координат — без единого сообщения об "
            + "ошибке. Перевод одних только единиц чинит половину: этот сторож останется "
            + "красным, пока запись не примет тот же угол, что отдаёт чтение.");
    }

    [Test]
    public void CompactSize_ListsTheAxes_InTheSameOrderAsEveryOtherTool()
    {
        Make("B2_bottom", new Vector3(0.715f, 0.108f, -3.344f), new Vector3Int(564, 16, 552));

        var compact = _handler!.Handle(MakeReq("get",
            new { names = new[] { "B2_bottom" }, fields = new[] { "sizeMm" } }));
        Assert.AreEqual("result", compact.type, "get не ответил");
        var size = JObject.Parse(JsonConvert.SerializeObject(compact.data))
            ["elements"]![0]!["sizeMm"]!.ToObject<int[]>()!;

        var full = _handler!.Handle(MakeReq("get_elements",
            new { names = new[] { "B2_bottom" }, summary = true }));
        var row = JObject.Parse(JsonConvert.SerializeObject(full.data))["elements"]![0]!;

        CollectionAssert.AreEqual(
            new[] { (int)row["dimXMm"]!, (int)row["dimYMm"]!, (int)row["dimZMm"]! }, size,
            "get отдавал size как [width, depth, height] — единственное место во всей "
            + "поверхности, где глубина и высота меняются местами. Значения одного и того же "
            + "элемента, порядок разный, и ошибка такая же тихая, как с единицами: 16 и 552 "
            + "оба правдоподобны на своём месте.");
    }

    private static readonly (string method, object oldCall)[] MetreEraCalls =
    {
        ("create_elements", new { items = new[] { new { name = "Ghost", x = 0.715f, y = 0f, z = 0f } } }),
        ("edit_elements", new { ops = new[] { new { name = "B2_bottom", x = 0.715f } } }),
        ("clone_elements", new { ops = new[] { new { name = "B2_bottom", count = 1, offset_x = 0.3f } } }),
        ("snap_diagnose", new { ops = new[] { new { name = "B2_bottom", x = 0.715f } } }),
        ("set_photo_camera", new { target_x = 0.715f }),
        ("set_position", new { ops = new[] { new { object_path = "B2_bottom", x = 0.715f } } }),
    };

    [Test]
    public void AMetreEraCall_IsRefusedByFieldName_OnEveryToolThatEverTookMetres()
    {
        Make("B2_bottom", new Vector3(0.715f, 0.108f, -3.344f), new Vector3Int(564, 16, 552));
        LogAssert.ignoreFailingMessages = true;

        var accepted = new List<string>();
        foreach (var (method, oldCall) in MetreEraCalls)
        {
            var resp = _handler!.Handle(MakeReq(method, oldCall));
            var text = JsonConvert.SerializeObject(resp.data);
            if (resp.type != "error" || !text.Contains("Unknown field"))
                accepted.Add(method + " -> " + resp.type + " " + text);
        }

        CollectionAssert.IsEmpty(accepted,
            "чистое удаление метровых имён безопасно ровно потому, что McpJson.cs:20-24 "
            + "десериализует со MissingMemberHandling.Error: старый вызов обязан получить "
            + "отказ ПО ИМЕНИ поля, а не сдвиг на три порядка. Инструмент из списка ниже "
            + "читает параметры мимо McpJson — там старый вызов пройдёт молча, и удаление "
            + "имени перестаёт быть громким:\n" + string.Join("\n", accepted));
    }

    [Test]
    public void GetElementsSummary_ReportsPositionAndSize_InTheSameUnit()
    {
        Make("B2_bottom", new Vector3(0.715f, 0.108f, -3.344f), new Vector3Int(564, 16, 552));

        var resp = _handler!.Handle(MakeReq("get_elements", new { names = new[] { "B2_bottom" }, summary = true }));
        Assert.AreEqual("result", resp.type,
            "get_elements не ответил: " + JsonConvert.SerializeObject(resp.data));
        var row = JObject.Parse(JsonConvert.SerializeObject(resp.data))["elements"]![0]!;

        Assert.AreEqual(564, (int)row["dimXMm"]!, "размер и так в мм — опора проверки");
        Assert.AreEqual(715f, (float)row["posXMm"]!, 0.5f,
            "один плоский объект ответа нёс posX в метрах и dimX в миллиметрах, и ничто в "
            + "именах полей об этом не говорило. Единицы ответов не описаны нигде, кроме прозы "
            + "guide {topic:\"fields\"} — то есть читающий агент обязан был помнить, какая "
            + "половина объекта в чём. То же было и в get_free_space, где мм-поля суффикс "
            + "несли (sizeMmX), а метровые (minX, centerX) — нет.");
    }
}
