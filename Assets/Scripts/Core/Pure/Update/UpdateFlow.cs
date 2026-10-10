using System;
using System.Collections.Generic;

namespace KitchenDesigner.Core.Update
{
    public sealed class UpdateFlow
    {
        private readonly UpdatePlanner _planner;
        private readonly IReleaseSource _releases;
        private readonly IUpdateFolder _folder;
        private readonly IFileInspector _inspector;
        private readonly IPartDownloader _downloader;
        private readonly IUpdateConsole _console;
        private readonly IUpdateDialog _dialog;
        private readonly IUpdateApplier _applier;
        private readonly Func<float> _clockSeconds;

        public UpdateFlow(
            UpdatePlanner planner,
            IReleaseSource releases,
            IUpdateFolder folder,
            IFileInspector inspector,
            IPartDownloader downloader,
            IUpdateConsole console,
            IUpdateDialog dialog,
            IUpdateApplier applier,
            Func<float> clockSeconds)
        {
            _planner = planner ?? throw new ArgumentNullException(nameof(planner));
            _releases = releases ?? throw new ArgumentNullException(nameof(releases));
            _folder = folder ?? throw new ArgumentNullException(nameof(folder));
            _inspector = inspector ?? throw new ArgumentNullException(nameof(inspector));
            _downloader = downloader ?? throw new ArgumentNullException(nameof(downloader));
            _console = console ?? throw new ArgumentNullException(nameof(console));
            _dialog = dialog ?? throw new ArgumentNullException(nameof(dialog));
            _applier = applier ?? throw new ArgumentNullException(nameof(applier));
            _clockSeconds = clockSeconds ?? throw new ArgumentNullException(nameof(clockSeconds));
        }

        public UpdatePhase Phase => _planner.Phase;

        public void Start()
        {
            Run(() =>
            {
                Execute(_planner.Begin());
                _releases.Fetch(OnReleaseFetched);
            });
        }

        private void OnReleaseFetched(ReleaseLookup lookup) =>
            Run(() =>
            {
                IReadOnlyList<FolderEntry> listing;
                try
                {
                    listing = _folder.List();
                }
                catch (Exception e)
                {
                    listing = Array.Empty<FolderEntry>();
                    lookup = ReleaseLookup.Failed(UpdateMessages.FolderUnreadable(e.Message));
                }
                Execute(_planner.OnReleaseResolved(lookup, listing));
            });

        private void Run(Action step)
        {
            try
            {
                step();
            }
            catch (Exception e)
            {
                _console.Write(UpdateLogLevel.Error, UpdateMessages.UnexpectedFailure(e.Message));
            }
        }

        private void Execute(IReadOnlyList<UpdateAction> actions)
        {
            foreach (var action in actions) Execute(action);
        }

        private void Execute(UpdateAction action)
        {
            switch (action.Kind)
            {
                case UpdateActionKind.Log:
                    _console.Write(action.Level, action.Message);
                    break;
                case UpdateActionKind.Delete:
                    Delete(action.FileName);
                    break;
                case UpdateActionKind.Cleanup:
                    foreach (var file in action.Files) Delete(file);
                    break;
                case UpdateActionKind.VerifyExisting:
                    Inspect(action, _planner.OnExistingVerified);
                    break;
                case UpdateActionKind.VerifyDownloaded:
                    Inspect(action, _planner.OnDownloadVerified);
                    break;
                case UpdateActionKind.VerifyBeforeApply:
                    Inspect(action, _planner.OnApplyVerified);
                    break;
                case UpdateActionKind.Download:
                    StartDownload(action);
                    break;
                case UpdateActionKind.Promote:
                    Promote(action);
                    break;
                case UpdateActionKind.ShowDialog:
                    ShowDialog(action);
                    break;
                case UpdateActionKind.Apply:
                    _applier.ApplyAndRelaunch(_folder.PathOf(action.FileName));
                    break;
            }
        }

        private void Delete(string name)
        {
            if (!_folder.TryDelete(name, out var error))
                _console.Write(UpdateLogLevel.Warning, UpdateMessages.DeleteFailed(name, error));
        }

        private void Inspect(UpdateAction action, Func<FileFacts, IReadOnlyList<UpdateAction>> next) =>
            _inspector.Inspect(action.FileName, action.Expectation.NeedsHash,
                facts => Run(() => Execute(next(facts))));

        private void Promote(UpdateAction action)
        {
            bool ok = _folder.TryPromote(action.PartName, action.FileName, out var error);
            Execute(_planner.OnPromoted(ok, error));
        }

        private void ShowDialog(UpdateAction action)
        {
            _dialog.ShowUpdateAvailable(action.Version,
                () => Run(() => Answer(true)),
                () => Run(() => Answer(false)));
        }

        private void Answer(bool accepted)
        {
            _dialog.Hide();
            Execute(_planner.OnUserChoice(accepted));
        }

        private void StartDownload(UpdateAction action)
        {
            _console.Write(UpdateLogLevel.Info, UpdateMessages.DownloadStarted(action.Version, action.Attempt));
            try
            {
                _folder.EnsureExists();
            }
            catch (Exception e)
            {
                _console.Write(UpdateLogLevel.Error, UpdateMessages.DownloadFolderFailed(e.Message));
                Execute(_planner.OnDownloadFinished(DownloadOutcome.Failure(e.Message)));
                return;
            }

            var observer = new ProgressObserver(this, action.Expectation.Size, _clockSeconds());
            _downloader.Download(new DownloadRequest(action.Url, action.PartName, action.Expectation.Size), observer);
        }

        private sealed class ProgressObserver : IDownloadObserver
        {
            private readonly UpdateFlow _owner;
            private readonly long _expectedSize;
            private readonly DownloadProgressThrottle _throttle;

            public ProgressObserver(UpdateFlow owner, long expectedSize, float startedAt)
            {
                _owner = owner;
                _expectedSize = expectedSize;
                _throttle = new DownloadProgressThrottle(startedAt);
            }

            public void OnProgress(long received, long total)
            {
                long known = total > 0 ? total : _expectedSize;
                if (!_throttle.ShouldReport(received, known, _owner._clockSeconds())) return;
                _owner._console.Write(UpdateLogLevel.Info, UpdateMessages.Progress(received, known));
            }

            public void OnRetry(int attempt, int attempts, string reason) =>
                _owner._console.Write(UpdateLogLevel.Warning, UpdateMessages.DownloadRetry(attempt, attempts, reason));

            public void OnFinished(DownloadOutcome outcome) =>
                _owner.Run(() => _owner.Execute(_owner._planner.OnDownloadFinished(outcome)));
        }
    }
}
