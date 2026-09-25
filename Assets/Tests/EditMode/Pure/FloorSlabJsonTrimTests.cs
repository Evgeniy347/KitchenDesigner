using NUnit.Framework;
using KitchenDesigner.Core;

/// <summary>Mirrors FoundationJsonTrimTests: ElementData now carries isFloorSlab + four detail
/// fields for EVERY element, not only FloorSlabElement, because JsonUtility cannot skip a field
/// by value. Without this trim, every old save and every one of the 30 golden snapshots would
/// silently gain five new lines per element — not lost data, but not byte-identical either.
/// basePlate is trimmed separately for the same reason LevelsJsonTrim and FoundationJsonTrim
/// both already do: it is a named root object, not an array entry.</summary>
public class FloorSlabJsonTrimTests
{
    private const string ProjectWithABasePlate =
        "{\n" +
        "    \"version\": 1,\n" +
        "    \"elements\": [],\n" +
        "    \"basePlateValid\": true,\n" +
        "    \"basePlate\": {\n" +
        "        \"name\": \"BasePlate\",\n" +
        "        \"isFloorSlab\": false,\n" +
        "        \"slabTechnology\": 0,\n" +
        "        \"slabConcreteGrade\": 1,\n" +
        "        \"slabRebarDiameterMm\": 12,\n" +
        "        \"slabRebarStepMm\": 300,\n" +
        "        \"isLightSource\": false\n" +
        "    },\n" +
        "    \"lightsOn\": true\n" +
        "}";

    [Test]
    public void RemoveWhenNotFloorSlab_ABasePlate_DropsAllFiveKeys_TooNotOnlyTheArray()
    {
        var trimmed = FloorSlabJsonTrim.RemoveWhenNotFloorSlab(ProjectWithABasePlate);

        StringAssert.DoesNotContain("isFloorSlab", trimmed);
        StringAssert.DoesNotContain("slabTechnology", trimmed);
        StringAssert.DoesNotContain("slabRebarStepMm", trimmed);

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
        "            \"isFloorSlab\": false,\n" +
        "            \"slabTechnology\": 0,\n" +
        "            \"slabConcreteGrade\": 1,\n" +
        "            \"slabRebarDiameterMm\": 12,\n" +
        "            \"slabRebarStepMm\": 300,\n" +
        "            \"isPillar\": false\n" +
        "        }\n" +
        "    ]\n" +
        "}";

    private const string ProjectWithAFloorSlab =
        "{\n" +
        "    \"version\": 1,\n" +
        "    \"elements\": [\n" +
        "        {\n" +
        "            \"name\": \"S1\",\n" +
        "            \"elementType\": \"FloorSlabElement\",\n" +
        "            \"isFloorSlab\": true,\n" +
        "            \"slabTechnology\": 0,\n" +
        "            \"slabConcreteGrade\": 2,\n" +
        "            \"slabRebarDiameterMm\": 14,\n" +
        "            \"slabRebarStepMm\": 250\n" +
        "        }\n" +
        "    ]\n" +
        "}";

    [Test]
    public void RemoveWhenNotFloorSlab_APlainBoard_DropsAllFiveKeys()
    {
        var trimmed = FloorSlabJsonTrim.RemoveWhenNotFloorSlab(ProjectWithAPlainBoard);

        StringAssert.DoesNotContain("isFloorSlab", trimmed);
        StringAssert.DoesNotContain("slabTechnology", trimmed);
        StringAssert.DoesNotContain("slabConcreteGrade", trimmed);
        StringAssert.DoesNotContain("slabRebarDiameterMm", trimmed);
        StringAssert.DoesNotContain("slabRebarStepMm", trimmed);
    }

    [Test]
    public void RemoveWhenNotFloorSlab_APlainBoard_KeepsItsOwnFieldsAndStaysValidJson()
    {
        var trimmed = FloorSlabJsonTrim.RemoveWhenNotFloorSlab(ProjectWithAPlainBoard);

        var root = JsonText.RootObject(trimmed);
        Assert.IsTrue(root.Found);
        var elements = JsonText.MemberValue(trimmed, root, "elements");
        var items = JsonText.ArrayItems(trimmed, elements);
        Assert.AreEqual(1, items.Count);
        Assert.IsTrue(JsonText.MemberValue(trimmed, items[0], "name").Found);
        Assert.IsTrue(JsonText.MemberValue(trimmed, items[0], "isWall").Found);
        Assert.IsTrue(JsonText.MemberValue(trimmed, items[0], "isPillar").Found,
            "поле ПОСЛЕ удалённой пятёрки обязано пережить срез пяти членов подряд — если "
            + "промежуточное удаление потеряло позицию, оно тоже пропало бы");
    }

    [Test]
    public void RemoveWhenNotFloorSlab_ARealFloorSlab_KeepsAllFiveKeysUntouched()
    {
        var result = FloorSlabJsonTrim.RemoveWhenNotFloorSlab(ProjectWithAFloorSlab);

        Assert.AreEqual(ProjectWithAFloorSlab, result,
            "настоящая плита — не мусор по умолчанию, её собственные значения трогать нельзя, "
            + "даже когда они совпадают с дефолтами полей");
    }

    [Test]
    public void RemoveWhenNotFloorSlab_NoElementsArray_ReturnsInputUnchanged()
    {
        const string json = "{\n    \"version\": 1\n}";
        Assert.AreEqual(json, FloorSlabJsonTrim.RemoveWhenNotFloorSlab(json));
    }

    [Test]
    public void RemoveWhenNotFloorSlab_EmptyOrNullInput_ReturnsInputUnchanged()
    {
        Assert.AreEqual("", FloorSlabJsonTrim.RemoveWhenNotFloorSlab(""));
        Assert.IsNull(FloorSlabJsonTrim.RemoveWhenNotFloorSlab(null!));
    }
}
