using KitchenDesigner.Core;
using NUnit.Framework;

public class LocalizedCacheTests
{
    [Test]
    public void Value_SameRevision_IsBuiltOnce()
    {
        int builds = 0;
        var cache = new LocalizedCache<string[]>(() => { builds++; return new[] { "a" }; }, () => 7);

        var first = cache.Value;
        var second = cache.Value;

        Assert.AreSame(first, second);
        Assert.AreEqual(1, builds, "таблица подписей строится один раз на язык, а не на каждое обращение");
    }

    [Test]
    public void Value_AfterALanguageSwitch_IsRebuilt()
    {
        int revision = 1;
        int builds = 0;
        var cache = new LocalizedCache<string[]>(() => new[] { "build " + ++builds }, () => revision);

        var before = cache.Value;
        revision++;
        var after = cache.Value;

        Assert.AreNotSame(before, after,
            "статический список подписей, построенный до смены языка, иначе остался бы на старом языке");
        Assert.AreEqual("build 2", after[0]);
    }
}
