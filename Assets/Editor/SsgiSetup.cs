using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using KitchenDesigner.Core;

namespace KitchenDesigner.EditorTools
{
    /// <summary>Подключает SSGI-фичу к URP-рендереру и создаёт для неё материал —
    /// настоящим API (не правкой YAML). Запуск: Unity -batchmode -executeMethod
    /// KitchenDesigner.EditorTools.SsgiSetup.Install. Идемпотентно.</summary>
    public static class SsgiSetup
    {
        private const string RendererPath = "Assets/URPForwardRenderer.asset";
        private const string ShaderName = "Hidden/KitchenDesigner/ScreenSpaceGI";
        private const string MaterialPath = "Assets/Scripts/Core/Rendering/SSGI/ScreenSpaceGI.mat";
        private const string FeatureName = "ScreenSpaceGI";

        [MenuItem("Tools/Kitchen/Install SSGI Feature")]
        public static void Install()
        {
            var shader = Shader.Find(ShaderName);
            if (shader == null)
            {
                Debug.LogError($"[SSGI] Shader not found: {ShaderName}");
                EditorApplication.Exit(2);
                return;
            }

            // 1. Материал.
            var mat = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (mat == null)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(MaterialPath)!);
                mat = new Material(shader) { name = "ScreenSpaceGI" };
                AssetDatabase.CreateAsset(mat, MaterialPath);
            }
            else if (mat.shader != shader)
            {
                mat.shader = shader;
                EditorUtility.SetDirty(mat);
            }

            // 2. Рендерер.
            var data = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPath);
            if (data == null)
            {
                Debug.LogError($"[SSGI] Renderer not found: {RendererPath}");
                EditorApplication.Exit(3);
                return;
            }

            // Удаляем прежние SSGI-фичи (идемпотентность).
            for (int i = data.rendererFeatures.Count - 1; i >= 0; i--)
            {
                var f = data.rendererFeatures[i];
                if (f is ScreenSpaceGIFeature)
                {
                    data.rendererFeatures.RemoveAt(i);
                    if (f != null) Object.DestroyImmediate(f, true);
                }
            }

            // Новая фича как подобъект ассета рендерера.
            var feature = ScriptableObject.CreateInstance<ScreenSpaceGIFeature>();
            feature.name = FeatureName;
            feature.SetActive(false); // включается только в фоторежиме
            SetMaterial(feature, mat);
            AssetDatabase.AddObjectToAsset(feature, data);

            // Регистрируем в списке через SerializedObject (как инспектор URP).
            var so = new SerializedObject(data);
            so.Update();
            var list = so.FindProperty("m_RendererFeatures");
            var map = so.FindProperty("m_RendererFeatureMap");
            int idx = list.arraySize;
            list.arraySize = idx + 1;
            list.GetArrayElementAtIndex(idx).objectReferenceValue = feature;
            map.arraySize = idx + 1;
            map.GetArrayElementAtIndex(idx).longValue = feature.GetInstanceID();
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[SSGI] Installed feature into {RendererPath}; material {MaterialPath}. " +
                      $"Features now: {data.rendererFeatures.Count}");
        }

        private static void SetMaterial(ScreenSpaceGIFeature feature, Material mat)
        {
            var so = new SerializedObject(feature);
            var prop = so.FindProperty("_material");
            if (prop != null) prop.objectReferenceValue = mat;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
