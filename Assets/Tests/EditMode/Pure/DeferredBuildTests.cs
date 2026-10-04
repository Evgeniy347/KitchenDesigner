using System;
using System.Collections.Generic;
using KitchenDesigner.Core.UI;
using NUnit.Framework;

/// <summary>
/// Инспектор строится лениво: окно открывают не в каждом сеансе, а его сборка стоила ~0,37 с
/// из ~0,5 с запуска интерфейса (замер 2026-10-04). Очередь шагов — на быстром пути: она не знает
/// про сцену, только про порядок и повторный вход.
/// </summary>
public class DeferredBuildTests
{
    private static DeferredBuild Of(List<int> log, int count)
    {
        var steps = new List<Action>();
        for (int i = 0; i < count; i++)
        {
            int n = i;
            steps.Add(() => log.Add(n));
        }
        return new DeferredBuild(steps);
    }

    [Test]
    public void RunAll_RunsEveryStepOnceInTheOrderGiven()
    {
        var log = new List<int>();
        var build = Of(log, 4);

        build.RunAll();
        build.RunAll();

        CollectionAssert.AreEqual(new[] { 0, 1, 2, 3 }, log, "каждый шаг один раз, по порядку");
        Assert.IsTrue(build.Done);
    }

    [Test]
    public void RunAll_CalledFromInsideAStep_DoesNotRunTheRemainingStepsOutOfOrder()
    {
        var log = new List<int>();
        DeferredBuild? build = null;
        build = new DeferredBuild(new Action[]
        {
            () => { log.Add(0); build!.RunAll(); log.Add(1); },
            () => log.Add(2),
        });

        build.RunAll();

        CollectionAssert.AreEqual(new[] { 0, 1, 2 }, log,
            "секция, которой при сборке понадобился доступ к инспектору, не должна запускать хвост очереди внутри себя");
    }

    [Test]
    public void AroundScope_WrapsTheWholeRunAllOnceAndEachPrewarmStep_EvenWhenAStepThrows()
    {
        var log = new List<string>();
        var build = new DeferredBuild(new Action[] { () => log.Add("a"), () => log.Add("b"), () => throw new InvalidOperationException() },
            run => { log.Add("<"); try { run(); } finally { log.Add(">"); } });

        build.TryPrewarmStep(DeferredBuild.QuietFramesBeforePrewarm, false);
        Assert.Throws<InvalidOperationException>(() => build.RunAll());

        CollectionAssert.AreEqual(new[] { "<", "a", ">", "<", "b", ">" }, log,
            "Prewarm-шаг и RunAll оборачиваются каждый по разу; оболочка закрывается и при исключении шага");
    }

    [Test]
    public void TryPrewarmStep_RunsOneStepPerCallOnceTheFirstSecondIsQuiet()
    {
        var log = new List<int>();
        var build = Of(log, 3);

        Assert.IsTrue(build.TryPrewarmStep(DeferredBuild.QuietFramesBeforePrewarm, inputActive: false));
        Assert.IsTrue(build.TryPrewarmStep(DeferredBuild.QuietFramesBeforePrewarm, inputActive: false));

        CollectionAssert.AreEqual(new[] { 0, 1 }, log, "шаг за кадр, а не вся сборка за один кадр");
        Assert.IsFalse(build.Done);
    }

    [Test]
    public void TryPrewarmStep_WaitsWhileTheStartupFramesRun()
    {
        var log = new List<int>();
        var build = Of(log, 2);

        Assert.IsFalse(build.TryPrewarmStep(DeferredBuild.QuietFramesBeforePrewarm - 1, inputActive: false));

        Assert.IsEmpty(log, "в первые кадры после запуска сборка не должна отнимать время у запуска");
    }

    [Test]
    public void TryPrewarmStep_WaitsWhileTheUserIsPressingSomething()
    {
        var log = new List<int>();
        var build = Of(log, 2);

        Assert.IsFalse(build.TryPrewarmStep(DeferredBuild.QuietFramesBeforePrewarm * 10, inputActive: true));

        Assert.IsEmpty(log, "шаг сборки посреди перетаскивания был бы заметным рывком");
    }

    [Test]
    public void TryPrewarmStep_AfterTheLastStep_ReportsNothingToDo()
    {
        var log = new List<int>();
        var build = Of(log, 1);
        build.RunAll();

        Assert.IsFalse(build.TryPrewarmStep(DeferredBuild.QuietFramesBeforePrewarm, inputActive: false));
    }

    [Test]
    public void TryPrewarmStep_CalledFromInsideAStep_RunsNothing()
    {
        var log = new List<int>();
        DeferredBuild? build = null;
        bool nested = true;
        build = new DeferredBuild(new Action[]
        {
            () => { nested = build!.TryPrewarmStep(DeferredBuild.QuietFramesBeforePrewarm, false); log.Add(0); },
            () => log.Add(1),
        });

        build.RunAll();

        Assert.IsFalse(nested);
        CollectionAssert.AreEqual(new[] { 0, 1 }, log);
    }
}
