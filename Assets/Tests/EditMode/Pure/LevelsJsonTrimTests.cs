using NUnit.Framework;
using KitchenDesigner.Core;

/// <summary>
/// Пустой <c>levelId</c> — это КАЖДАЯ деталь любого файла, снятого до появления этажей,
/// и каждая новая деталь без явного уровня; пустой <c>levels</c> — это КАЖДЫЙ проект,
/// где ни одного дополнительного этажа ещё не создавали. Писать оба в файл значит менять
/// КАЖДЫЙ файл пользователя и КАЖДЫЙ снапшот-эталон лишними ключами — приём уже опробован
/// на <c>KeyBindingsJsonTrim</c> для пустого <c>settings.keyBindings</c>. Отсутствие ключа
/// читается ТАК ЖЕ, как значение по умолчанию (инициализатор поля), так что урезание
/// ничего не меняет при обратном чтении.
/// </summary>
public class LevelsJsonTrimTests
{
    private const string TwoElementsOneWithLevel =
        "{\n" +
        "    \"version\": 1,\n" +
        "    \"elements\": [\n" +
        "        {\n" +
        "            \"name\": \"A\",\n" +
        "            \"levelId\": \"\",\n" +
        "            \"movable\": true\n" +
        "        },\n" +
        "        {\n" +
        "            \"name\": \"B\",\n" +
        "            \"levelId\": \"2\",\n" +
        "            \"movable\": true\n" +
        "        }\n" +
        "    ],\n" +
        "    \"levels\": [],\n" +
        "    \"lightsOn\": true\n" +
        "}";

    private const string LevelIdIsTheLastKeyBeforeClosingBrace =
        "{\n" +
        "    \"elements\": [\n" +
        "        {\n" +
        "            \"name\": \"A\",\n" +
        "            \"movable\": true,\n" +
        "            \"levelId\": \"\"\n" +
        "        }\n" +
        "    ]\n" +
        "}";

    private const string ProjectWithTwoRealLevels =
        "{\n" +
        "    \"elements\": [],\n" +
        "    \"levels\": [\n" +
        "        {\n" +
        "            \"id\": \"1\",\n" +
        "            \"name\": \"1 этаж\",\n" +
        "            \"floorElevationMm\": 0,\n" +
        "            \"heightMm\": 3000\n" +
        "        }\n" +
        "    ],\n" +
        "    \"lightsOn\": true\n" +
        "}";

    [Test]
    public void RemoveWhenEmpty_DropsEmptyLevelId_ButKeepsANonEmptyOne()
    {
        var trimmed = LevelsJsonTrim.RemoveWhenEmpty(TwoElementsOneWithLevel);

        var root = JsonText.RootObject(trimmed);
        var elements = JsonText.MemberValue(trimmed, root, "elements");
        var items = JsonText.ArrayItems(trimmed, elements);
        Assert.AreEqual(2, items.Count, "урезание не имеет права терять или сливать детали");

        Assert.IsFalse(JsonText.MemberValue(trimmed, items[0], "levelId").Found,
            "пустой levelId у первой детали обязан пропасть из файла целиком");
        var secondLevelId = JsonText.MemberValue(trimmed, items[1], "levelId");
        Assert.IsTrue(secondLevelId.Found, "levelId с настоящим значением — данные пользователя, трогать нельзя");
        Assert.AreEqual("\"2\"", secondLevelId.Text(trimmed));
    }

    [Test]
    public void RemoveWhenEmpty_DropsTheEmptyLevelsArray_AtProjectRoot()
    {
        var trimmed = LevelsJsonTrim.RemoveWhenEmpty(TwoElementsOneWithLevel);

        var root = JsonText.RootObject(trimmed);
        Assert.IsFalse(JsonText.MemberValue(trimmed, root, "levels").Found,
            "пустой список этажей — проект без единого лишнего уровня, ключ ни к чему");
        Assert.IsTrue(JsonText.MemberValue(trimmed, root, "lightsOn").Found,
            "соседнее поле после levels обязано пережить удаление");
    }

    [Test]
    public void RemoveWhenEmpty_KeepsANonEmptyLevelsArray_ByteForByte()
    {
        var trimmed = LevelsJsonTrim.RemoveWhenEmpty(ProjectWithTwoRealLevels);

        Assert.AreEqual(ProjectWithTwoRealLevels, trimmed,
            "хотя бы один реально созданный этаж — данные пользователя, их трогать нельзя");
    }

    [Test]
    public void RemoveWhenEmpty_StaysValidJson_NeighbourFieldsSurvive()
    {
        var trimmed = LevelsJsonTrim.RemoveWhenEmpty(TwoElementsOneWithLevel);

        var root = JsonText.RootObject(trimmed);
        Assert.IsTrue(root.Found);
        var elements = JsonText.MemberValue(trimmed, root, "elements");
        var items = JsonText.ArrayItems(trimmed, elements);
        Assert.IsTrue(JsonText.MemberValue(trimmed, items[0], "name").Found);
        Assert.IsTrue(JsonText.MemberValue(trimmed, items[0], "movable").Found);
        Assert.IsTrue(JsonText.MemberValue(trimmed, root, "lightsOn").Found);
    }

    [Test]
    public void RemoveWhenEmpty_LevelIdAsTheLastKey_LeavesNoDanglingComma()
    {
        var trimmed = LevelsJsonTrim.RemoveWhenEmpty(LevelIdIsTheLastKeyBeforeClosingBrace);

        StringAssert.DoesNotContain("levelId", trimmed);
        StringAssert.DoesNotContain(",\n        }", trimmed,
            "удаление последнего члена объекта не должно оставлять висячую запятую");
    }

    [Test]
    public void RemoveWhenEmpty_NoElementsArray_ReturnsInputUnchanged()
    {
        const string json = "{\n    \"version\": 1\n}";
        Assert.AreEqual(json, LevelsJsonTrim.RemoveWhenEmpty(json));
    }

    [Test]
    public void RemoveWhenEmpty_EmptyOrNullInput_ReturnsInputUnchanged()
    {
        Assert.AreEqual("", LevelsJsonTrim.RemoveWhenEmpty(""));
        Assert.IsNull(LevelsJsonTrim.RemoveWhenEmpty(null!));
    }

    [Test]
    public void RemoveWhenEmpty_NoElementsAtAll_LeavesAnEmptyArrayAlone()
    {
        const string json = "{\n    \"elements\": []\n}";
        Assert.AreEqual(json, LevelsJsonTrim.RemoveWhenEmpty(json));
    }
}
