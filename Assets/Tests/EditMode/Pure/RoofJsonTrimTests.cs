using NUnit.Framework;
using KitchenDesigner.Core;

/// <summary>Mirrors FenceJsonTrimTests/FloorSlabJsonTrimTests: ElementData now carries
/// isRoof + five detail fields for EVERY element, and JsonUtility cannot skip a field by
/// value — without this trim every old save and golden snapshot would silently gain six new
/// lines per element.</summary>
public class RoofJsonTrimTests
{
    private const string ProjectWithABasePlate =
        "{\n" +
        "    \"version\": 1,\n" +
        "    \"elements\": [],\n" +
        "    \"basePlateValid\": true,\n" +
        "    \"basePlate\": {\n" +
        "        \"name\": \"BasePlate\",\n" +
        "        \"isRoof\": false,\n" +
        "        \"roofType\": 1,\n" +
        "        \"roofRidgeAxis\": 0,\n" +
        "        \"roofPitchDeg\": 30,\n" +
        "        \"roofOverhangMm\": 500,\n" +
        "        \"roofRafterStepMm\": 600,\n" +
        "        \"isLightSource\": false\n" +
        "    },\n" +
        "    \"lightsOn\": true\n" +
        "}";

    [Test]
    public void RemoveWhenNotRoof_ABasePlate_DropsAllSixKeys_TooNotOnlyTheArray()
    {
        var trimmed = RoofJsonTrim.RemoveWhenNotRoof(ProjectWithABasePlate);

        StringAssert.DoesNotContain("isRoof", trimmed);
        StringAssert.DoesNotContain("roofType", trimmed);
        StringAssert.DoesNotContain("roofRidgeAxis", trimmed);
        StringAssert.DoesNotContain("roofPitchDeg", trimmed);
        StringAssert.DoesNotContain("roofOverhangMm", trimmed);
        StringAssert.DoesNotContain("roofRafterStepMm", trimmed);

        var root = JsonText.RootObject(trimmed);
        var basePlate = JsonText.MemberValue(trimmed, root, "basePlate");
        Assert.IsTrue(basePlate.Found, "basePlate — не мусор, сама запись обязана остаться");
        Assert.IsTrue(JsonText.MemberValue(trimmed, basePlate, "name").Found);
        Assert.IsTrue(JsonText.MemberValue(trimmed, basePlate, "isLightSource").Found,
            "поле ПОСЛЕ удалённой шестёрки внутри basePlate обязано пережить срез");
        Assert.IsTrue(JsonText.MemberValue(trimmed, root, "lightsOn").Found,
            "корневое поле ПОСЛЕ basePlate обязано пережить срез именованного объекта");
    }

    [Test]
    public void RemoveWhenNotRoof_APlainBoard_DropsAllSixKeys()
    {
        const string json =
            "{\n" +
            "    \"version\": 1,\n" +
            "    \"elements\": [\n" +
            "        {\n" +
            "            \"name\": \"B1\",\n" +
            "            \"elementType\": \"KitchenElement\",\n" +
            "            \"isWall\": false,\n" +
            "            \"isRoof\": false,\n" +
            "            \"roofType\": 1,\n" +
            "            \"roofRidgeAxis\": 0,\n" +
            "            \"roofPitchDeg\": 30,\n" +
            "            \"roofOverhangMm\": 500,\n" +
            "            \"roofRafterStepMm\": 600,\n" +
            "            \"isPillar\": false\n" +
            "        }\n" +
            "    ]\n" +
            "}";

        var trimmed = RoofJsonTrim.RemoveWhenNotRoof(json);

        StringAssert.DoesNotContain("isRoof", trimmed);
        StringAssert.DoesNotContain("roofType", trimmed);
        StringAssert.DoesNotContain("roofRidgeAxis", trimmed);
        StringAssert.DoesNotContain("roofPitchDeg", trimmed);
        StringAssert.DoesNotContain("roofOverhangMm", trimmed);
        StringAssert.DoesNotContain("roofRafterStepMm", trimmed);

        var root = JsonText.RootObject(trimmed);
        var elements = JsonText.MemberValue(trimmed, root, "elements");
        var items = JsonText.ArrayItems(trimmed, elements);
        Assert.AreEqual(1, items.Count);
        Assert.IsTrue(JsonText.MemberValue(trimmed, items[0], "name").Found);
        Assert.IsTrue(JsonText.MemberValue(trimmed, items[0], "isWall").Found);
        Assert.IsTrue(JsonText.MemberValue(trimmed, items[0], "isPillar").Found,
            "поле ПОСЛЕ удалённой шестёрки обязано пережить срез шести членов подряд");
    }

    private const string ProjectWithARoof =
        "{\n" +
        "    \"version\": 1,\n" +
        "    \"elements\": [\n" +
        "        {\n" +
        "            \"name\": \"R1\",\n" +
        "            \"elementType\": \"RoofElement\",\n" +
        "            \"isRoof\": true,\n" +
        "            \"roofType\": 1,\n" +
        "            \"roofRidgeAxis\": 0,\n" +
        "            \"roofPitchDeg\": 30,\n" +
        "            \"roofOverhangMm\": 500,\n" +
        "            \"roofRafterStepMm\": 600\n" +
        "        }\n" +
        "    ]\n" +
        "}";

    [Test]
    public void RemoveWhenNotRoof_ARealRoof_KeepsAllSixKeysUntouched()
    {
        var result = RoofJsonTrim.RemoveWhenNotRoof(ProjectWithARoof);

        Assert.AreEqual(ProjectWithARoof, result,
            "настоящая крыша — не мусор по умолчанию, её собственные значения трогать нельзя, "
            + "даже когда они совпадают с дефолтами полей");
    }

    [Test]
    public void RemoveWhenNotRoof_NoElementsArray_ReturnsInputUnchanged()
    {
        const string json = "{\n    \"version\": 1\n}";
        Assert.AreEqual(json, RoofJsonTrim.RemoveWhenNotRoof(json));
    }

    [Test]
    public void RemoveWhenNotRoof_EmptyOrNullInput_ReturnsInputUnchanged()
    {
        Assert.AreEqual("", RoofJsonTrim.RemoveWhenNotRoof(""));
        Assert.IsNull(RoofJsonTrim.RemoveWhenNotRoof(null!));
    }
}
