using System.Collections.Generic;

namespace KitchenDesigner.Core.Update
{
    public enum UpdatePhase
    {
        Idle,
        AwaitingRelease,
        VerifyingExisting,
        Downloading,
        VerifyingDownload,
        Promoting,
        AwaitingUser,
        VerifyingBeforeApply,
        Finished,
    }

    public sealed class UpdatePlanner
    {
        public const int MaxDownloadAttempts = 2;

        private readonly string _currentVersion;
        private ReleaseManifest? _target;
        private InstallerExpectation _expectation;
        private int _attempt;

        public UpdatePlanner(string currentVersion)
        {
            _currentVersion = currentVersion ?? string.Empty;
        }

        public UpdatePhase Phase { get; private set; } = UpdatePhase.Idle;

        public IReadOnlyList<UpdateAction> Begin()
        {
            if (Phase != UpdatePhase.Idle) return Unexpected(nameof(Begin));
            Phase = UpdatePhase.AwaitingRelease;
            return NoActions();
        }

        public IReadOnlyList<UpdateAction> OnReleaseResolved(ReleaseLookup lookup, IReadOnlyList<FolderEntry> folder)
        {
            if (Phase != UpdatePhase.AwaitingRelease) return Unexpected(nameof(OnReleaseResolved));

            if (!VersionUtil.TryParse(_currentVersion, out _, out _, out _))
                return Finish(Log(UpdateLogLevel.Error, UpdateMessages.UnreadableCurrentVersion(_currentVersion)));

            switch (lookup.Status)
            {
                case ReleaseLookupStatus.Failed:
                    return Finish(Log(UpdateLogLevel.Error, UpdateMessages.CheckFailed(lookup.Reason)));
                case ReleaseLookupStatus.NoRelease:
                    return Finish(Log(UpdateLogLevel.Warning, UpdateMessages.NoRelease(lookup.Reason)));
            }

            if (!InstallerFileName.IsValidVersion(lookup.Version))
                return Finish(Log(UpdateLogLevel.Error, UpdateMessages.UnreadableReleaseVersion(lookup.Version)));

            return VersionUtil.Compare(lookup.Version, _currentVersion) > 0
                ? PlanForNewerRelease(lookup, folder)
                : PlanForCurrentRelease(lookup, folder);
        }

        public IReadOnlyList<UpdateAction> OnExistingVerified(FileFacts facts)
        {
            if (Phase != UpdatePhase.VerifyingExisting) return Unexpected(nameof(OnExistingVerified));

            var file = FinalName();
            var verdict = _expectation.Judge(facts);
            if (verdict == IntegrityVerdict.Ok)
            {
                Phase = UpdatePhase.AwaitingUser;
                return Actions(
                    Log(UpdateLogLevel.Info, UpdateMessages.ExistingVerified(file, _expectation.Method)),
                    UpdateAction.ShowDialog(_target!.Version, file));
            }

            if (verdict == IntegrityVerdict.Missing)
                return Actions(
                    Log(UpdateLogLevel.Warning, UpdateMessages.ExistingMissing(file)),
                    StartDownload());

            return Actions(
                Log(UpdateLogLevel.Warning, UpdateMessages.ExistingBroken(file, _expectation.Method)),
                UpdateAction.Delete(file),
                StartDownload());
        }

        public IReadOnlyList<UpdateAction> OnDownloadFinished(DownloadOutcome outcome)
        {
            if (Phase != UpdatePhase.Downloading) return Unexpected(nameof(OnDownloadFinished));

            var part = PartName();
            if (!outcome.Succeeded)
                return Finish(
                    Log(UpdateLogLevel.Error, UpdateMessages.DownloadFailed(outcome.Reason)),
                    UpdateAction.Delete(part));

            Phase = UpdatePhase.VerifyingDownload;
            return Actions(
                Log(UpdateLogLevel.Info, UpdateMessages.DownloadFinished(FinalName())),
                UpdateAction.Verify(UpdateActionKind.VerifyDownloaded, part, _expectation));
        }

        public IReadOnlyList<UpdateAction> OnDownloadVerified(FileFacts facts)
        {
            if (Phase != UpdatePhase.VerifyingDownload) return Unexpected(nameof(OnDownloadVerified));

            var part = PartName();
            if (_expectation.Judge(facts) == IntegrityVerdict.Ok)
            {
                Phase = UpdatePhase.Promoting;
                return Actions(
                    Log(UpdateLogLevel.Info, UpdateMessages.DownloadedVerified(_expectation.Method)),
                    UpdateAction.Promote(part, FinalName()));
            }

            if (_attempt >= MaxDownloadAttempts)
                return Finish(
                    UpdateAction.Delete(part),
                    Log(UpdateLogLevel.Error, UpdateMessages.DownloadMismatchTwice(_expectation.Method)));

            return Actions(
                UpdateAction.Delete(part),
                Log(UpdateLogLevel.Warning, UpdateMessages.DownloadMismatchRetry(_expectation.Method)),
                StartDownload());
        }

        public IReadOnlyList<UpdateAction> OnPromoted(bool succeeded, string reason)
        {
            if (Phase != UpdatePhase.Promoting) return Unexpected(nameof(OnPromoted));

            if (!succeeded)
                return Finish(
                    Log(UpdateLogLevel.Error, UpdateMessages.PromoteFailed(reason)),
                    UpdateAction.Delete(PartName()));

            Phase = UpdatePhase.AwaitingUser;
            return Actions(
                Log(UpdateLogLevel.Info, UpdateMessages.Ready(_target!.Version)),
                UpdateAction.ShowDialog(_target.Version, FinalName()));
        }

        public IReadOnlyList<UpdateAction> OnUserChoice(bool accepted)
        {
            if (Phase != UpdatePhase.AwaitingUser) return Unexpected(nameof(OnUserChoice));

            var file = FinalName();
            if (!accepted)
                return Finish(Log(UpdateLogLevel.Info, UpdateMessages.UserDeclined(file)));

            Phase = UpdatePhase.VerifyingBeforeApply;
            return Actions(
                Log(UpdateLogLevel.Info, UpdateMessages.UserAccepted(file)),
                UpdateAction.Verify(UpdateActionKind.VerifyBeforeApply, file, _expectation));
        }

        public IReadOnlyList<UpdateAction> OnApplyVerified(FileFacts facts)
        {
            if (Phase != UpdatePhase.VerifyingBeforeApply) return Unexpected(nameof(OnApplyVerified));

            var file = FinalName();
            var verdict = _expectation.Judge(facts);
            if (verdict == IntegrityVerdict.Ok)
                return Finish(
                    Log(UpdateLogLevel.Info, UpdateMessages.Starting(file)),
                    UpdateAction.Apply(file));

            if (verdict == IntegrityVerdict.Missing)
                return Finish(Log(UpdateLogLevel.Error, UpdateMessages.ChangedBeforeApply(file)));

            return Finish(
                Log(UpdateLogLevel.Error, UpdateMessages.ChangedBeforeApply(file)),
                UpdateAction.Delete(file));
        }

        private IReadOnlyList<UpdateAction> PlanForCurrentRelease(
            ReleaseLookup lookup, IReadOnlyList<FolderEntry> folder)
        {
            var actions = new List<UpdateAction>
            {
                Log(UpdateLogLevel.Info, UpdateMessages.UpToDate(_currentVersion, lookup.Version)),
            };
            AddCleanup(actions, StaleInstallerSelector.Select(folder, _currentVersion, null));
            Phase = UpdatePhase.Finished;
            return actions;
        }

        private IReadOnlyList<UpdateAction> PlanForNewerRelease(
            ReleaseLookup lookup, IReadOnlyList<FolderEntry> folder)
        {
            var version = lookup.Version;
            var announce = Log(UpdateLogLevel.Info, UpdateMessages.NewVersionFound(version));

            var manifest = lookup.Manifest;
            if (manifest == null)
                return Finish(announce, Log(UpdateLogLevel.Error, UpdateMessages.NoInstaller(version)));

            var expectedName = InstallerFileName.For(version);
            if (manifest.FileName != expectedName)
                return Finish(announce,
                    Log(UpdateLogLevel.Error, UpdateMessages.ForeignInstaller(manifest.FileName, expectedName)));

            if (string.IsNullOrWhiteSpace(manifest.DownloadUrl))
                return Finish(announce, Log(UpdateLogLevel.Error, UpdateMessages.NoDownloadUrl(version)));

            var expectation = new InstallerExpectation(manifest.Sha256, manifest.Size);
            if (expectation.Method == IntegrityMethod.None)
                return Finish(announce, Log(UpdateLogLevel.Error, UpdateMessages.CannotVerify(version)));

            _target = manifest;
            _expectation = expectation;
            _attempt = 0;

            var actions = new List<UpdateAction> { announce };
            if (expectation.Method == IntegrityMethod.Size)
                actions.Add(Log(UpdateLogLevel.Warning, UpdateMessages.SizeOnlyCheck(expectation.Size)));

            AddCleanup(actions, StaleInstallerSelector.Select(folder, _currentVersion, version));

            if (ContainsFile(folder, expectedName))
            {
                Phase = UpdatePhase.VerifyingExisting;
                actions.Add(UpdateAction.Verify(UpdateActionKind.VerifyExisting, expectedName, expectation));
            }
            else
            {
                actions.Add(StartDownload());
            }
            return actions;
        }

        private UpdateAction StartDownload()
        {
            _attempt++;
            Phase = UpdatePhase.Downloading;
            return UpdateAction.Download(
                _target!.Version, _target.DownloadUrl, PartName(), _expectation, _attempt);
        }

        private string FinalName() => InstallerFileName.For(_target!.Version);

        private string PartName() => InstallerFileName.PartFor(_target!.Version);

        private static void AddCleanup(List<UpdateAction> actions, IReadOnlyList<string> stale)
        {
            if (stale.Count == 0) return;
            actions.Add(Log(UpdateLogLevel.Info, UpdateMessages.Cleaning(stale)));
            actions.Add(UpdateAction.Cleanup(stale));
        }

        private static bool ContainsFile(IReadOnlyList<FolderEntry> folder, string name)
        {
            foreach (var entry in folder)
                if (entry.Name == name) return true;
            return false;
        }

        private static UpdateAction Log(UpdateLogLevel level, string message) =>
            UpdateAction.Log(level, message);

        private IReadOnlyList<UpdateAction> Finish(params UpdateAction[] actions)
        {
            Phase = UpdatePhase.Finished;
            return actions;
        }

        private static IReadOnlyList<UpdateAction> Actions(params UpdateAction[] actions) => actions;

        private static IReadOnlyList<UpdateAction> NoActions() => new UpdateAction[0];

        private IReadOnlyList<UpdateAction> Unexpected(string eventName) =>
            Actions(Log(UpdateLogLevel.Warning, UpdateMessages.UnexpectedEvent(eventName, Phase)));
    }
}
