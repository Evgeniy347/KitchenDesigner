using TMPro;
using UnityEditor;
using UnityEngine;

namespace KitchenDesigner.Editor
{
    public static class CreateTMPFontFromArial
    {
        [MenuItem("Tools/Kitchen/Create TMP Font From Arial")]
        public static void Create()
        {
            var unityFont = AssetDatabase.LoadAssetAtPath<Font>("Assets/Resources/Fonts/arial.ttf");
            if (unityFont == null)
            {
                Debug.LogError("[TMP] arial.ttf not found at Assets/Resources/Fonts/arial.ttf");
                return;
            }

            var fontAsset = TMP_FontAsset.CreateFontAsset(
                unityFont,
                samplingPointSize: 90,
                atlasPadding: 5,
                renderMode: UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA,
                atlasWidth: 1024,
                atlasHeight: 1024);

            if (fontAsset == null)
            {
                Debug.LogError("[TMP] Failed to create font asset");
                return;
            }

            var path = "Assets/Resources/Fonts/arial SDF.asset";
            AssetDatabase.CreateAsset(fontAsset, path);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[TMP] Created SDF font asset at {path}");
            Selection.activeObject = fontAsset;
        }
    }
}
