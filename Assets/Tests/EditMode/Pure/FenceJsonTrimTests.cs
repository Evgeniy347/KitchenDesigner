using NUnit.Framework;
using KitchenDesigner.Core;

/// <summary>Mirrors FloorSlabJsonTrimTests / FoundationJsonTrimTests: ElementData now carries
/// isFence + four detail fields for EVERY element, and JsonUtility cannot skip a field by
/// value — without this trim every old save and golden snapshot would silently gain five new
/// lines per element.</summary>
public class FenceJsonTrimTests
{
    private const string ProjectWithABasePlate =
        "{\n" +
        "    \"version\": 1,\n" +
        "    \"elements\": [],\n" +
        "    \"basePlateValid\": true,\n" +
        "    \"basePlate\": {\n" +
        "        \"name\": \"BasePlate\",\n" +
        "        \"isFence\": false,\n" +
        "        \"fencePostSectionMm\": 60,\n" +
        "        \"fencePostStepMm\": 2500,\n" +
        "        \"fencePitDepthMm\": 1200,\n" +
        "        \"fenceSheetMark\": 0,\n" +
        "        \"isLightSource\": false\n" +
        "    },\n" +
        "    \"lightsOn\": true\n" +
        "}";

    [Test]
    public void RemoveWhenNotFence_ABasePlate_DropsAllFiveKeys_TooNotOnlyTheArray()
    {
        var trimmed = FenceJsonTrim.RemoveWhenNotFence(ProjectWithABasePlate);

        StringAssert.DoesNotContain("isFence", trimmed);
        StringAssert.DoesNotContain("fencePostSectionMm", trimmed);
        StringAssert.DoesNotContain("fenceSheetMark", trimmed);

        var root = JsonText.RootObject(trimmed);
        var basePlate = JsonText.MemberValue(trimmed, root, "basePlate");
        Assert.IsTrue(basePlate.Found, "basePlate — не мусор, сама запись обязана остаться");
        Assert.IsTrue(JsonText.MemberValue(trimmed, basePlate, "name").Found);
        Assert.IsTrue(JsonText.MemberValue(trimmed, basePlate, "isLightSource").Found,
            "поле ПОСЛЕ удалённой пятёрки внутри basePlate обязано пережить срез");
        Assert.IsTrue(JsonText.MemberValue(trimmed, root, "lightsOn").Found,
            "корневое поле ПОСЛЕ basePlate обязано пережить срез именованного объекта");
    }

    private const string ProjectWithAPlainBoard =
        "{\n" +
        "    \"version\": 1,\n" +
        "    \"elements\": [\n" +
        "        {\n" +
        "            \"name\": \"B1\",\n" +
        "            \"elementType\": \"KitchenElement\",\n" +
        "            \"isWall\": false,\n" +
        "            \"isFence\": false,\n" +
        "            \"fencePostSectionMm\": 60,\n" +
        "            \"fencePostStepMm\": 2500,\n" +
        "            \"fencePitDepthMm\": 1200,\n" +
        "            \"fenceSheetMark\": 0,\n" +
        "            \"isPillar\": false\n" +
        "        }\n" +
        "    ]\n" +
        "}";

    private const string ProjectWithAFence =
        "{\n" +
        "    \"version\": 1,\n" +
        "    \"elements\": [\n" +
        "        {\n" +
        "            \"name\": \"F1\",\n" +
        "            \"elementType\": \"FenceElement\",\n" +
        "            \"isFence\": true,\n" +
        "            \"fencePostSectionMm\": 60,\n" +
        "            \"fencePostStepMm\": 2500,\n" +
        "            \"fencePitDepthMm\": 1200,\n" +
        "            \"fenceSheetMark\": 0\n" +
        "        }\n" +
        "    ]\n" +
        "}";

    [Test]
    public void RemoveWhenNotFence_APlainBoard_DropsAllFiveKeys()
    {
        var trimmed = FenceJsonTrim.RemoveWhenNotFence(ProjectWithAPlainBoard);

        StringAssert.DoesNotContain("isFence", trimmed);
        StringAssert.DoesNotContain("fencePostSectionMm", trimmed);
        StringAssert.DoesNotContain("fencePostStepMm", trimmed);
        StringAssert.DoesNotContain("fencePitDepthMm", trimmed);
        StringAssert.DoesNotContain("fenceSheetMark", trimmed);
    }

    [Test]
    public void RemoveWhenNotFence_APlainBoard_KeepsItsOwnFieldsAndStaysValidJson()
    {
        var trimmed = FenceJsonTrim.RemoveWhenNotFence(ProjectWithAPlainBoard);

        var root = JsonText.RootObject(trimmed);
        Assert.IsTrue(root.Found);
        var elements = JsonText.MemberValue(trimmed, root, "elements");
        var items = JsonText.ArrayItems(trimmed, elements);
        Assert.AreEqual(1, items.Count);
        Assert.IsTrue(JsonText.MemberValue(trimmed, items[0], "name").Found);
        Assert.IsTrue(JsonText.MemberValue(trimmed, items[0], "isWall").Found);
        Assert.IsTrue(JsonText.MemberValue(trimmed, items[0], "isPillar").Found,
            "поле ПОСЛЕ удалённой пятёрки обязано пережить срез пяти членов подряд");
    }

    [Test]
    public void RemoveWhenNotFence_ARealFence_KeepsAllFiveKeysUntouched()
    {
        var result = FenceJsonTrim.RemoveWhenNotFence(ProjectWithAFence);

        Assert.AreEqual(ProjectWithAFence, result,
            "настоящий забор — не мусор по умолчанию, его собственные значения трогать нельзя, "
            + "даже когда они совпадают с дефолтами полей");
    }

    [Test]
    public void RemoveWhenNotFence_NoElementsArray_ReturnsInputUnchanged()
    {
        const string json = "{\n    \"version\": 1\n}";
        Assert.AreEqual(json, FenceJsonTrim.RemoveWhenNotFence(json));
    }

    [Test]
    public void RemoveWhenNotFence_EmptyOrNullInput_ReturnsInputUnchanged()
    {
        Assert.AreEqual("", FenceJsonTrim.RemoveWhenNotFence(""));
        Assert.IsNull(FenceJsonTrim.RemoveWhenNotFence(null!));
    }
}
