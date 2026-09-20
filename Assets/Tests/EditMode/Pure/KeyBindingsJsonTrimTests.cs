using NUnit.Framework;
using KitchenDesigner.Core.Keybinding;

public class KeyBindingsJsonTrimTests
{
    private const string ProjectWithEmptyKeyBindings =
        "{\n" +
        "    \"version\": 1,\n" +
        "    \"settings\": {\n" +
        "        \"gridStep\": 18,\n" +
        "        \"constructionCompacted\": true,\n" +
        "        \"keyBindings\": []\n" +
        "    },\n" +
        "    \"lightsOn\": true\n" +
        "}";

    private const string ProjectWithOneOverride =
        "{\n" +
        "    \"version\": 1,\n" +
        "    \"settings\": {\n" +
        "        \"gridStep\": 18,\n" +
        "        \"constructionCompacted\": true,\n" +
        "        \"keyBindings\": [\n" +
        "            {\n" +
        "                \"action\": \"Undo\",\n" +
        "                \"primary\": \"U\",\n" +
        "                \"alt\": \"\"\n" +
        "            }\n" +
        "        ]\n" +
        "    },\n" +
        "    \"lightsOn\": true\n" +
        "}";

    [Test]
    public void KeyBindingsJsonTrim_RemoveWhenEmpty_DropsTheMemberAndItsPrecedingComma()
    {
        var trimmed = KeyBindingsJsonTrim.RemoveWhenEmpty(ProjectWithEmptyKeyBindings);

        StringAssert.DoesNotContain("keyBindings", trimmed,
            "полностью дефолтные привязки не должны попадать в файл вовсе");
        StringAssert.DoesNotContain(",\n    }", trimmed,
            "удаление последнего члена объекта не должно оставлять висячую запятую");
    }

    [Test]
    public void KeyBindingsJsonTrim_RemoveWhenEmpty_StaysValidJson_AfterRemoval()
    {
        var trimmed = KeyBindingsJsonTrim.RemoveWhenEmpty(ProjectWithEmptyKeyBindings);

        var root = KitchenDesigner.Core.JsonText.RootObject(trimmed);
        Assert.IsTrue(root.Found);
        var settings = KitchenDesigner.Core.JsonText.MemberValue(trimmed, root, "settings");
        Assert.IsTrue(settings.Found);
        var lightsOn = KitchenDesigner.Core.JsonText.MemberValue(trimmed, root, "lightsOn");
        Assert.IsTrue(lightsOn.Found, "соседние поля обязаны пережить удаление keyBindings");
    }

    [Test]
    public void KeyBindingsJsonTrim_RemoveWhenEmpty_LeavesANonEmptyOverrideArrayUntouched()
    {
        var result = KeyBindingsJsonTrim.RemoveWhenEmpty(ProjectWithOneOverride);

        Assert.AreEqual(ProjectWithOneOverride, result,
            "непустой список переопределений — это данные пользователя, их трогать нельзя");
    }

    [Test]
    public void KeyBindingsJsonTrim_RemoveWhenEmpty_NoSettingsObject_ReturnsInputUnchanged()
    {
        const string json = "{\n    \"version\": 1\n}";
        Assert.AreEqual(json, KeyBindingsJsonTrim.RemoveWhenEmpty(json));
    }

    [Test]
    public void KeyBindingsJsonTrim_RemoveWhenEmpty_EmptyOrNullInput_ReturnsInputUnchanged()
    {
        Assert.AreEqual("", KeyBindingsJsonTrim.RemoveWhenEmpty(""));
        Assert.IsNull(KeyBindingsJsonTrim.RemoveWhenEmpty(null!));
    }
}
