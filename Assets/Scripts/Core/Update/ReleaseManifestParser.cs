using System;
using UnityEngine;

namespace KitchenDesigner.Core.Update
{
    /// <summary>Разобранный «последний релиз» с GitHub, ровно то, что нужно для
    /// решения и скачивания. Не трогает сеть и файловую систему.</summary>
    public sealed class ReleaseManifest
    {
        public string Version = string.Empty;      // «0.700» (без ведущей «v»)
        public string DownloadUrl = string.Empty;  // browser_download_url установщика
        public string FileName = string.Empty;     // «KitchenDesigner-Setup-0.700-x64.exe»
    }

    // ── DTO ответа GitHub Releases API (только нужные поля) ────────────────
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

    /// <summary>
    /// Разбирает JSON эндпоинта «/releases/latest» в ReleaseManifest. Чистая функция
    /// над строкой — тестируется фикстурами без сети. Возвращает false + человекочитаемую
    /// причину ошибки, если JSON битый, нет тега или нет подходящего x64-установщика.
    /// </summary>
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

            // Ищем x64-установщик: в имени есть «Setup», «x64», и оно кончается на «.exe».
            // GitHub отдаёт ассеты в своём порядке — берём первый подходящий, но
            // предпочитаем тот, у кого имя совпадает с тегом (защита от leftover-артефактов).
            GhAsset? best = null;
            if (release.assets != null)
            {
                foreach (var a in release.assets)
                {
                    if (a == null || string.IsNullOrEmpty(a.name) ||
                        string.IsNullOrEmpty(a.browser_download_url)) continue;
                    if (!IsSetupAsset(a.name)) continue;
                    if (best == null) best = a;
                    else if (a.name.IndexOf(release.tag_name.TrimStart('v'), StringComparison.Ordinal) >= 0)
                        best = a; // совпадает по версии — надёжнее
                }
            }

            if (best == null)
            {
                error = "В релизе нет установщика x64";
                return false;
            }

            manifest = new ReleaseManifest
            {
                Version = release.tag_name.TrimStart('v', 'V'),
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
    }
}
