namespace KitchenDesigner.Core.Update
{
    public enum ReleaseLookupStatus { Found, NoInstallerAsset, NoRelease, Failed }

    public sealed class ReleaseLookup
    {
        private ReleaseLookup(ReleaseLookupStatus status, string version, ReleaseManifest? manifest, string reason)
        {
            Status = status;
            Version = version;
            Manifest = manifest;
            Reason = reason;
        }

        public ReleaseLookupStatus Status { get; }
        public string Version { get; }
        public ReleaseManifest? Manifest { get; }
        public string Reason { get; }

        public static ReleaseLookup Found(ReleaseManifest manifest) =>
            new ReleaseLookup(ReleaseLookupStatus.Found, manifest.Version, manifest, string.Empty);

        public static ReleaseLookup NoInstallerAsset(string version, string reason) =>
            new ReleaseLookup(ReleaseLookupStatus.NoInstallerAsset, version, null, reason);

        public static ReleaseLookup NoRelease(string reason) =>
            new ReleaseLookup(ReleaseLookupStatus.NoRelease, string.Empty, null, reason);

        public static ReleaseLookup Failed(string reason) =>
            new ReleaseLookup(ReleaseLookupStatus.Failed, string.Empty, null, reason);
    }
}
