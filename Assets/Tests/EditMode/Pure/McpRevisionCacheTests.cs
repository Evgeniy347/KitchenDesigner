using NUnit.Framework;
using KitchenDesigner.Core.MCP;

/// <summary>Замер показал 280-410 мс на create/get/delete_elements: почти всё время уходит на
/// пересчёт валидации ВСЕЙ сцены синхронно перед ответом, даже когда сцена не менялась между
/// двумя запросами. `McpRevisionCache` — общий механизм: пересчитывать заново только когда
/// ревизия сцены (`SceneRevision.Version`) реально изменилась, иначе отдавать прошлый
/// результат. Здесь — чистая логика кэша без Unity; `McpValidationCache` — тонкая обёртка
/// над ним для сцены.</summary>
public class McpRevisionCacheTests
{
    [Test]
    public void FirstCall_AlwaysComputes()
    {
        var cache = new McpRevisionCache<string>();
        int calls = 0;
        var result = cache.Get(1, () => { calls++; return "v1"; });

        Assert.AreEqual("v1", result);
        Assert.AreEqual(1, calls, "первый запрос обязан посчитать значение — кэш пуст");
        Assert.AreEqual(1, cache.TakeRecomputeCount());
    }

    [Test]
    public void SameRevision_ReturnsCachedValue_WithoutRecomputing()
    {
        var cache = new McpRevisionCache<string>();
        int calls = 0;
        cache.Get(5, () => { calls++; return "computed-" + calls; });
        var second = cache.Get(5, () => { calls++; return "computed-" + calls; });

        Assert.AreEqual("computed-1", second,
            "ревизия не изменилась между двумя запросами — второй обязан получить старое значение, не пересчитанное заново");
        Assert.AreEqual(1, calls, "compute() не должен вызываться второй раз на той же ревизии");
    }

    [Test]
    public void RevisionChanges_ForcesRecompute()
    {
        var cache = new McpRevisionCache<string>();
        int calls = 0;
        cache.Get(1, () => { calls++; return "computed-" + calls; });
        var afterChange = cache.Get(2, () => { calls++; return "computed-" + calls; });

        Assert.AreEqual("computed-2", afterChange,
            "ревизия сцены изменилась (мутация произошла) — кэш обязан пересчитать, а не отдать устаревший результат");
        Assert.AreEqual(2, calls);
    }

    [Test]
    public void RecomputeCount_IsTheWorkCounter_NotAClock()
    {
        var cache = new McpRevisionCache<string>();
        cache.Get(1, () => "a");
        cache.Get(1, () => "a");
        cache.Get(1, () => "a");
        cache.Get(2, () => "b");

        Assert.AreEqual(2, cache.TakeRecomputeCount(),
            "три запроса на одной ревизии и один на следующей — пересчётов должно быть ровно два: "
            + "это сторож на регресс, число не плавает от машины к машине, в отличие от миллисекунд");
    }

    [Test]
    public void TakeRecomputeCount_ResetsToZero()
    {
        var cache = new McpRevisionCache<string>();
        cache.Get(1, () => "a");
        cache.TakeRecomputeCount();

        Assert.AreEqual(0, cache.TakeRecomputeCount(),
            "TakeRecomputeCount забирает счётчик — повторное чтение без нового пересчёта обязано вернуть 0");
    }

    [Test]
    public void Reset_ForgetsTheCachedValue_NextCallRecomputesEvenOnTheSameRevision()
    {
        var cache = new McpRevisionCache<string>();
        int calls = 0;
        cache.Get(1, () => { calls++; return "computed-" + calls; });
        cache.Reset();
        var afterReset = cache.Get(1, () => { calls++; return "computed-" + calls; });

        Assert.AreEqual("computed-2", afterReset,
            "Reset() обязан забыть закэшированное значение — иначе тест на сцене между прогонами "
            + "унаследует ответ от предыдущего теста на той же ревизии");
        Assert.AreEqual(2, calls);
    }
}
