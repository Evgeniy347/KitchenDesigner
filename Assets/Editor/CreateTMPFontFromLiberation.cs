using TMPro;
using UnityEditor;
using UnityEngine;

namespace KitchenDesigner.Editor
{
    public static class CreateTMPFontFromLiberation
    {
        private const string SourcePath = "Assets/Resources/Fonts/LiberationSans.ttf";
        public const string AssetPath = "Assets/Resources/Fonts/LiberationSans SDF.asset";

        [MenuItem("Tools/Kitchen/Create TMP Font From LiberationSans")]
        public static void Create()
        {
            var unityFont = AssetDatabase.LoadAssetAtPath<Font>(SourcePath);
            if (unityFont == null)
            {
                Debug.LogError($"[TMP] LiberationSans.ttf not found at {SourcePath}");
                return;
            }

            // Dynamic population mode (default): глифы, включая кириллицу,
            // добавляются в атлас на лету из встроенного ttf.
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

            AssetDatabase.CreateAsset(fontAsset, AssetPath);

            // Атлас и материал созданы в памяти и БЕЗ AddObjectToAsset не попадут
            // в файл: ссылки станут битыми, а TMP_PreBuildProcessor будет падать
            // с UnassignedReferenceException (m_AtlasTextures) на каждой сборке.
            if (fontAsset.atlasTextures != null && fontAsset.atlasTextures.Length > 0 && fontAsset.atlasTextures[0] != null)
            {
                fontAsset.atlasTextures[0].name = "LiberationSans SDF Atlas";
                AssetDatabase.AddObjectToAsset(fontAsset.atlasTextures[0], fontAsset);
            }
            if (fontAsset.material != null)
            {
                fontAsset.material.name = "LiberationSans SDF Material";
                AssetDatabase.AddObjectToAsset(fontAsset.material, fontAsset);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[TMP] Created SDF font asset at {AssetPath}");
            Selection.activeObject = fontAsset;
        }
    }
}
