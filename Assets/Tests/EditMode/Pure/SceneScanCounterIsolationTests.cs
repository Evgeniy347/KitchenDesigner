using System;
using System.Collections.Generic;
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
/// Каждая из трёх обязана нести <c>[Parallelizable(ParallelScope.None)]</c> — тогда NUnit
/// гонит их строго по одной (остальные фикстуры чужого счётчика не касаются, поэтому им
/// параллелизм не мешает). Не <c>[NonParallelizable]</c>: у Unity свой урезанный
/// <c>nunit.framework.dll</c> (<c>com.unity.ext.nunit</c>), и в нём этого класса нет —
/// только <c>ParallelizableAttribute</c> и <c>ParallelScope</c> сам по себе.</summary>
public class SceneScanCounterIsolationTests
{
    private static readonly Type[] FixturesTouchingTheSharedScanCounter =
    {
        typeof(SceneScanLogTests),
        typeof(SceneScanCounterTests),
        typeof(SceneListAccessIsAccountedTests),
    };

    /// <summary>Unity's trimmed nunit.framework.dll (com.unity.ext.nunit) keeps the scope
    /// NUnit picked in a private field with a different name than the NuGet build's
    /// auto-property backing field — reading the VALUE portably needs reflection that would
    /// itself depend on which build is loaded. Presence is enough here: no fixture in this
    /// codebase carries a class-level [Parallelizable] for any other reason, so an explicit
    /// attribute on one of these three IS the opt-out from the assembly's
    /// [Parallelizable(ParallelScope.Fixtures)] default.</summary>
    private static bool RunsExclusively(Type type) =>
        Attribute.IsDefined(type, typeof(ParallelizableAttribute));

    [Test]
    public void EveryFixtureTouchingTheSharedScanCounter_RunsExclusively()
    {
        var missing = new List<string>();
        foreach (var type in FixturesTouchingTheSharedScanCounter)
            if (!RunsExclusively(type))
                missing.Add(type.Name);

        Assert.IsEmpty(missing,
            "эти фикстуры читают или пишут process-global SceneScanLog._scans/_shares/_recent "
            + "(через SceneScanLog или SceneScanCounter) без блокировки; под "
            + "[Parallelizable(ParallelScope.Fixtures)] сосед на другом потоке вклинивает свой "
            + "Note() между «before» и проверкой точной дельты. Без "
            + "[Parallelizable(ParallelScope.None)]: " + string.Join(", ", missing));
    }
}
