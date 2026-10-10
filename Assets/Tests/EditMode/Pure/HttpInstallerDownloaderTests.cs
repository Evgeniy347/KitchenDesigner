#nullable disable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using KitchenDesigner.Core.Update;

/// <summary>
/// Загрузчик на подставном HTTP-обработчике: сети нет, обработчик отдаёт заранее заданные
/// байты, коды и обрывы. Проверяется то, что остаётся на диске: .part с точными байтами, ни
/// одного готового файла до проверки, чистка после сбоя, повторы по политике и разные виды отказа.
/// </summary>
public class HttpInstallerDownloaderTests
{
    private static readonly string PartName = InstallerFileName.PartFor("0.2100");
    private static readonly TimeSpan NoPause = TimeSpan.Zero;

    private TempUpdateFolder _temp;
    private List<TimeSpan> _delays;

    [SetUp]
    public void SetUp()
    {
        _temp = new TempUpdateFolder();
        _temp.Folder.EnsureExists();
        _delays = new List<TimeSpan>();
    }

    [TearDown]
    public void TearDown() => _temp.Dispose();

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, int, CancellationToken, Task<HttpResponseMessage>> _respond;
        private int _calls;

        public StubHandler(Func<HttpRequestMessage, int, CancellationToken, Task<HttpResponseMessage>> respond) =>
            _respond = respond;

        public int Calls => _calls;
        public List<HttpRequestMessage> Requests { get; } = new List<HttpRequestMessage>();

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            int index = Interlocked.Increment(ref _calls);
            lock (Requests) Requests.Add(request);
            return _respond(request, index, ct);
        }
    }

    private static HttpResponseMessage Ok(byte[] bytes, bool declareLength = true)
    {
        var content = new StreamContent(new MemoryStream(bytes));
        if (declareLength) content.Headers.ContentLength = bytes.Length;
        return new HttpResponseMessage(HttpStatusCode.OK) { Content = content };
    }

    private static Task<HttpResponseMessage> Done(HttpResponseMessage response) => Task.FromResult(response);

    private HttpInstallerDownloader Downloader(StubHandler handler, UpdateRetryPolicy policy = null, TimeSpan? idle = null)
    {
        var http = new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan };
        return new HttpInstallerDownloader(http, _temp.Folder, new InlineMainThread(),
            policy ?? new UpdateRetryPolicy(0.001f, 0.002f), idle ?? TimeSpan.FromSeconds(30),
            (span, token) =>
            {
                lock (_delays) _delays.Add(span);
                return Task.CompletedTask;
            });
    }

    private static DownloadRequest Request(long expectedSize = 0) =>
        new DownloadRequest("https://example.invalid/i.exe", PartName, expectedSize);

    private static RecordingObserver Run(HttpInstallerDownloader downloader, DownloadRequest request, int seconds = 15)
    {
        var observer = new RecordingObserver();
        downloader.Download(request, observer);
        observer.Await(seconds);
        return observer;
    }

    [Test]
    public void Success_WritesExactlyTheServedBytes_IntoThePartFile_AndNothingElse()
    {
        var bytes = Payload.Bytes(1_500_000, seed: 11);
        var handler = new StubHandler((r, i, ct) => Done(Ok(bytes)));

        var observer = Run(Downloader(handler), Request());

        Assert.IsTrue(observer.Outcome.Succeeded, observer.Outcome.Reason);
        CollectionAssert.AreEqual(bytes, File.ReadAllBytes(_temp.PathOf(PartName)));
        CollectionAssert.AreEqual(new[] { PartName }, _temp.Names(),
            "загрузчик пишет только .part: готовое имя появляется после проверки, не раньше");
        Assert.AreEqual(1, observer.Finished);
        Assert.AreEqual(1, handler.Calls);
    }

    [Test]
    public void Progress_GrowsMonotonically_AndEndsAtTheFullSize()
    {
        var bytes = Payload.Bytes(500_000, seed: 12);
        var observer = Run(Downloader(new StubHandler((r, i, ct) => Done(Ok(bytes)))), Request());

        var progress = observer.Progress;
        Assert.IsNotEmpty(progress);
        Assert.AreEqual(bytes.Length, progress.Last().received);
        Assert.IsTrue(progress.All(p => p.total == bytes.Length), "общий размер берётся из Content-Length");
        for (int i = 1; i < progress.Count; i++)
            Assert.Greater(progress[i].received, progress[i - 1].received);
    }

    [Test]
    public void WithoutAContentLength_TheExpectedSizeIsTheTotal()
    {
        var bytes = Payload.Bytes(10_000, seed: 13);
        var observer = Run(Downloader(new StubHandler((r, i, ct) => Done(Ok(bytes, declareLength: false)))),
            Request(expectedSize: 10_000));

        Assert.IsTrue(observer.Outcome.Succeeded, observer.Outcome.Reason);
        Assert.IsTrue(observer.Progress.All(p => p.total == 10_000));
    }

    [Test]
    public void TheRequest_IsAGetToTheGivenUrl_WithTheUpdaterUserAgent()
    {
        var handler = new StubHandler((r, i, ct) => Done(Ok(new byte[10])));

        Run(Downloader(handler), Request());

        var sent = handler.Requests.Single();
        Assert.AreEqual(HttpMethod.Get, sent.Method);
        Assert.AreEqual("https://example.invalid/i.exe", sent.RequestUri.ToString());
        StringAssert.Contains(HttpInstallerDownloader.UserAgent, sent.Headers.UserAgent.ToString(),
            "GitHub отвечает 403 на запрос без User-Agent");
    }

    [Test]
    public void AnOlderPartFile_IsOverwritten_NotAppendedTo()
    {
        _temp.Write(PartName, Payload.Bytes(2_000_000, seed: 1));
        var bytes = Payload.Bytes(1000, seed: 2);

        Run(Downloader(new StubHandler((r, i, ct) => Done(Ok(bytes)))), Request());

        CollectionAssert.AreEqual(bytes, File.ReadAllBytes(_temp.PathOf(PartName)));
    }

    [Test]
    public void NotFound_FailsAtOnce_NamesTheStatus_AndLeavesNoPart()
    {
        var handler = new StubHandler((r, i, ct) => Done(new HttpResponseMessage(HttpStatusCode.NotFound)));

        var observer = Run(Downloader(handler), Request());

        Assert.IsFalse(observer.Outcome.Succeeded);
        StringAssert.Contains("404", observer.Outcome.Reason);
        Assert.AreEqual(1, handler.Calls, "404 не лечится повтором");
        Assert.IsEmpty(_temp.Names());
        Assert.IsEmpty(observer.Retries);
        Assert.IsEmpty(_delays);
    }

    [Test]
    public void AServerError_IsRetriedAfterThePolicyPause_ThenSucceeds()
    {
        var bytes = Payload.Bytes(5000, seed: 14);
        var handler = new StubHandler((r, i, ct) =>
            Done(i == 1 ? new HttpResponseMessage(HttpStatusCode.ServiceUnavailable) : Ok(bytes)));

        var observer = Run(Downloader(handler, new UpdateRetryPolicy(2f, 5f)), Request());

        Assert.IsTrue(observer.Outcome.Succeeded, observer.Outcome.Reason);
        Assert.AreEqual(2, handler.Calls);
        CollectionAssert.AreEqual(new[] { TimeSpan.FromSeconds(2) }, _delays);
        Assert.AreEqual(1, observer.Retries.Count);
        Assert.AreEqual(2, observer.Retries[0].attempt);
        Assert.AreEqual(3, observer.Retries[0].attempts);
        StringAssert.Contains("503", observer.Retries[0].reason);
        CollectionAssert.AreEqual(bytes, File.ReadAllBytes(_temp.PathOf(PartName)));
    }

    [Test]
    public void AServerThatKeepsFailing_StopsAfterTheLastAttempt_WithNoPartLeft()
    {
        var handler = new StubHandler((r, i, ct) => Done(new HttpResponseMessage(HttpStatusCode.BadGateway)));

        var observer = Run(Downloader(handler, new UpdateRetryPolicy(2f, 5f)), Request());

        Assert.IsFalse(observer.Outcome.Succeeded);
        StringAssert.Contains("502", observer.Outcome.Reason);
        Assert.AreEqual(3, handler.Calls);
        CollectionAssert.AreEqual(new[] { TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(5) }, _delays);
        Assert.IsEmpty(_temp.Names());
        Assert.AreEqual(1, observer.Finished);
    }

    [Test]
    public void NoNetwork_IsRetried_ThenReportedAsNoConnection()
    {
        var handler = new StubHandler((r, i, ct) => throw new HttpRequestException("не удаётся разрешить имя"));

        var observer = Run(Downloader(handler), Request());

        Assert.IsFalse(observer.Outcome.Succeeded);
        StringAssert.Contains("нет соединения", observer.Outcome.Reason);
        Assert.AreEqual(3, handler.Calls);
        Assert.IsEmpty(_temp.Names());
    }

    [Test]
    public void ABodyShorterThanDeclared_IsAFailure_NotASilentlyTruncatedInstaller()
    {
        var handler = new StubHandler((r, i, ct) =>
        {
            var content = new StreamContent(new MemoryStream(Payload.Bytes(60)));
            content.Headers.ContentLength = 100;
            return Done(new HttpResponseMessage(HttpStatusCode.OK) { Content = content });
        });

        var observer = Run(Downloader(handler), Request());

        Assert.IsFalse(observer.Outcome.Succeeded);
        StringAssert.Contains("60 из 100", observer.Outcome.Reason);
        Assert.AreEqual(3, handler.Calls, "оборванное соединение — тот случай, ради которого повторяем");
        Assert.IsEmpty(_temp.Names(), "недокачанный файл не остаётся под видом готового");
    }

    [Test]
    public void ABodyThatDiesInTheMiddle_IsAConnectionFailure_AndIsRetried()
    {
        var handler = new StubHandler((r, i, ct) =>
            Done(i < 3 ? new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(new DyingStream(1000)) }
                       : Ok(Payload.Bytes(2000))));

        var observer = Run(Downloader(handler), Request());

        Assert.IsTrue(observer.Outcome.Succeeded, observer.Outcome.Reason);
        Assert.AreEqual(3, handler.Calls);
        Assert.AreEqual(2000, new FileInfo(_temp.PathOf(PartName)).Length, "результат третьей попытки, а не склейка");
    }

    private sealed class DyingStream : Stream
    {
        private int _left;

        public DyingStream(int bytes) => _left = bytes;

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (_left == 0) throw new IOException("соединение разорвано");
            int n = Math.Min(Math.Min(count, _left), 400);
            _left -= n;
            return n;
        }

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    private sealed class FrozenStream : Stream
    {
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
            new TaskCompletionSource<int>().Task;

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    [Test]
    public void AStalledBody_IsCutOffByTheIdleTimeout_EvenWhenTheStreamIgnoresCancellation()
    {
        var handler = new StubHandler((r, i, ct) =>
            Done(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(new FrozenStream()) }));

        var observer = Run(Downloader(handler, new UpdateRetryPolicy(0.001f), TimeSpan.FromMilliseconds(150)), Request());

        Assert.IsFalse(observer.Outcome.Succeeded);
        StringAssert.Contains("встала", observer.Outcome.Reason);
        Assert.AreEqual(2, handler.Calls);
        Assert.IsEmpty(_temp.Names());
    }

    [Test]
    public void AServerThatNeverAnswers_IsCutOffByTheIdleTimeout()
    {
        var handler = new StubHandler((r, i, ct) => new TaskCompletionSource<HttpResponseMessage>().Task);

        var observer = Run(Downloader(handler, new UpdateRetryPolicy(0.001f), TimeSpan.FromMilliseconds(150)), Request());

        Assert.IsFalse(observer.Outcome.Succeeded);
        StringAssert.Contains("встала", observer.Outcome.Reason);
    }

    [Test]
    public void ADiskThatCannotBeWritten_FailsWithoutRetry_AndSaysItIsTheDisk()
    {
        Directory.Delete(_temp.Root);
        var handler = new StubHandler((r, i, ct) => Done(Ok(Payload.Bytes(100))));

        var observer = Run(Downloader(handler), Request());

        Assert.IsFalse(observer.Outcome.Succeeded);
        StringAssert.Contains("ошибка записи на диск", observer.Outcome.Reason);
        Assert.AreEqual(1, handler.Calls, "повтор не починит диск");
        Assert.IsEmpty(_delays);
    }

    [Test]
    public void APartHeldOpenByAnotherInstance_IsALocalFault_AndTheOtherInstancesFileSurvives()
    {
        _temp.Write(PartName, new byte[] { 1, 2, 3 });
        var handler = new StubHandler((r, i, ct) => Done(Ok(Payload.Bytes(100))));

        using (new FileStream(_temp.PathOf(PartName), FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            var observer = Run(Downloader(handler), Request());

            Assert.IsFalse(observer.Outcome.Succeeded);
            StringAssert.Contains("ошибка записи на диск", observer.Outcome.Reason);
        }

        Assert.AreEqual(3, new FileInfo(_temp.PathOf(PartName)).Length, "чужой открытый .part не затирается");
    }

    [Test]
    public void Dispose_CancelsAHangingDownload_AndReportsItOnce()
    {
        var handler = new StubHandler((r, i, ct) => new TaskCompletionSource<HttpResponseMessage>().Task);
        var downloader = Downloader(handler);
        var observer = new RecordingObserver();

        downloader.Download(Request(), observer);
        SpinWait.SpinUntil(() => handler.Calls > 0, TimeSpan.FromSeconds(10));
        downloader.Dispose();
        observer.Await();

        Assert.IsFalse(observer.Outcome.Succeeded);
        StringAssert.Contains("отменена", observer.Outcome.Reason);
        Assert.AreEqual(1, handler.Calls);
        Assert.AreEqual(1, observer.Finished);
    }

    [Test]
    public void Corruption_ServedWithTheRightSize_IsCaughtByTheHashNotByTheDownloader()
    {
        var good = Payload.Bytes(300_000, seed: 21);
        var served = Payload.Corrupted(good);
        var observer = Run(Downloader(new StubHandler((r, i, ct) => Done(Ok(served)))), Request());
        var expectation = new InstallerExpectation(Payload.Sha256(good), good.Length);

        var facts = Sha256FileInspector.Examine(_temp.PathOf(PartName), expectation.NeedsHash);

        Assert.IsTrue(observer.Outcome.Succeeded, "транспорт не знает про хеш: байты дошли, сколько обещано");
        Assert.AreEqual(good.Length, facts.Size);
        Assert.AreEqual(IntegrityVerdict.Mismatch, expectation.Judge(facts),
            "размер сошёлся, содержимое нет — поймать это может только SHA-256");
    }

    [Test]
    public void AnIntactDownload_PassesTheHashCheck()
    {
        var good = Payload.Bytes(300_000, seed: 22);
        Run(Downloader(new StubHandler((r, i, ct) => Done(Ok(good)))), Request());
        var expectation = new InstallerExpectation(Payload.Sha256(good), good.Length);

        var facts = Sha256FileInspector.Examine(_temp.PathOf(PartName), true);

        Assert.AreEqual(IntegrityVerdict.Ok, expectation.Judge(facts));
    }

    [Test]
    public void Constructor_RejectsANonPositiveIdleTimeout()
    {
        var http = new HttpClient(new StubHandler((r, i, ct) => Done(Ok(new byte[1]))));

        Assert.Throws<ArgumentOutOfRangeException>(() => new HttpInstallerDownloader(
            http, _temp.Folder, new InlineMainThread(), new UpdateRetryPolicy(1f), NoPause));
    }
}
