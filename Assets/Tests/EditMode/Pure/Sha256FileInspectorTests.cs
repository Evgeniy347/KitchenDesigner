#nullable disable
using System;
using System.IO;
using System.Text;
using System.Threading;
using NUnit.Framework;
using KitchenDesigner.Core.Update;

/// <summary>
/// Факты о файле на диске: есть ли он, сколько весит и какой у него SHA-256. Хеш сверен с
/// опубликованными эталонными значениями, поэтому подмена алгоритма или регистра не пройдёт.
/// </summary>
public class Sha256FileInspectorTests
{
    private const string AbcSha256 = "ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad";
    private const string EmptySha256 = "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855";

    private TempUpdateFolder _temp;

    [SetUp]
    public void SetUp() => _temp = new TempUpdateFolder();

    [TearDown]
    public void TearDown() => _temp.Dispose();

    private string PathFor(string name) => _temp.PathOf(name);

    [Test]
    public void Examine_AMissingFile_IsAbsent()
    {
        var facts = Sha256FileInspector.Examine(PathFor("nothing.exe"), true);

        Assert.IsFalse(facts.Exists);
    }

    [Test]
    public void Examine_KnownContent_GivesTheReferenceSha256()
    {
        _temp.Write("abc.bin", Encoding.ASCII.GetBytes("abc"));

        var facts = Sha256FileInspector.Examine(PathFor("abc.bin"), true);

        Assert.IsTrue(facts.Exists);
        Assert.AreEqual(3, facts.Size);
        Assert.AreEqual(AbcSha256, facts.Sha256);
    }

    [Test]
    public void Examine_AnEmptyFile_GivesTheEmptyInputSha256()
    {
        _temp.Write("empty.bin", new byte[0]);

        var facts = Sha256FileInspector.Examine(PathFor("empty.bin"), true);

        Assert.IsTrue(facts.Exists);
        Assert.AreEqual(0, facts.Size);
        Assert.AreEqual(EmptySha256, facts.Sha256);
    }

    [Test]
    public void Examine_WithoutAskingForAHash_ReturnsTheSizeOnly()
    {
        _temp.Write("abc.bin", Encoding.ASCII.GetBytes("abc"));

        var facts = Sha256FileInspector.Examine(PathFor("abc.bin"), false);

        Assert.IsTrue(facts.Exists);
        Assert.AreEqual(3, facts.Size);
        Assert.AreEqual(string.Empty, facts.Sha256);
    }

    [Test]
    public void Examine_ALargeFile_MatchesAnIndependentlyComputedHash()
    {
        var bytes = Payload.Bytes(3_000_000, seed: 7);
        _temp.Write("big.bin", bytes);

        var facts = Sha256FileInspector.Examine(PathFor("big.bin"), true);

        Assert.AreEqual(Payload.Sha256(bytes), facts.Sha256);
        Assert.AreEqual(bytes.Length, facts.Size);
    }

    [Test]
    public void Examine_OneFlippedByte_ChangesTheHash()
    {
        var bytes = Payload.Bytes(100_000, seed: 3);
        _temp.Write("good.bin", bytes);
        _temp.Write("bad.bin", Payload.Corrupted(bytes));

        var good = Sha256FileInspector.Examine(PathFor("good.bin"), true);
        var bad = Sha256FileInspector.Examine(PathFor("bad.bin"), true);

        Assert.AreEqual(good.Size, bad.Size);
        Assert.AreNotEqual(good.Sha256, bad.Sha256, "повреждение в один байт при том же размере обязано быть видно");
    }

    [Test]
    public void Examine_ALockedFile_ExistsButHasNoHash_SoItNeverPassesAsVerified()
    {
        _temp.Write("locked.bin", Encoding.ASCII.GetBytes("abc"));

        using (new FileStream(PathFor("locked.bin"), FileMode.Open, FileAccess.Read, FileShare.None))
        {
            var facts = Sha256FileInspector.Examine(PathFor("locked.bin"), true);

            Assert.IsTrue(facts.Exists);
            Assert.AreEqual(string.Empty, facts.Sha256);
            var verdict = new InstallerExpectation(AbcSha256, 3).Judge(facts);
            Assert.AreEqual(IntegrityVerdict.Mismatch, verdict);
        }
    }

    [Test]
    public void Inspect_ReportsThroughTheMainThread_NotOnItsOwn()
    {
        _temp.Write("abc.bin", Encoding.ASCII.GetBytes("abc"));
        var posted = new System.Collections.Concurrent.ConcurrentQueue<Action>();
        var inspector = new Sha256FileInspector(_temp.Folder, new QueueMainThread(posted));
        FileFacts result = default;
        var called = new ManualResetEventSlim();

        inspector.Inspect("abc.bin", true, facts => { result = facts; called.Set(); });
        SpinWait.SpinUntil(() => !posted.IsEmpty, TimeSpan.FromSeconds(10));

        Assert.IsFalse(called.IsSet, "колбэк не вызывается из рабочего потока: он ждёт, пока главный поток разберёт очередь");
        Assert.IsTrue(posted.TryDequeue(out var action));
        action();
        Assert.IsTrue(called.IsSet);
        Assert.AreEqual(AbcSha256, result.Sha256);
    }

    private sealed class QueueMainThread : IMainThread
    {
        private readonly System.Collections.Concurrent.ConcurrentQueue<Action> _queue;

        public QueueMainThread(System.Collections.Concurrent.ConcurrentQueue<Action> queue) => _queue = queue;

        public void Post(Action action) => _queue.Enqueue(action);
    }
}
