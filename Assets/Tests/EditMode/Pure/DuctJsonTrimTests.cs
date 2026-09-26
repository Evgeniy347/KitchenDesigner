using NUnit.Framework;
using KitchenDesigner.Core;

/// <summary>V3 — как FenceJsonTrim/RoofJsonTrim, но однопроходный: RemoveFromElementsArray
/// идёт через JsonText.RewriteArrayItems вместо O(n) RemoveMember по всему документу на
/// каждое поле (conventions/PERFORMANCE.md — "чинить долг по скорости сразу"). ElementData
/// несёт isDuct/isGrille + свои поля на КАЖДОМ элементе; без среза старые сохранения и
/// золотые снапшоты молча обрастают лишними строками.</summary>
public class DuctJsonTrimTests
{
    [Test]
    public void RemoveWhenNotDuct_APlainBoard_DropsAllDuctAndGrilleKeys()
    {
        const string json =
            "{\n" +
            "    \"elements\": [\n" +
            "        {\n" +
            "            \"name\": \"B1\",\n" +
            "            \"isWall\": false,\n" +
            "            \"isDuct\": false,\n" +
            "            \"ductProfileKind\": 0,\n" +
            "            \"ductDiameterMm\": 125,\n" +
            "            \"ductWidthMm\": 200,\n" +
            "            \"ductHeightMm\": 150,\n" +
            "            \"ductAirflowM3PerHour\": 60,\n" +
            "            \"isGrille\": false,\n" +
            "            \"grilleAirflowM3PerHour\": 60,\n" +
            "            \"isPillar\": false\n" +
            "        }\n" +
            "    ]\n" +
            "}";

        var trimmed = DuctJsonTrim.RemoveWhenNotDuct(json);

        StringAssert.DoesNotContain("isDuct", trimmed);
        StringAssert.DoesNotContain("ductProfileKind", trimmed);
        StringAssert.DoesNotContain("ductDiameterMm", trimmed);
        StringAssert.DoesNotContain("ductWidthMm", trimmed);
        StringAssert.DoesNotContain("ductHeightMm", trimmed);
        StringAssert.DoesNotContain("ductAirflowM3PerHour", trimmed);
        StringAssert.DoesNotContain("isGrille", trimmed);
        StringAssert.DoesNotContain("grilleAirflowM3PerHour", trimmed);

        var root = JsonText.RootObject(trimmed);
        var elements = JsonText.MemberValue(trimmed, root, "elements");
        var items = JsonText.ArrayItems(trimmed, elements);
        Assert.AreEqual(1, items.Count);
        Assert.IsTrue(JsonText.MemberValue(trimmed, items[0], "name").Found);
        Assert.IsTrue(JsonText.MemberValue(trimmed, items[0], "isWall").Found);
        Assert.IsTrue(JsonText.MemberValue(trimmed, items[0], "isPillar").Found,
            "поле ПОСЛЕ обеих удалённых групп обязано пережить срез");
    }

    [Test]
    public void RemoveWhenNotDuct_ARealDuct_KeepsItsOwnFieldsUntouched()
    {
        const string json =
            "{\n" +
            "    \"elements\": [\n" +
            "        {\n" +
            "            \"name\": \"D1\",\n" +
            "            \"isDuct\": true,\n" +
            "            \"ductProfileKind\": 1,\n" +
            "            \"ductDiameterMm\": 125,\n" +
            "            \"ductWidthMm\": 200,\n" +
            "            \"ductHeightMm\": 150,\n" +
            "            \"ductAirflowM3PerHour\": 90,\n" +
            "            \"isGrille\": false,\n" +
            "            \"grilleAirflowM3PerHour\": 60\n" +
            "        }\n" +
            "    ]\n" +
            "}";

        var trimmed = DuctJsonTrim.RemoveWhenNotDuct(json);

        StringAssert.Contains("\"ductProfileKind\": 1", trimmed);
        StringAssert.Contains("\"ductWidthMm\": 200", trimmed);
        StringAssert.Contains("\"ductHeightMm\": 150", trimmed);
        StringAssert.Contains("\"ductAirflowM3PerHour\": 90", trimmed);
        StringAssert.DoesNotContain("isGrille", trimmed);
        StringAssert.DoesNotContain("grilleAirflowM3PerHour", trimmed);
    }

    [Test]
    public void RemoveWhenNotDuct_ARealGrille_KeepsItsOwnFieldUntouched_DropsDuctFields()
    {
        const string json =
            "{\n" +
            "    \"elements\": [\n" +
            "        {\n" +
            "            \"name\": \"G1\",\n" +
            "            \"isDuct\": false,\n" +
            "            \"ductProfileKind\": 0,\n" +
            "            \"ductDiameterMm\": 125,\n" +
            "            \"ductWidthMm\": 200,\n" +
            "            \"ductHeightMm\": 150,\n" +
            "            \"ductAirflowM3PerHour\": 60,\n" +
            "            \"isGrille\": true,\n" +
            "            \"grilleAirflowM3PerHour\": 45\n" +
            "        }\n" +
            "    ]\n" +
            "}";

        var trimmed = DuctJsonTrim.RemoveWhenNotDuct(json);

        StringAssert.DoesNotContain("ductProfileKind", trimmed);
        StringAssert.DoesNotContain("ductDiameterMm", trimmed);
        StringAssert.Contains("\"grilleAirflowM3PerHour\": 45", trimmed);
    }

    [Test]
    public void RemoveWhenNotDuct_NoElementsArray_ReturnsInputUnchanged()
    {
        const string json = "{\n    \"version\": 1\n}";
        Assert.AreEqual(json, DuctJsonTrim.RemoveWhenNotDuct(json));
    }

    [Test]
    public void RemoveWhenNotDuct_EmptyOrNullInput_ReturnsInputUnchanged()
    {
        Assert.AreEqual("", DuctJsonTrim.RemoveWhenNotDuct(""));
        Assert.IsNull(DuctJsonTrim.RemoveWhenNotDuct(null!));
    }
}
