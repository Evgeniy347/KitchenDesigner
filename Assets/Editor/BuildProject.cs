using KitchenDesigner.Editor;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public static class BuildProject
{
    [MenuItem("KitchenDesigner/Build Windows")]
    public static void Build()
    {
        BuildWindows();
    }

    // ── Windows Debug (fastest iteration) ──────────────────

    [MenuItem("KitchenDesigner/Build Windows Debug")]
    public static void BuildWindowsDebug()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        Debug.Log("[BuildProject] Windows Debug — starting (development, incremental)...");

        EnsureShadersIncluded();
        EnsureURPAssigned();
        EnsureWindowSettings();
        EditorUserBuildSettings.development = true;
        EditorUserBuildSettings.allowDebugging = true;

        // Separate folder: a running release exe from Build/ must not lock the debug loop.
        var location = "Build_Debug/KitchenDesigner.exe";
        var options = new BuildPlayerOptions
        {
            scenes = new[] { "Assets/Scenes/TestScene.unity" },
            locationPathName = location,
            targetGroup = BuildTargetGroup.Standalone,
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.Development | BuildOptions.AllowDebugging
        };

        var report = BuildPipeline.BuildPlayer(options);

        sw.Stop();
        if (report.summary.result == BuildResult.Succeeded)
            Debug.Log($"[BuildProject] Windows Debug OK: {location} ({report.summary.totalSize / 1048576.0:F1} MB) in {sw.Elapsed.TotalSeconds:F0}s");
        else
            LogBuildFailure(report);

        EditorApplication.Exit(report.summary.result == BuildResult.Succeeded ? 0 : 1);
    }

    // ── WebGL Release ──────────────────────────────────────

    [MenuItem("KitchenDesigner/Build WebGL Release")]
    public static void BuildWebGLRelease()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        Debug.Log("[BuildProject] WebGL Release — starting...");

        ConfigureWebGL(isDebug: false);
        EnsureShadersIncluded();
        EnsureURPAssigned();
        PlayerSettings.runInBackground = true;

        var scenes = new[] { "Assets/Scenes/TestScene.unity" };
        var location = "Builds/WebGL";

        var options = new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = location,
            targetGroup = BuildTargetGroup.WebGL,
            target = BuildTarget.WebGL,
            options = BuildOptions.None
        };

        var report = BuildPipeline.BuildPlayer(options);

        sw.Stop();
        if (report.summary.result == BuildResult.Succeeded)
            Debug.Log($"[BuildProject] WebGL Release OK: {location} ({report.summary.totalSize / 1048576.0:F1} MB) in {sw.Elapsed.TotalSeconds:F0}s");
        else
            LogBuildFailure(report);

        EditorApplication.Exit(report.summary.result == BuildResult.Succeeded ? 0 : 1);
    }

    // ── WebGL Debug (fast iteration) ───────────────────────

    [MenuItem("KitchenDesigner/Build WebGL Debug")]
    public static void BuildWebGLDebug()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        Debug.Log("[BuildProject] WebGL Debug — starting (no compression, no stripping)...");

        ConfigureWebGL(isDebug: true);
        EnsureShadersIncluded();
        EnsureURPAssigned();
        PlayerSettings.runInBackground = true;

        var scenes = new[] { "Assets/Scenes/TestScene.unity" };
        var location = "Builds/WebGL_Debug";

        var options = new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = location,
            targetGroup = BuildTargetGroup.WebGL,
            target = BuildTarget.WebGL,
            options = BuildOptions.Development | BuildOptions.AllowDebugging
        };

        var report = BuildPipeline.BuildPlayer(options);

        sw.Stop();
        if (report.summary.result == BuildResult.Succeeded)
            Debug.Log($"[BuildProject] WebGL Debug OK: {location} ({report.summary.totalSize / 1048576.0:F1} MB) in {sw.Elapsed.TotalSeconds:F0}s");
        else
            LogBuildFailure(report);

        EditorApplication.Exit(report.summary.result == BuildResult.Succeeded ? 0 : 1);
    }

    // ── Configuration ──────────────────────────────────────

    private static void ConfigureWebGL(bool isDebug)
    {
        EnsureTMProFont();

        // Debug: no compression — nginx serves plain files, build skips the gzip pass.
        // Release: gzip matches nginx.conf (gzip_static + Content-Encoding locations).
        PlayerSettings.WebGL.compressionFormat = isDebug
            ? WebGLCompressionFormat.Disabled
            : WebGLCompressionFormat.Gzip;

        PlayerSettings.WebGL.debugSymbols = isDebug;
        PlayerSettings.WebGL.exceptionSupport = isDebug
            ? WebGLExceptionSupport.FullWithStacktrace
            : WebGLExceptionSupport.None;

        PlayerSettings.SetManagedStrippingLevel(
            BuildTargetGroup.WebGL,
            isDebug ? ManagedStrippingLevel.Disabled : ManagedStrippingLevel.High);

        PlayerSettings.WebGL.dataCaching = false;
        PlayerSettings.WebGL.memorySize = isDebug ? 512 : 256;
        PlayerSettings.WebGL.threadsSupport = false;

        EditorUserBuildSettings.development = isDebug;
        EditorUserBuildSettings.allowDebugging = isDebug;
        EditorUserBuildSettings.waitForPlayerConnection = false;
        EditorUserBuildSettings.connectProfiler = false;
        EditorUserBuildSettings.buildWithDeepProfilingSupport = false;

        Debug.Log($"[BuildProject] WebGL configured: debug={isDebug}, stripping={(isDebug ? "off" : "high")}, exceptions={(isDebug ? "full" : "none")}");
    }

    // ── Windows ────────────────────────────────────────────

    private static void BuildWindows()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        EnsureShadersIncluded();
        EnsureURPAssigned();
        EnsureWindowSettings();
        EditorUserBuildSettings.development = false;
        EditorUserBuildSettings.allowDebugging = false;

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

        sw.Stop();
        if (report.summary.result == BuildResult.Succeeded)
        {
            Debug.Log($"[BuildProject] BUILD OK: {location} ({report.summary.totalSize / 1048576.0:F1} MB) in {sw.Elapsed.TotalSeconds:F0}s");
            EditorApplication.Exit(0);
        }
        else
        {
            LogBuildFailure(report);
            EditorApplication.Exit(1);
        }
    }

    // ── Helpers ────────────────────────────────────────────

    private static void LogBuildFailure(BuildReport report)
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
    }

    private static void EnsureWindowSettings()
    {
        PlayerSettings.resizableWindow = true;
        PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
        PlayerSettings.defaultScreenWidth = 1280;
        PlayerSettings.defaultScreenHeight = 720;
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

    private static void EnsureTMProFont()
    {
        var sdfPath = "Assets/Resources/Fonts/arial SDF.asset";
        if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(sdfPath) != null)
        {
            Debug.Log("[BuildProject] TMPro SDF font already exists, skipping");
            return;
        }

        Debug.Log("[BuildProject] Creating TMPro SDF font from arial.ttf...");
        CreateTMPFontFromArial.Create();
    }
}
