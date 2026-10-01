using System;
using System.Collections.Generic;
using System.IO;

namespace KitchenDesigner.Core
{
    public static class ScriptFallbackFonts
    {
        public enum Script { None, Arabic, SimplifiedChinese, Japanese }

        public const int MaxAttached = 2;

        private static readonly IReadOnlyList<string> NoFiles = Array.Empty<string>();
        private static readonly IReadOnlyList<string> ArabicFiles = new[] { "segoeui.ttf", "tahoma.ttf", "arial.ttf" };
        private static readonly IReadOnlyList<string> ChineseFiles = new[] { "msyh.ttc", "simsun.ttc", "msyhl.ttc" };
        private static readonly IReadOnlyList<string> JapaneseFiles = new[] { "YuGothM.ttc", "meiryo.ttc", "YuGothR.ttc", "msgothic.ttc", "msyh.ttc" };

        public static Script For(string? language)
        {
            switch (LanguageChoice.Normalize(language))
            {
                case "ar":
                case "fa":
                case "ur":
                    return Script.Arabic;
                case "zh":
                    return Script.SimplifiedChinese;
                case "ja":
                    return Script.Japanese;
                default:
                    return Script.None;
            }
        }

        public static IReadOnlyList<string> CandidateFiles(Script script) => script switch
        {
            Script.Arabic => ArabicFiles,
            Script.SimplifiedChinese => ChineseFiles,
            Script.Japanese => JapaneseFiles,
            _ => NoFiles,
        };

        public static IReadOnlyList<string> Resolve(string? language, IEnumerable<string?> fontDirectories, Func<string, bool> exists)
        {
            var found = new List<string>();
            var files = CandidateFiles(For(language));
            if (files.Count == 0) return found;
            var directories = new List<string>();
            foreach (var directory in fontDirectories)
                if (!string.IsNullOrEmpty(directory)) directories.Add(directory!);

            foreach (var file in files)
            {
                foreach (var directory in directories)
                {
                    var path = Path.Combine(directory, file);
                    if (!exists(path)) continue;
                    found.Add(path);
                    break;
                }
                if (found.Count == MaxAttached) break;
            }
            return found;
        }

        public static IEnumerable<string?> WindowsFontDirectories(Func<string, string?> environment)
        {
            var windows = environment("WINDIR");
            yield return string.IsNullOrEmpty(windows) ? null : Path.Combine(windows!, "Fonts");
            var local = environment("LOCALAPPDATA");
            yield return string.IsNullOrEmpty(local) ? null : Path.Combine(local!, "Microsoft", "Windows", "Fonts");
        }
    }
}
