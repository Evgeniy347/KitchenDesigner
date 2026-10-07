using NUnit.Framework;
using KitchenDesigner.Core;

/// <summary>Mirrors FenceJsonTrimTests/FloorSlabJsonTrimTests: ElementData now carries
/// isInsulation/isVentGap/isCladding/wallLayerHostWallName/ventGapBattenStepMm for EVERY
/// element, and JsonUtility cannot skip a field by value — without this trim every old save
/// and golden snapshot silently gains five new lines per element (ba4c12b0 accepted 29
/// EditMode goldens for exactly this reason).</summary>
public class WallLayerJsonTrimTests
{
    [Test]
    public void RemoveWhenNotWallLayer_APlainBoard_DropsAllFiveKeys()
    {
        const string json =
            "{\n" +
            "    \"version\": 1,\n" +
            "    \"elements\": [\n" +
            "        {\n" +
            "            \"name\": \"B1\",\n" +
            "            \"elementType\": \"KitchenElement\",\n" +
            "            \"isWall\": false,\n" +
            "            \"isInsulation\": false,\n" +
            "            \"isVentGap\": false,\n" +
            "            \"isCladding\": false,\n" +
            "            \"wallLayerHostWallName\": \"\",\n" +
            "            \"ventGapBattenStepMm\": 600,\n" +
            "            \"isPillar\": false\n" +
            "        }\n" +
            "    ]\n" +
            "}";

        var trimmed = WallLayerJsonTrim.RemoveWhenNotWallLayer(json);

        StringAssert.DoesNotContain("isInsulation", trimmed);
        StringAssert.DoesNotContain("isVentGap", trimmed);
        StringAssert.DoesNotContain("isCladding", trimmed);
        StringAssert.DoesNotContain("wallLayerHostWallName", trimmed);
        StringAssert.DoesNotContain("ventGapBattenStepMm", trimmed);

        var root = JsonText.RootObject(trimmed);
        var elements = JsonText.MemberValue(trimmed, root, "elements");
        var items = JsonText.ArrayItems(trimmed, elements);
        Assert.AreEqual(1, items.Count);
        Assert.IsTrue(JsonText.MemberValue(trimmed, items[0], "name").Found);
        Assert.IsTrue(JsonText.MemberValue(trimmed, items[0], "isWall").Found);
        Assert.IsTrue(JsonText.MemberValue(trimmed, items[0], "isPillar").Found,
            "поле ПОСЛЕ удалённой пятёрки обязано пережить срез пяти членов подряд");
    }

    private const string ProjectWithAnInsulationLayer =
        "{\n" +
        "    \"version\": 1,\n" +
        "    \"elements\": [\n" +
        "        {\n" +
        "            \"name\": \"I1\",\n" +
        "            \"elementType\": \"InsulationElement\",\n" +
        "            \"isInsulation\": true,\n" +
        "            \"isVentGap\": false,\n" +
        "            \"isCladding\": false,\n" +
        "            \"wallLayerHostWallName\": \"Wall_1\",\n" +
        "            \"ventGapBattenStepMm\": 600\n" +
        "        }\n" +
        "    ]\n" +
        "}";

    [Test]
    public void RemoveWhenNotWallLayer_ARealInsulationLayer_KeepsItsFlagsButDropsTheVentGapDetail()
    {
        var trimmed = WallLayerJsonTrim.RemoveWhenNotWallLayer(ProjectWithAnInsulationLayer);

        var root = JsonText.RootObject(trimmed);
        var elements = JsonText.MemberValue(trimmed, root, "elements");
        var items = JsonText.ArrayItems(trimmed, elements);
        var element = items[0];

        Assert.IsTrue(JsonText.MemberValue(trimmed, element, "isInsulation").Found);
        Assert.AreEqual("true", JsonText.MemberValue(trimmed, element, "isInsulation").Text(trimmed));
        Assert.IsTrue(JsonText.MemberValue(trimmed, element, "isVentGap").Found,
            "своя ложная тройка остаётся при настоящем слое — как isFence остаётся у настоящего забора");
        Assert.IsTrue(JsonText.MemberValue(trimmed, element, "isCladding").Found);
        Assert.IsTrue(JsonText.MemberValue(trimmed, element, "wallLayerHostWallName").Found);
        Assert.IsFalse(JsonText.MemberValue(trimmed, element, "ventGapBattenStepMm").Found,
            "шаг обрешётки нужен только вент-зазору — у утеплителя это чужое неиспользуемое поле");
    }

    private const string ProjectWithAVentGap =
        "{\n" +
        "    \"version\": 1,\n" +
        "    \"elements\": [\n" +
        "        {\n" +
        "            \"name\": \"V1\",\n" +
        "            \"elementType\": \"VentGapElement\",\n" +
        "            \"isInsulation\": false,\n" +
        "            \"isVentGap\": true,\n" +
        "            \"isCladding\": false,\n" +
        "            \"wallLayerHostWallName\": \"Wall_1\",\n" +
        "            \"ventGapBattenStepMm\": 600\n" +
        "        }\n" +
        "    ]\n" +
        "}";

    [Test]
    public void RemoveWhenNotWallLayer_ARealVentGap_KeepsAllFiveKeysUntouched()
    {
        var result = WallLayerJsonTrim.RemoveWhenNotWallLayer(ProjectWithAVentGap);

        Assert.AreEqual(ProjectWithAVentGap, result,
            "настоящий вент-зазор пользуется шагом обрешётки — его собственные значения "
            + "трогать нельзя, даже когда они совпадают с дефолтами полей");
    }

    [Test]
    public void RemoveWhenNotWallLayer_NoElementsArray_ReturnsInputUnchanged()
    {
        const string json = "{\n    \"version\": 1\n}";
        Assert.AreEqual(json, WallLayerJsonTrim.RemoveWhenNotWallLayer(json));
    }

    [Test]
    public void RemoveWhenNotWallLayer_EmptyOrNullInput_ReturnsInputUnchanged()
    {
        Assert.AreEqual("", WallLayerJsonTrim.RemoveWhenNotWallLayer(""));
        Assert.IsNull(WallLayerJsonTrim.RemoveWhenNotWallLayer(null!));
    }
}
