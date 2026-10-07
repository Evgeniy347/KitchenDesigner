using NUnit.Framework;
using KitchenDesigner.Core;

/// <summary>test-results/review-persistence.md #3: `ElementData` rides a second time inside
/// `CommandRecord.convertState` (one entry, written by `ConvertElementCommand.ToRecord` on every
/// undo/redo-able element-type conversion), but none of the eight *JsonTrim classes ever looked
/// inside `undoHistory`/`redoHistory` - only `elements[]`. A user who once
/// converted a board into a panel (or any other structural conversion) got ~46 default-valued
/// family keys appended to that one `convertState[0]` record on every resave, growing forever
/// because nothing ever trims them back out.
///
/// `docs/example.save.json` has 1266 undo/redo records and not one carries a convert command
/// (all `convertState: []`), which is exactly why the review says "no repo fixture carries a
/// convert record" - the bug is real but nothing in the existing suite could see it.</summary>
public class ConvertStateHistoryJsonTrimTests
{
    private const string ProjectWithAConvertRecord =
        "{\n" +
        "    \"elements\": [],\n" +
        "    \"undoHistory\": [\n" +
        "        {\n" +
        "            \"type\": \"convert\",\n" +
        "            \"convertTo\": 0,\n" +
        "            \"convertState\": [\n" +
        "                {\n" +
        "                    \"name\": \"Board1\",\n" +
        "                    \"levelId\": \"\",\n" +
        "                    \"isFence\": false,\n" +
        "                    \"fencePostSectionMm\": 60,\n" +
        "                    \"isFloorSlab\": false,\n" +
        "                    \"isFoundation\": false,\n" +
        "                    \"foundationCoverMm\": 0,\n" +
        "                    \"isInsulation\": false,\n" +
        "                    \"isVentGap\": false,\n" +
        "                    \"isCladding\": false,\n" +
        "                    \"wallLayerHostWallName\": \"\",\n" +
        "                    \"isRoof\": false,\n" +
        "                    \"roofRafterStepMm\": 0,\n" +
        "                    \"isDuct\": false,\n" +
        "                    \"isGrille\": false,\n" +
        "                    \"movable\": true\n" +
        "                }\n" +
        "            ]\n" +
        "        }\n" +
        "    ],\n" +
        "    \"redoHistory\": []\n" +
        "}";

    private const string ProjectWithNestedChildren =
        "{\n" +
        "    \"elements\": [],\n" +
        "    \"undoHistory\": [\n" +
        "        {\n" +
        "            \"type\": \"group\",\n" +
        "            \"children\": [\n" +
        "                {\n" +
        "                    \"type\": \"convert\",\n" +
        "                    \"convertState\": [\n" +
        "                        {\n" +
        "                            \"name\": \"Nested\",\n" +
        "                            \"isFence\": false,\n" +
        "                            \"fencePostSectionMm\": 60,\n" +
        "                            \"movable\": true\n" +
        "                        }\n" +
        "                    ]\n" +
        "                }\n" +
        "            ]\n" +
        "        }\n" +
        "    ]\n" +
        "}";

    [Test]
    public void RemoveWhenNotNeeded_DropsFamilyKeys_FromConvertStateInUndoHistory()
    {
        var trimmed = ConvertStateHistoryJsonTrim.RemoveWhenNotNeeded(ProjectWithAConvertRecord);

        var root = JsonText.RootObject(trimmed);
        var undo = JsonText.MemberValue(trimmed, root, "undoHistory");
        var records = JsonText.ArrayItems(trimmed, undo);
        Assert.AreEqual(1, records.Count, "запись отмены не имеет права пропасть или слиться");

        var record = records[0];
        Assert.IsTrue(JsonText.MemberValue(trimmed, record, "type").Found,
            "поле самой записи команды обязано пережить срез вложенного convertState");

        var convertState = JsonText.MemberValue(trimmed, record, "convertState");
        var items = JsonText.ArrayItems(trimmed, convertState);
        Assert.AreEqual(1, items.Count, "convertState хранит РОВНО один снимок - терять его нельзя");

        var element = items[0].Text(trimmed);
        foreach (var key in new[]
                 {
                     "levelId", "isFence", "fencePostSectionMm", "isFloorSlab", "isFoundation",
                     "foundationCoverMm", "isInsulation", "isVentGap", "isCladding",
                     "wallLayerHostWallName", "isRoof", "roofRafterStepMm", "isDuct", "isGrille",
                 })
        {
            StringAssert.DoesNotContain(key, element,
                $"{key} - мусорный ключ ElementData внутри convertState, обязан пропасть так же, "
                + "как он пропадает из elements[]");
        }

        Assert.IsTrue(JsonText.MemberValue(trimmed, items[0], "name").Found,
            "имя детали в снимке отмены - не мусор");
        Assert.IsTrue(JsonText.MemberValue(trimmed, items[0], "movable").Found,
            "поле ПОСЛЕ удалённых ключей обязано пережить срез");
    }

    [Test]
    public void RemoveWhenNotNeeded_RecursesIntoChildren_ForCompositeCommands()
    {
        var trimmed = ConvertStateHistoryJsonTrim.RemoveWhenNotNeeded(ProjectWithNestedChildren);

        StringAssert.DoesNotContain("isFence", trimmed);
        StringAssert.DoesNotContain("fencePostSectionMm", trimmed);

        var root = JsonText.RootObject(trimmed);
        var undo = JsonText.MemberValue(trimmed, root, "undoHistory");
        var records = JsonText.ArrayItems(trimmed, undo);
        var children = JsonText.ArrayItems(trimmed, JsonText.MemberValue(trimmed, records[0], "children"));
        var convertState = JsonText.ArrayItems(trimmed, JsonText.MemberValue(trimmed, children[0], "convertState"));

        Assert.AreEqual(1, convertState.Count);
        Assert.IsTrue(JsonText.MemberValue(trimmed, convertState[0], "name").Found,
            "снимок внутри вложенной children[] команды обязан пережить срез своих же мусорных ключей");
    }

    [Test]
    public void RemoveWhenNotNeeded_NoHistory_ReturnsInputUnchanged()
    {
        const string json = "{\n    \"elements\": []\n}";
        Assert.AreEqual(json, ConvertStateHistoryJsonTrim.RemoveWhenNotNeeded(json));
    }

    [Test]
    public void RemoveWhenNotNeeded_EmptyOrNullInput_ReturnsInputUnchanged()
    {
        Assert.AreEqual("", ConvertStateHistoryJsonTrim.RemoveWhenNotNeeded(""));
        Assert.IsNull(ConvertStateHistoryJsonTrim.RemoveWhenNotNeeded(null!));
    }

    [Test]
    public void ThePerFamilyTrimsAlone_StillMissConvertState_ThisIsWhyTheDedicatedTrimExists()
    {
        var stillDirty = FenceJsonTrim.RemoveWhenNotFence(ProjectWithAConvertRecord);

        StringAssert.Contains("isFence", stillDirty,
            "документирует находку #3: ни один из семи RemoveWhenNotXxx не заглядывает в "
            + "undoHistory/redoHistory сам по себе - это обязан делать именно "
            + "ConvertStateHistoryJsonTrim, подключённый отдельным шагом в ProjectJson.Serialize");
    }
}
