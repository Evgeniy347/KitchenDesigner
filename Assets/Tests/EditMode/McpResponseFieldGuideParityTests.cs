using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using KitchenDesigner.Core.MCP;
using KitchenDesigner.Core.MCP.Contract;

/// <summary>ElementInfo — это ответ, который читает агент, а guide {topic:"fields"} —
/// единственное место, где значение полей ему объясняют. Два списка одного и того
/// же: поле появляется в ответе молча, а прозу никто не дописывает. Ровно так
/// разошлись схема create_elements и её прозаический перечень типов, где не
/// оказалось мойки.
///
/// Здесь живут смыслы полей, стоявшие раньше хвостовыми комментариями в
/// McpModels.cs.</summary>
public class McpResponseFieldGuideParityTests
{
    private static readonly Dictionary<string, string> UndocumentedForNow =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["active"] = "элемент включён; удалённый через MCP остаётся в сцене выключенным",
            ["cornerRadiusMm"] = "радиус скругления: угол радиусной полки или углы табуретки, иначе 0",
            ["grooves"] = "пазы детали «through:top, blind:left»; null, если пазов нет",
            ["textureOverlays"] = "накладки текстур стены/пола «a:oak; b:white@100,200+800x600»; null, если их нет",
            ["edgeBanding"] = "кромковать открытые торцы (только листовая деталь)",
            ["edgeThicknessMM"] = "толщина кромочной ленты, мм",
            ["edgeSkipValidation"] = "не выдавать EDG-01 по этой детали",
            ["edges"] = "торцы с кромкой, вычислено из сцены: «L1,W1»",
            ["attachedToName"] = "имя родителя AttachLinks: деталь едет за ним при переносе, повороте и открывании; null — не прикреплена",
            ["attachDetached"] = "связь есть, а контакта нет: сборка разъехалась (ошибка ATT-01)",
            ["faceNormalX"] = "мировая нормаль лицевой грани фасада",
            ["faceNormalY"] = "мировая нормаль лицевой грани фасада",
            ["faceNormalZ"] = "мировая нормаль лицевой грани фасада",
            ["faceInward"] = "фасад развёрнут лицом внутрь модуля",
            ["faceObstructions"] = "детали вплотную перед лицевой гранью фасада",
            ["openingViolations"] = "детали, пересекающие траекторию открывания",
            ["pillar"] = "свойства опоры, только для PillarElement",
            ["screwLeg"] = "свойства винтовой опоры, только для ScrewLegElement",
            ["cooktop"] = "свойства варочной, только для CooktopElement",
            ["oven"] = "свойства духовки, только для OvenElement",
            ["dishwasher"] = "свойства посудомойки, только для DishwasherElement",
            ["window"] = "свойства окна, только для WindowElement",
            ["door"] = "свойства двери, только для DoorElement",
        };

    private static string FieldsTopic() => McpGuideTexts.Topics["fields"];

    private static IReadOnlyList<string> ElementInfoFields() =>
        typeof(ElementInfo).GetFields(BindingFlags.Public | BindingFlags.Instance)
            .Select(f => f.Name).ToList();

    private static readonly string[] UnitSuffixes = { "MM", "Mm", "Deg", "Pct", "Px", "Sec", "M2" };

    private static string StripUnitSuffix(string field)
    {
        foreach (var unit in UnitSuffixes)
            if (field.Length > unit.Length && field.EndsWith(unit, StringComparison.Ordinal))
                return field.Substring(0, field.Length - unit.Length);
        return field;
    }

    private static string WithoutAxisOrUnitSuffix(string field) =>
        StripUnitSuffix(StripUnitSuffix(field).TrimEnd('X', 'Y', 'Z'));

    private static bool GuideExplains(string field)
    {
        var topic = FieldsTopic();
        return topic.Contains(field, StringComparison.Ordinal)
            || topic.Contains(WithoutAxisOrUnitSuffix(field), StringComparison.Ordinal);
    }

    [Test]
    public void TheGuideTopic_IsTheRealText_NotAnEmptyLookup()
    {
        var topic = FieldsTopic();
        StringAssert.Contains("RESPONSE FIELD SEMANTICS", topic,
            "сверка идёт с пустой строкой — сторож зеленеет, ничего не проверив");
        Assert.Greater(ElementInfoFields().Count, 30,
            "полей ElementInfo не нашлось — рефлексия смотрит не туда");
    }

    [Test]
    public void EveryElementInfoField_IsExplainedInTheGuide_OrListedAsAKnownGap()
    {
        var silent = ElementInfoFields()
            .Where(f => !GuideExplains(f))
            .Where(f => !UndocumentedForNow.ContainsKey(f))
            .ToList();

        CollectionAssert.IsEmpty(silent,
            "новое поле ответа без строки в guide {topic:\"fields\"} — это молчаливое "
            + "расхождение схемы и прозы: агент получает значение и трактует его наугад. "
            + "Либо опиши поле в McpGuideTexts, либо занеси его в UndocumentedForNow "
            + "со смыслом:\n" + string.Join("\n", silent));
    }

    [Test]
    public void EveryKnownGap_StillNamesALivingField()
    {
        var live = ElementInfoFields();
        foreach (var pair in UndocumentedForNow)
            CollectionAssert.Contains(live, pair.Key,
                "запись «" + pair.Key + "» (" + pair.Value + ") пережила своё поле: "
                + "список долгов начнёт молча прощать следующее поле с этим именем");
    }

    [Test]
    public void NoKnownGap_IsAlreadyDocumented_SoTheListCannotRot()
    {
        var stale = UndocumentedForNow.Keys.Where(GuideExplains).ToList();

        CollectionAssert.IsEmpty(stale,
            "поле уже описано в guide — убери его из списка долгов, иначе список "
            + "перестанет быть списком долгов:\n" + string.Join("\n", stale));
    }

    [Test]
    public void TheSubObjectsTheGuideAdvertises_AreAllRealFieldsOfTheResponse()
    {
        var advertised = new[] { "drawer", "table", "radiusTable", "stool", "chair" };
        var live = ElementInfoFields();

        foreach (var name in advertised)
            CollectionAssert.Contains(live, name,
                "guide {topic:\"fields\"} обещает агенту под-объект «" + name + "», "
                + "которого в ответе нет — обещание в прозе без поля в схеме клиент "
                + "проверить не может");
    }
}
