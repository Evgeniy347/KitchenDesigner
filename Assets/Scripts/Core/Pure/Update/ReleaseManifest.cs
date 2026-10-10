namespace KitchenDesigner.Core.Update
{
    public sealed class ReleaseManifest
    {
        public string Version = string.Empty;
        public string DownloadUrl = string.Empty;
        public string FileName = string.Empty;
        public long Size;
        public string Sha256 = string.Empty;
    }
}
