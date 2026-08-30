using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

/// <summary>
/// Правила инфраструктуры, которые раньше держались комментариями в исходниках:
/// откуда берётся версия сцены, кто имеет право читать
/// <c>Transform.hasChanged</c> и почему информационные логи идут без стека.
/// </summary>
public class InfrastructureContractTests
{
    private readonly List<GameObject> _spawned = new List<GameObject>();

    [TearDown]
    public void Teardown()
    {
        foreach (var go in _spawned)
            if (go != null) Object.DestroyImmediate(go);
        _spawned.Clear();
        PartRegistry.Clear();
        CommandStack.Clear();
    }

    private KitchenElement MakeBoard(string name, Vector3 position)
    {
        var go = ElementFactory.CreatePart(new Vector3Int(600, 18, 500), name, position);
        _spawned.Add(go);
        return go.GetComponent<KitchenElement>();
    }

    [Test]
    public void InfoLogs_CarryNoStackTrace_ButWarningsAndErrorsStillDo()
    {
        var previous = Application.GetStackTraceLogType(LogType.Log);
        try
        {
            Application.SetStackTraceLogType(LogType.Log, StackTraceLogType.ScriptOnly);
            var errorBefore = Application.GetStackTraceLogType(LogType.Error);

            Bootstrap.KeepStackTracesOutOfInfoLogs();

            Assert.AreEqual(StackTraceLogType.None, Application.GetStackTraceLogType(LogType.Log),
                "каждый Debug.Log тащил в Player.log около десяти строк стека и забивал лог");
            Assert.AreEqual(errorBefore, Application.GetStackTraceLogType(LogType.Error),
                "стек ошибок не трогаем — там он и нужен");
        }
        finally
        {
            Application.SetStackTraceLogType(LogType.Log, previous);
        }
    }

    [Test]
    public void SceneRevision_MovesOn_WhenAnElementIsRegistered()
    {
        int seen = SceneRevision.Version;
        MakeBoard("RevBoard", new Vector3(0f, 0.5f, 0f));

        Assert.AreNotEqual(seen, SceneRevision.Version,
            "реестр — одно из трёх узких мест, мимо которых изменение сцены не проходит");
    }

    [Test]
    public void SceneRevision_MovesOn_WhenACommandRuns()
    {
        int seen = SceneRevision.Version;

        CommandStack.Execute(new NoOpCommand());

        Assert.AreNotEqual(seen, SceneRevision.Version,
            "стек команд — второе узкое место: через него идёт каждое пользовательское "
            + "изменение, включая правки свойств, которые не трогают ни трансформ, ни реестр");
    }

    private sealed class NoOpCommand : IUndoCommand
    {
        public void Execute() { }

        public void Undo() { }

        public string Description => "тестовая команда";
    }

    [Test]
    public void SceneRevision_MovesOn_WhenAPoseChanges_AndTheTrackerEatsTheFlagOnce()
    {
        var board = MakeBoard("RevMove", new Vector3(0f, 0.5f, 0f));
        SceneChangeTracker.Poll();
        int seen = SceneRevision.Version;

        board.transform.position = new Vector3(1f, 0.5f, 0f);
        SceneChangeTracker.Poll();

        Assert.AreNotEqual(seen, SceneRevision.Version,
            "позы — третье узкое место, оно ловится по Transform.hasChanged");
        Assert.IsFalse(board.transform.hasChanged,
            "флаг гасит трекер и только он: прочитавший его сам «съел» бы сигнал у остальных");

        int afterFirstPoll = SceneRevision.Version;
        SceneChangeTracker.Poll();
        Assert.AreEqual(afterFirstPoll, SceneRevision.Version,
            "повторный опрос без движения версию не двигает");
    }

    private static string CoreSourceRoot =>
        Path.Combine(Application.dataPath, "Scripts", "Core");

    private static readonly string[] MayReadHasChanged = { "SceneChangeTracker.cs" };

    [Test]
    public void OnlySceneChangeTracker_ReadsTransformHasChanged()
    {
        var offenders = HasChangedReaders()
            .Where(name => !MayReadHasChanged.Contains(name))
            .ToList();

        Assert.IsEmpty(offenders,
            "hasChanged гасится в одном месте; кто прочитает его сам, тот съест сигнал "
            + "у всех остальных систем: " + string.Join(", ", offenders));
    }

    [Test]
    public void TheHasChangedScan_ActuallyReachesTheSources()
    {
        Assert.IsTrue(Directory.Exists(CoreSourceRoot),
            "скан по несуществующему каталогу прошёл бы зелёным, ничего не проверив");
        Assert.IsNotEmpty(Directory.GetFiles(CoreSourceRoot, "*.cs", SearchOption.AllDirectories),
            "в каталоге ядра обязаны быть исходники");
        CollectionAssert.Contains(HasChangedReaders(), "SceneChangeTracker.cs",
            "единственный законный читатель обязан находиться сканом — иначе скан "
            + "ищет не то, что нужно");

        foreach (var allowed in MayReadHasChanged)
            Assert.IsNotEmpty(
                Directory.GetFiles(CoreSourceRoot, allowed, SearchOption.AllDirectories),
                $"файл {allowed} из списка исключений больше не существует");
    }

    private static List<string> HasChangedReaders()
    {
        var readers = new List<string>();
        foreach (var path in Directory.GetFiles(CoreSourceRoot, "*.cs", SearchOption.AllDirectories))
            if (File.ReadAllText(path).Contains("hasChanged"))
                readers.Add(Path.GetFileName(path));
        return readers;
    }
}
