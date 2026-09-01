using System;
using UnityEngine;

namespace KitchenDesigner.Core.Update
{
    [Serializable] internal sealed class GhAsset
    {
        public string name = string.Empty;
        public string browser_download_url = string.Empty;
    }

    [Serializable] internal sealed class GhRelease
    {
        public string tag_name = string.Empty;
        public GhAsset[] assets = Array.Empty<GhAsset>();
    }

    public static class ReleaseManifestParser
    {
        public static bool TryParse(string json, out ReleaseManifest? manifest, out string error)
        {
            manifest = null;
            error = string.Empty;

            if (string.IsNullOrWhiteSpace(json))
            {
                error = "Пустой ответ сервера";
                return false;
            }

            GhRelease release;
            try
            {
                release = JsonUtility.FromJson<GhRelease>(json);
            }
            catch (Exception e)
            {
                error = "Некорректный ответ: " + e.Message;
                return false;
            }

            if (release == null || string.IsNullOrEmpty(release.tag_name))
            {
                error = "В ответе нет номера версии";
                return false;
            }

            if (!VersionUtil.TryParse(release.tag_name, out _, out _, out _))
            {
                error = "Не удалось разобрать номер версии: " + release.tag_name;
                return false;
            }

            string version = release.tag_name.TrimStart('v', 'V');

            GhAsset? best = null;
            if (release.assets != null)
            {
                foreach (var a in release.assets)
                {
                    if (a == null || string.IsNullOrEmpty(a.name) ||
                        string.IsNullOrEmpty(a.browser_download_url)) continue;
                    if (!IsSetupAsset(a.name)) continue;
                    if (!CarriesVersion(a.name, version)) continue;
                    best = a;
                    break;
                }
            }

            if (best == null)
            {
                error = "В релизе нет установщика x64 для версии " + version;
                return false;
            }

            manifest = new ReleaseManifest
            {
                Version = version,
                DownloadUrl = best.browser_download_url,
                FileName = best.name,
            };
            return true;
        }

        private static bool IsSetupAsset(string name)
        {
            return name.IndexOf("Setup", StringComparison.OrdinalIgnoreCase) >= 0
                && name.IndexOf("x64", StringComparison.OrdinalIgnoreCase) >= 0
                && name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase);
        }

        private static bool CarriesVersion(string name, string version)
        {
            return name.IndexOf("-" + version + "-", StringComparison.Ordinal) >= 0;
        }
    }
}
