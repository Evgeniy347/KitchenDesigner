using System;
using NUnit.Framework;

/// <summary>Сторож на изоляцию, а не на код: <c>SceneScanLog._scans</c>/<c>_shares</c> и
/// кольцо <c>_recent</c> — process-global состояние (не <c>[ThreadStatic]</c>, как соседние
/// счётчики этого файла), а <c>geometry/pure-tests/ParallelAssemblyInfo.cs</c> гонит фикстуры
/// параллельно (<c>[Parallelizable(ParallelScope.Fixtures)]</c>) на разных потоках. Три
/// фикстуры трогают это состояние — <c>SceneScanLogTests</c> (через <c>SceneScanLog.Note</c>),
/// <c>SceneScanCounterTests</c> и <c>SceneListAccessIsAccountedTests</c> (через
/// <c>SceneScanCounter</c>) — и без взаимного исключения сосед на другом потоке вклинивает
/// свои <c>Note</c> между «before» и проверкой точной дельты
/// (<c>SceneScanCounterTests.EveryScan_MovesTheCounterByOne</c> и подобные).
///
/// Каждая из трёх обязана нести <c>[NonParallelizable]</c> — тогда NUnit гонит их строго по
/// одной (остальные фикстуры чужого счётчика не касаются, поэтому им параллелизм не
/// мешает).</summary>
public class SceneScanCounterIsolationTests
{
    private static readonly Type[] FixturesTouchingTheSharedScanCounter =
    {
        typeof(SceneScanLogTests),
        typeof(SceneScanCounterTests),
        typeof(SceneListAccessIsAccountedTests),
    };

    [Test]
    public void EveryFixtureTouchingTheSharedScanCounter_RunsExclusively()
    {
        var missing = new System.Collections.Generic.List<string>();
        foreach (var type in FixturesTouchingTheSharedScanCounter)
            if (!Attribute.IsDefined(type, typeof(NonParallelizableAttribute)))
                missing.Add(type.Name);

        Assert.IsEmpty(missing,
            "эти фикстуры читают или пишут process-global SceneScanLog._scans/_shares/_recent "
            + "(через SceneScanLog или SceneScanCounter) без блокировки; под "
            + "[Parallelizable(ParallelScope.Fixtures)] сосед на другом потоке вклинивает свой "
            + "Note() между «before» и проверкой точной дельты. Без [NonParallelizable]: "
            + string.Join(", ", missing));
    }
}
