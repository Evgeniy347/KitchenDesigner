using NUnit.Framework;
using KitchenDesigner.Core;

public class FoundationJsonTrimTests
{
    private const string ProjectWithAPlainBoard =
        "{\n" +
        "    \"version\": 1,\n" +
        "    \"elements\": [\n" +
        "        {\n" +
        "            \"name\": \"B1\",\n" +
        "            \"elementType\": \"KitchenElement\",\n" +
        "            \"isWall\": false,\n" +
        "            \"isFoundation\": false,\n" +
        "            \"foundationSoilKind\": 5,\n" +
        "            \"foundationSandMm\": 100,\n" +
        "            \"foundationGravelMm\": 100,\n" +
        "            \"foundationCompacted\": true,\n" +
        "            \"foundationConcreteGrade\": 1,\n" +
        "            \"foundationRebarDiameterMm\": 12,\n" +
        "            \"foundationRebarStepMm\": 300,\n" +
        "            \"foundationCoverMm\": 40,\n" +
        "            \"isPillar\": false\n" +
        "        }\n" +
        "    ]\n" +
        "}";

    private const string ProjectWithAFoundation =
        "{\n" +
        "    \"version\": 1,\n" +
        "    \"elements\": [\n" +
        "        {\n" +
        "            \"name\": \"F1\",\n" +
        "            \"elementType\": \"FoundationElement\",\n" +
        "            \"isFoundation\": true,\n" +
        "            \"foundationSoilKind\": 3,\n" +
        "            \"foundationSandMm\": 120,\n" +
        "            \"foundationGravelMm\": 90,\n" +
        "            \"foundationCompacted\": false,\n" +
        "            \"foundationConcreteGrade\": 2,\n" +
        "            \"foundationRebarDiameterMm\": 14,\n" +
        "            \"foundationRebarStepMm\": 250,\n" +
        "            \"foundationCoverMm\": 45\n" +
        "        }\n" +
        "    ]\n" +
        "}";

    /// <summary>ElementData сериализует эту девятку полей для КАЖДОГО элемента, не только
    /// для ленты — JsonUtility не умеет пропускать поле по значению. Без обрезки каждый
    /// старый save (и все 30 золотых снапшотов, ни один из которых не знает о фундаменте)
    /// молча обзавёлся бы новыми строками — не потерянными данными, но и не тем, что было
    /// раньше побайтово.</summary>
    [Test]
    public void RemoveWhenNotFoundation_APlainBoard_DropsAllNineFoundationKeys()
    {
        var trimmed = FoundationJsonTrim.RemoveWhenNotFoundation(ProjectWithAPlainBoard);

        StringAssert.DoesNotContain("isFoundation", trimmed);
        StringAssert.DoesNotContain("foundationSoilKind", trimmed);
        StringAssert.DoesNotContain("foundationSandMm", trimmed);
        StringAssert.DoesNotContain("foundationGravelMm", trimmed);
        StringAssert.DoesNotContain("foundationCompacted", trimmed);
        StringAssert.DoesNotContain("foundationConcreteGrade", trimmed);
        StringAssert.DoesNotContain("foundationRebarDiameterMm", trimmed);
        StringAssert.DoesNotContain("foundationRebarStepMm", trimmed);
        StringAssert.DoesNotContain("foundationCoverMm", trimmed);
    }

    [Test]
    public void RemoveWhenNotFoundation_APlainBoard_KeepsItsOwnFieldsAndStaysValidJson()
    {
        var trimmed = FoundationJsonTrim.RemoveWhenNotFoundation(ProjectWithAPlainBoard);

        var root = JsonText.RootObject(trimmed);
        Assert.IsTrue(root.Found);
        var elements = JsonText.MemberValue(trimmed, root, "elements");
        var items = JsonText.ArrayItems(trimmed, elements);
        Assert.AreEqual(1, items.Count);
        Assert.IsTrue(JsonText.MemberValue(trimmed, items[0], "name").Found);
        Assert.IsTrue(JsonText.MemberValue(trimmed, items[0], "isWall").Found);
        Assert.IsTrue(JsonText.MemberValue(trimmed, items[0], "isPillar").Found,
            "поле ПОСЛЕ удалённой девятки обязано пережить срез девяти членов подряд — если "
            + "промежуточное удаление потеряло позицию, оно тоже пропало бы");
    }

    [Test]
    public void RemoveWhenNotFoundation_ARealFoundation_KeepsAllNineKeysUntouched()
    {
        var result = FoundationJsonTrim.RemoveWhenNotFoundation(ProjectWithAFoundation);

        Assert.AreEqual(ProjectWithAFoundation, result,
            "настоящая лента — не мусор по умолчанию, её собственные значения трогать нельзя, "
            + "даже когда они совпадают с дефолтами полей");
    }

    [Test]
    public void RemoveWhenNotFoundation_NoElementsArray_ReturnsInputUnchanged()
    {
        const string json = "{\n    \"version\": 1\n}";
        Assert.AreEqual(json, FoundationJsonTrim.RemoveWhenNotFoundation(json));
    }

    [Test]
    public void RemoveWhenNotFoundation_EmptyOrNullInput_ReturnsInputUnchanged()
    {
        Assert.AreEqual("", FoundationJsonTrim.RemoveWhenNotFoundation(""));
        Assert.IsNull(FoundationJsonTrim.RemoveWhenNotFoundation(null!));
    }
}
