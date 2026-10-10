using System;
using System.Collections.Generic;

namespace KitchenDesigner.Core.Update
{
    public sealed class ReleaseAssetInfo
    {
        public string Name = string.Empty;
        public string DownloadUrl = string.Empty;
        public long Size;
        public string Digest = string.Empty;
    }

    public static class ReleaseAssetSelector
    {
        public static ReleaseLookup Select(string? tagName, IReadOnlyList<ReleaseAssetInfo>? assets)
        {
            if (string.IsNullOrEmpty(tagName))
                return ReleaseLookup.NoRelease(UpdateMessages.NoVersionInResponse);

            if (!VersionUtil.TryParse(tagName, out _, out _, out _))
                return ReleaseLookup.Failed(UpdateMessages.UnreadableTag(tagName!));

            string version = tagName!.TrimStart('v', 'V');

            if (assets != null)
            {
                foreach (var asset in assets)
                {
                    if (asset == null || string.IsNullOrEmpty(asset.Name)
                        || string.IsNullOrEmpty(asset.DownloadUrl)) continue;
                    if (!IsSetupAsset(asset.Name)) continue;
                    if (!CarriesVersion(asset.Name, version)) continue;

                    return ReleaseLookup.Found(new ReleaseManifest
                    {
                        Version = version,
                        DownloadUrl = asset.DownloadUrl,
                        FileName = asset.Name,
                        Size = asset.Size,
                        Sha256 = Sha256Digest.FromGitHubDigest(asset.Digest),
                    });
                }
            }

            return ReleaseLookup.NoInstallerAsset(version, UpdateMessages.NoInstallerForVersion(version));
        }

        private static bool IsSetupAsset(string name) =>
            name.IndexOf("Setup", StringComparison.OrdinalIgnoreCase) >= 0
            && name.IndexOf("x64", StringComparison.OrdinalIgnoreCase) >= 0
            && name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase);

        private static bool CarriesVersion(string name, string version) =>
            name.IndexOf("-" + version + "-", StringComparison.Ordinal) >= 0;
    }
}
