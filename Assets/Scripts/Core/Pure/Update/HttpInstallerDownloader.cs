using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace KitchenDesigner.Core.Update
{
    public sealed class HttpInstallerDownloader : IPartDownloader, IDisposable
    {
        public const int BufferSize = 81920;
        public const string UserAgent = "KitchenDesigner-Updater";

        private readonly HttpClient _http;
        private readonly IUpdateFolder _folder;
        private readonly IMainThread _mainThread;
        private readonly UpdateRetryPolicy _policy;
        private readonly TimeSpan _idle;
        private readonly Func<TimeSpan, CancellationToken, Task> _delay;
        private readonly CancellationTokenSource _lifetime = new CancellationTokenSource();

        public HttpInstallerDownloader(
            HttpClient http,
            IUpdateFolder folder,
            IMainThread mainThread,
            UpdateRetryPolicy policy,
            TimeSpan idleTimeout,
            Func<TimeSpan, CancellationToken, Task>? delay = null)
        {
            _http = http ?? throw new ArgumentNullException(nameof(http));
            _folder = folder ?? throw new ArgumentNullException(nameof(folder));
            _mainThread = mainThread ?? throw new ArgumentNullException(nameof(mainThread));
            _policy = policy ?? throw new ArgumentNullException(nameof(policy));
            if (idleTimeout <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(idleTimeout));
            _idle = idleTimeout;
            _delay = delay ?? Task.Delay;
        }

        public void Download(DownloadRequest request, IDownloadObserver observer)
        {
            Task.Run(() => RunAsync(request, observer));
        }

        public void Dispose() => _lifetime.Cancel();

        private async Task RunAsync(DownloadRequest request, IDownloadObserver observer)
        {
            var outcome = new UpdateAttemptOutcome();
            var partPath = _folder.PathOf(request.PartName);
            try
            {
                for (int attempt = 1; attempt <= _policy.MaxAttempts; attempt++)
                {
                    if (attempt > 1)
                    {
                        DeleteQuietly(partPath);
                        await _delay(TimeSpan.FromSeconds(_policy.PauseBeforeAttemptSeconds(attempt)), _lifetime.Token)
                            .ConfigureAwait(false);
                        PostRetry(observer, attempt, outcome.Reason);
                    }

                    await AttemptAsync(request, partPath, observer, outcome).ConfigureAwait(false);

                    if (outcome.Succeeded)
                    {
                        Finish(observer, DownloadOutcome.Success());
                        return;
                    }
                    if (!_policy.ShouldRetryAfter(attempt, outcome.Failure)) break;
                }
            }
            catch (OperationCanceledException)
            {
                outcome.Fail(UpdateMessages.DownloadCancelled, UpdateAttemptFailure.Cancelled());
            }

            DeleteQuietly(partPath);
            Finish(observer, DownloadOutcome.Failure(outcome.Reason));
        }

        private async Task AttemptAsync(
            DownloadRequest request, string partPath, IDownloadObserver observer, UpdateAttemptOutcome outcome)
        {
            try
            {
                using var message = new HttpRequestMessage(HttpMethod.Get, request.Url);
                message.Headers.UserAgent.ParseAdd(UserAgent);
                using var response = await WithIdleTimeoutAsync(
                    token => _http.SendAsync(message, HttpCompletionOption.ResponseHeadersRead, token))
                    .ConfigureAwait(false);

                if (!response.IsSuccessStatusCode)
                {
                    outcome.Fail(UpdateMessages.HttpStatus((int)response.StatusCode),
                        UpdateAttemptFailure.FromResponse((long)response.StatusCode));
                    return;
                }

                long? declared = response.Content.Headers.ContentLength;
                long total = declared ?? request.ExpectedSize;
                using var source = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
                long received = await CopyAsync(source, partPath, total, observer).ConfigureAwait(false);

                if (declared.HasValue && received != declared.Value)
                {
                    outcome.Fail(UpdateMessages.Truncated(received, declared.Value), UpdateAttemptFailure.NoAnswer());
                    return;
                }
                outcome.Succeed();
            }
            catch (LocalFileFault e)
            {
                outcome.Fail(UpdateMessages.DiskFault(e.Message), UpdateAttemptFailure.LocalFault());
            }
            catch (OperationCanceledException) when (_lifetime.IsCancellationRequested)
            {
                outcome.Fail(UpdateMessages.DownloadCancelled, UpdateAttemptFailure.Cancelled());
            }
            catch (Exception e) when (e is IdleTimeoutFault || e is OperationCanceledException)
            {
                outcome.Fail(UpdateMessages.Stalled(_idle.TotalSeconds), UpdateAttemptFailure.NoAnswer());
            }
            catch (Exception e) when (e is HttpRequestException || e is IOException)
            {
                outcome.Fail(UpdateMessages.NoConnection(e.Message), UpdateAttemptFailure.NoAnswer());
            }
        }

        private async Task<T> WithIdleTimeoutAsync<T>(Func<CancellationToken, Task<T>> start)
        {
            using var guard = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token);
            var work = start(guard.Token);
            var timer = Task.Delay(_idle, guard.Token);
            var first = await Task.WhenAny(work, timer).ConfigureAwait(false);
            guard.Cancel();
            if (first == work) return await work.ConfigureAwait(false);

            _ = work.ContinueWith(t => t.Exception, TaskContinuationOptions.OnlyOnFaulted);
            _lifetime.Token.ThrowIfCancellationRequested();
            throw new IdleTimeoutFault();
        }

        private async Task<long> CopyAsync(Stream source, string partPath, long total, IDownloadObserver observer)
        {
            using var target = OpenPart(partPath);
            var buffer = new byte[BufferSize];
            long received = 0;
            while (true)
            {
                int read = await WithIdleTimeoutAsync(token => source.ReadAsync(buffer, 0, buffer.Length, token))
                    .ConfigureAwait(false);
                if (read == 0) break;

                WritePart(target, buffer, read);
                received += read;
                PostProgress(observer, received, total);
            }
            FlushPart(target);
            return received;
        }

        private static FileStream OpenPart(string partPath)
        {
            try
            {
                return new FileStream(partPath, FileMode.Create, FileAccess.Write, FileShare.None, BufferSize);
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                throw new LocalFileFault(e);
            }
        }

        private static void WritePart(FileStream target, byte[] buffer, int count)
        {
            try
            {
                target.Write(buffer, 0, count);
            }
            catch (IOException e)
            {
                throw new LocalFileFault(e);
            }
        }

        private static void FlushPart(FileStream target)
        {
            try
            {
                target.Flush(true);
            }
            catch (IOException e)
            {
                throw new LocalFileFault(e);
            }
        }

        private void PostProgress(IDownloadObserver observer, long received, long total) =>
            _mainThread.Post(() => observer.OnProgress(received, total));

        private void PostRetry(IDownloadObserver observer, int attempt, string reason)
        {
            int attempts = _policy.MaxAttempts;
            _mainThread.Post(() => observer.OnRetry(attempt, attempts, reason));
        }

        private void Finish(IDownloadObserver observer, DownloadOutcome outcome) =>
            _mainThread.Post(() => observer.OnFinished(outcome));

        private static void DeleteQuietly(string path)
        {
            try
            {
                if (File.Exists(path)) File.Delete(path);
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
            }
        }

        private sealed class IdleTimeoutFault : Exception
        {
        }

        private sealed class LocalFileFault : Exception
        {
            public LocalFileFault(Exception inner) : base(inner.Message, inner)
            {
            }
        }
    }
}
