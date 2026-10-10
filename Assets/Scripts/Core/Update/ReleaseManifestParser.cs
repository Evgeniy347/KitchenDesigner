using System;
using System.Collections.Generic;
using UnityEngine;

namespace KitchenDesigner.Core.Update
{
    [Serializable] internal sealed class GhAsset
    {
        public string name = string.Empty;
        public string browser_download_url = string.Empty;
        public long size;
        public string digest = string.Empty;
    }

    [Serializable] internal sealed class GhRelease
    {
        public string tag_name = string.Empty;
        public GhAsset[] assets = Array.Empty<GhAsset>();
    }

    public static class ReleaseManifestParser
    {
        public static ReleaseLookup Parse(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return ReleaseLookup.Failed("Пустой ответ сервера");

            GhRelease release;
            try
            {
                release = JsonUtility.FromJson<GhRelease>(json);
            }
            catch (Exception e)
            {
                return ReleaseLookup.Failed("Некорректный ответ: " + e.Message);
            }

            if (release == null) return ReleaseLookup.NoRelease(UpdateMessages.NoVersionInResponse);
            return ReleaseAssetSelector.Select(release.tag_name, AssetsOf(release));
        }

        public static bool TryParse(string json, out ReleaseManifest? manifest, out string error)
        {
            var lookup = Parse(json);
            manifest = lookup.Manifest;
            error = lookup.Status == ReleaseLookupStatus.Found ? string.Empty : lookup.Reason;
            return lookup.Status == ReleaseLookupStatus.Found;
        }

        private static IReadOnlyList<ReleaseAssetInfo> AssetsOf(GhRelease release)
        {
            var assets = new List<ReleaseAssetInfo>();
            if (release.assets == null) return assets;
            foreach (var asset in release.assets)
            {
                if (asset == null) continue;
                assets.Add(new ReleaseAssetInfo
                {
                    Name = asset.name ?? string.Empty,
                    DownloadUrl = asset.browser_download_url ?? string.Empty,
                    Size = asset.size,
                    Digest = asset.digest ?? string.Empty,
                });
            }
            return assets;
        }
    }
}
