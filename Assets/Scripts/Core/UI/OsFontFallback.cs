using System;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace KitchenDesigner.Core.UI
{
    public static class OsFontFallback
    {
        private const int SamplingPointSize = 48;
        private const int AtlasPadding = 5;
        private const int AtlasSize = 2048;

        private static readonly Dictionary<string, TMP_FontAsset> Loaded = new();
        private static readonly List<TMP_FontAsset> Attached = new();
        private static TMP_FontAsset? _host;
        private static bool _quitHooked;

        public static IReadOnlyList<TMP_FontAsset> AttachedFonts => Attached;

        public static void ApplyFor(string language, TMP_FontAsset? main)
        {
            Detach();
            if (main == null) return;
            var script = ScriptFallbackFonts.For(language);
            if (script == ScriptFallbackFonts.Script.None) return;

            var paths = ScriptFallbackFonts.Resolve(language,
                ScriptFallbackFonts.WindowsFontDirectories(Environment.GetEnvironmentVariable), File.Exists);
            main.fallbackFontAssetTable ??= new List<TMP_FontAsset>();
            foreach (var path in paths)
            {
                var font = Load(path);
                if (font == null) continue;
                main.fallbackFontAssetTable.Add(font);
                Attached.Add(font);
            }
            _host = main;
            HookQuit();

            if (Attached.Count == 0)
                Debug.LogWarning("[OsFontFallback] no system font for " + script + " (" + language + "); looked for "
                    + string.Join(", ", ScriptFallbackFonts.CandidateFiles(script))
                    + " in the Windows font folders. Text in this script will render as empty boxes.");
        }

        public static TMP_FontAsset? FontFor(string? language)
        {
            if (ScriptFallbackFonts.For(language) == ScriptFallbackFonts.Script.None) return null;
            var paths = ScriptFallbackFonts.Resolve(language,
                ScriptFallbackFonts.WindowsFontDirectories(Environment.GetEnvironmentVariable), File.Exists);
            foreach (var path in paths)
            {
                var font = Load(path);
                if (font != null) return font;
            }
            return null;
        }

        private static void Detach()
        {
            if (_host != null && _host.fallbackFontAssetTable != null)
                foreach (var font in Attached) _host.fallbackFontAssetTable.Remove(font);
            Attached.Clear();
            _host = null;
        }

        private static TMP_FontAsset? Load(string path)
        {
            if (Loaded.TryGetValue(path, out var cached) && cached != null) return cached;
            TMP_FontAsset? font = null;
            try
            {
                font = TMP_FontAsset.CreateFontAsset(path, 0, SamplingPointSize, AtlasPadding,
                    GlyphRenderMode.SDFAA, AtlasSize, AtlasSize);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[OsFontFallback] cannot load " + path + ": " + e.Message);
            }
            if (font == null)
            {
                Debug.LogWarning("[OsFontFallback] cannot load " + path + " as a font asset");
                return null;
            }
            font.name = "OS fallback " + Path.GetFileName(path);
            Loaded[path] = font;
            return font;
        }

        private static void HookQuit()
        {
            if (_quitHooked) return;
            _quitHooked = true;
            Application.quitting += Detach;
        }
    }
}
