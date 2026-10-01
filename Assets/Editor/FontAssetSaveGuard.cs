using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace KitchenDesigner.Editor
{
    public class FontAssetSaveGuard : AssetModificationProcessor
    {
        public static bool AllowSave { get; set; }

        public static string[] Filter(IEnumerable<string> paths, bool allowSave) =>
            allowSave
                ? paths.ToArray()
                : paths.Where(path => path != CreateTMPFontFromLiberation.AssetPath).ToArray();

        private static string[] OnWillSaveAssets(string[] paths)
        {
            var kept = Filter(paths, AllowSave);
            if (kept.Length != paths.Length && !Application.isBatchMode)
                Debug.Log("[FontAssetSaveGuard] " + CreateTMPFontFromLiberation.AssetPath
                    + " не сохранён: динамический TMP-шрифт дописывает в него кернинг при каждом запуске. "
                    + "Чтобы пересоздать — Tools/Kitchen/Create TMP Font From LiberationSans.");
            return kept;
        }
    }
}
