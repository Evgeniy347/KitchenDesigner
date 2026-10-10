#nullable disable
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using KitchenDesigner.Core.UI;
using KitchenDesigner.Core.Update;

/// <summary>
/// Склейка автообновления с движком: консольный адаптер, очередь главного потока и настоящее
/// окно «Доступно обновление» внутри настоящего потока обновления. Логика потока проверена
/// быстрыми тестами в Pure; здесь — только то, что без движка не проверить: окно появляется
/// ровно тогда, когда файл загружен и проверен, а строка консоли доходит до разработчика.
/// </summary>
public class UpdateGlueTests
{
    private const string Latest = "0.2100";
    private static readonly string FinalName = InstallerFileName.For(Latest);

    private TempUpdateFolder _temp;
    private Canvas _canvas;
    private GameObject _host;

    [SetUp]
    public void SetUp()
    {
        _temp = new TempUpdateFolder();
        UIFactory.EnsureEventSystem();
        _canvas = UIFactory.CreateCanvas("UpdateGlueCanvas");
        _host = new GameObject("UpdateGlueHost");
    }

    [TearDown]
    public void TearDown()
    {
        if (_host != null) UnityEngine.Object.DestroyImmediate(_host);
        if (_canvas != null) UnityEngine.Object.DestroyImmediate(_canvas.gameObject);
        _temp.Dispose();
    }

    private sealed class LogCatcher : IDisposable
    {
        public readonly List<(LogType type, string text)> Lines = new List<(LogType, string)>();

        public LogCatcher() => Application.logMessageReceived += OnLog;

        public void Dispose() => Application.logMessageReceived -= OnLog;

        private void OnLog(string condition, string stackTrace, LogType type) => Lines.Add((type, condition));
    }

    [Test]
    public void ConsoleAdapter_WritesEachLevelAsTheMatchingEngineLog_WithTheUpdatePrefix()
    {
        using var catcher = new LogCatcher();
        var console = new UnityUpdateConsole();
        LogAssert.Expect(LogType.Warning, UnityUpdateConsole.Prefix + "w");
        LogAssert.Expect(LogType.Error, UnityUpdateConsole.Prefix + "e");

        console.Write(UpdateLogLevel.Info, "i");
        console.Write(UpdateLogLevel.Warning, "w");
        console.Write(UpdateLogLevel.Error, "e");

        CollectionAssert.AreEqual(new[]
        {
            (LogType.Log, UnityUpdateConsole.Prefix + "i"),
            (LogType.Warning, UnityUpdateConsole.Prefix + "w"),
            (LogType.Error, UnityUpdateConsole.Prefix + "e"),
        }, catcher.Lines);
    }

    [Test]
    public void ConsoleAdapter_LinesReachTheDeveloperConsoleKinds_ThroughTheOverlaysOwnMapping()
    {
        Assert.AreEqual(ConsoleLineKind.Log, ConsoleOverlay.KindOf(LogType.Log));
        Assert.AreEqual(ConsoleLineKind.Warning, ConsoleOverlay.KindOf(LogType.Warning));
        Assert.AreEqual(ConsoleLineKind.Error, ConsoleOverlay.KindOf(LogType.Error),
            "ошибка обновления в консоли разработчика красная, а не серая строка журнала");
    }

    [Test]
    public void MainThreadQueue_RunsPostedActionsInOrder_OnlyWhenDrained()
    {
        var queue = _host.AddComponent<MainThreadQueue>();
        var order = new List<int>();

        queue.Post(() => order.Add(1));
        queue.Post(() => order.Add(2));
        Assert.IsEmpty(order, "Post не исполняет сам: действие ждёт кадра главного потока");

        Assert.AreEqual(2, queue.Drain());
        CollectionAssert.AreEqual(new[] { 1, 2 }, order);
        Assert.AreEqual(0, queue.Drain());
    }

    [Test]
    public void MainThreadQueue_AcceptsPostsFromAnotherThread()
    {
        var queue = _host.AddComponent<MainThreadQueue>();
        int ranOn = -1;
        int mainThread = Thread.CurrentThread.ManagedThreadId;

        var worker = new Thread(() => queue.Post(() => ranOn = Thread.CurrentThread.ManagedThreadId));
        worker.Start();
        worker.Join();
        queue.Drain();

        Assert.AreEqual(mainThread, ranOn, "действие из рабочего потока исполняется в потоке, который вызвал Drain");
    }

    [Test]
    public void MainThreadQueue_ABrokenAction_DoesNotStopTheOnesAfterIt()
    {
        var queue = _host.AddComponent<MainThreadQueue>();
        bool second = false;
        LogAssert.Expect(LogType.Exception, new Regex("boom"));

        queue.Post(() => throw new InvalidOperationException("boom"));
        queue.Post(() => second = true);
        queue.Drain();

        Assert.IsTrue(second);
    }

    [Test]
    public void MainThreadQueue_IgnoresANullAction()
    {
        var queue = _host.AddComponent<MainThreadQueue>();

        queue.Post(null);

        Assert.AreEqual(0, queue.Drain());
    }

    private UpdateFlow FlowWithTheRealDialog(byte[] installer, out UpdateDialogUI dialog,
        out RecordingApplier applier, out RecordingConsole console)
    {
        var dialogHost = new GameObject("UpdateDialogHost");
        dialogHost.transform.SetParent(_host.transform);
        dialog = dialogHost.AddComponent<UpdateDialogUI>();
        dialog.Build(_canvas.transform);
        applier = new RecordingApplier();
        console = new RecordingConsole();
        var releases = new FakeReleaseSource
        {
            Answer = ReleaseLookup.Found(new ReleaseManifest
            {
                Version = Latest,
                FileName = FinalName,
                DownloadUrl = "https://example.invalid/i.exe",
                Sha256 = Payload.Sha256(installer),
                Size = installer.Length,
            }),
        };
        var downloader = new ScriptedDownloader(_temp.Folder);
        downloader.Payloads.Enqueue(installer);
        return new UpdateFlow(new UpdatePlanner("0.2000"), releases, _temp.Folder,
            new SyncInspector(_temp.Folder), downloader, console, dialog, applier, () => 0f);
    }

    [Test]
    public void TheRealDialog_AppearsOnlyAfterTheDownloadIsComplete_NamingTheNewVersion()
    {
        var installer = Payload.Bytes(10_000, 1);
        var flow = FlowWithTheRealDialog(installer, out var dialog, out _, out _);
        Assert.IsFalse(dialog.IsVisible);

        flow.Start();

        Assert.IsTrue(dialog.IsVisible);
        StringAssert.Contains(Latest, dialog.MessageText);
        Assert.IsTrue(File.Exists(_temp.PathOf(FinalName)), "к моменту окна файл уже на диске под готовым именем");
    }

    [Test]
    public void TheRealDialog_AcceptButton_RunsTheInstallerFromTheUpdatesFolder()
    {
        var installer = Payload.Bytes(10_000, 2);
        var flow = FlowWithTheRealDialog(installer, out var dialog, out var applier, out _);
        flow.Start();

        dialog.UpdateButton.onClick.Invoke();

        CollectionAssert.AreEqual(new[] { _temp.PathOf(FinalName) }, applier.Applied);
        Assert.IsFalse(dialog.IsVisible);
    }

    [Test]
    public void TheRealDialog_CancelButton_KeepsTheFile_RunsNothing()
    {
        var installer = Payload.Bytes(10_000, 3);
        var flow = FlowWithTheRealDialog(installer, out var dialog, out var applier, out _);
        flow.Start();

        dialog.CancelButton.onClick.Invoke();

        Assert.IsEmpty(applier.Applied);
        Assert.IsTrue(File.Exists(_temp.PathOf(FinalName)));
        Assert.IsFalse(dialog.IsVisible);
    }

    [Test]
    public void TheRealDialog_NeverAppearsForAFailure_TheConsoleGetsTheLine()
    {
        var installer = Payload.Bytes(10_000, 4);
        var flow = FlowWithTheRealDialog(installer, out var dialog, out _, out var console);
        File.WriteAllText(_temp.Root, "a file where the folder should be");

        flow.Start();

        Assert.IsFalse(dialog.IsVisible, "сбой никогда не открывает модальное окно");
        Assert.AreEqual(UpdateLogLevel.Error, console.Lines[console.Lines.Count - 1].level);
    }
}
