using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

public static class BuildProject
{
    [MenuItem("KitchenDesigner/Build")]
    public static void Build()
    {
        EnsureShadersIncluded();
        EnsureURPAssigned();
        EnsureWindowSettings();

        var scenes = new[] { "Assets/Scenes/TestScene.unity" };
        var location = "Build/KitchenDesigner.exe";

        var options = new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = location,
            targetGroup = BuildTargetGroup.Standalone,
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.CompressWithLz4
        };

        var report = BuildPipeline.BuildPlayer(options);

        if (report.summary.result == BuildResult.Succeeded)
        {
            Debug.Log($"[BuildProject] BUILD OK: {location} ({report.summary.totalSize / 1048576.0:F1} MB)");
            EditorApplication.Exit(0);
        }
        else
        {
            Debug.LogError($"[BuildProject] BUILD FAILED: {report.summary.result}");
            foreach (var step in report.steps)
            {
                foreach (var msg in step.messages)
                {
                    if (msg.type == LogType.Error || msg.type == LogType.Exception)
                        Debug.LogError($"[Build] {step.name}: {msg.content}");
                }
            }
            EditorApplication.Exit(1);
        }
    }

    private static void EnsureWindowSettings()
    {
        // Полноценное окно Windows: с рамкой и растягиваемое.
        PlayerSettings.resizableWindow = true;
        PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
        PlayerSettings.defaultScreenWidth = 1280;
        PlayerSettings.defaultScreenHeight = 720;
        // Не замирать без фокуса: иначе стоят автосейв-таймер и TCP-мост отладки.
        PlayerSettings.runInBackground = true;
        Debug.Log("[BuildProject] Window: resizable, windowed 1280x720, runInBackground");
    }

    private static void EnsureURPAssigned()
    {
        var urpAsset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>("Assets/URPAsset.asset");
        if (urpAsset == null)
        {
            urpAsset = ScriptableObject.CreateInstance<UniversalRenderPipelineAsset>();
            AssetDatabase.CreateAsset(urpAsset, "Assets/URPAsset.asset");
            AssetDatabase.SaveAssets();
        }

        EnsureRendererData(urpAsset);

        int currentLevel = QualitySettings.GetQualityLevel();
        for (int i = 0; i < QualitySettings.names.Length; i++)
        {
            QualitySettings.SetQualityLevel(i, false);
            QualitySettings.renderPipeline = urpAsset;
        }
        QualitySettings.SetQualityLevel(currentLevel, false);

        var gsSo = new SerializedObject(GraphicsSettings.GetGraphicsSettings());
        gsSo.FindProperty("m_CustomRenderPipeline").objectReferenceValue = urpAsset;
        gsSo.ApplyModifiedProperties();

        AssetDatabase.SaveAssets();
        Debug.Log("[BuildProject] URP assigned to all quality levels and GraphicsSettings");
    }

    private static void EnsureRendererData(UniversalRenderPipelineAsset urpAsset)
    {
        var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>("Assets/URPForwardRenderer.asset");
        if (renderer == null)
        {
            renderer = ScriptableObject.CreateInstance<UniversalRendererData>();
            renderer.name = "URPForwardRenderer";
            AssetDatabase.CreateAsset(renderer, "Assets/URPForwardRenderer.asset");
            AssetDatabase.SaveAssets();
            Debug.Log("[BuildProject] Created URPForwardRenderer.asset");
        }

        var so = new SerializedObject(urpAsset);
        var rendererDataList = so.FindProperty("m_RendererDataList");
        if (rendererDataList.arraySize == 0 || rendererDataList.GetArrayElementAtIndex(0).objectReferenceValue == null)
        {
            rendererDataList.ClearArray();
            rendererDataList.InsertArrayElementAtIndex(0);
            rendererDataList.GetArrayElementAtIndex(0).objectReferenceValue = renderer;
            var defaultIndex = so.FindProperty("m_DefaultRendererIndex");
            defaultIndex.intValue = 0;
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(urpAsset);
            AssetDatabase.SaveAssets();
            Debug.Log("[BuildProject] Assigned UniversalRendererData to URPAsset");
        }
    }

    private static void EnsureShadersIncluded()
    {
        var gs = new SerializedObject(GraphicsSettings.GetGraphicsSettings());
        var alwaysIncluded = gs.FindProperty("m_AlwaysIncludedShaders");
        var lit = Shader.Find("Universal Render Pipeline/Lit");
        if (lit == null) return;

        bool found = false;
        for (int i = 0; i < alwaysIncluded.arraySize; i++)
        {
            if (alwaysIncluded.GetArrayElementAtIndex(i).objectReferenceValue == lit)
            {
                found = true;
                break;
            }
        }

        if (!found)
        {
            alwaysIncluded.InsertArrayElementAtIndex(alwaysIncluded.arraySize);
            alwaysIncluded.GetArrayElementAtIndex(alwaysIncluded.arraySize - 1).objectReferenceValue = lit;
            gs.ApplyModifiedProperties();
            AssetDatabase.SaveAssets();
            Debug.Log("[BuildProject] Added URP Lit to always included shaders");
        }
    }
}
