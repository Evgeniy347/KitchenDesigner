using NUnit.Framework;
using KitchenDesigner.Core;

public class CreatedAtUtcJsonTrimTests
{
    [Test]
    public void RemoveWhenEmpty_DropsAnEmptyCreatedAtUtc()
    {
        string json = "{\"version\":1,\"createdAtUtc\":\"\",\"elements\":[]}";
        string result = CreatedAtUtcJsonTrim.RemoveWhenEmpty(json);
        Assert.AreEqual("{\"version\":1,\"elements\":[]}", result);
    }

    [Test]
    public void RemoveWhenEmpty_KeepsANonEmptyCreatedAtUtc()
    {
        string json = "{\"version\":1,\"createdAtUtc\":\"2026-01-01T00:00:00Z\",\"elements\":[]}";
        Assert.AreEqual(json, CreatedAtUtcJsonTrim.RemoveWhenEmpty(json));
    }

    [Test]
    public void RemoveWhenEmpty_LeavesJsonWithoutTheKeyAlone()
    {
        string json = "{\"version\":1,\"elements\":[]}";
        Assert.AreEqual(json, CreatedAtUtcJsonTrim.RemoveWhenEmpty(json));
    }

    [Test]
    public void RemoveWhenEmpty_HandlesNullAndEmptyInput()
    {
        Assert.IsNull(CreatedAtUtcJsonTrim.RemoveWhenEmpty(null!));
        Assert.AreEqual("", CreatedAtUtcJsonTrim.RemoveWhenEmpty(""));
    }
}
