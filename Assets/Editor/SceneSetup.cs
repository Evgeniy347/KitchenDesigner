using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEditor.SceneManagement;

public static class SceneSetup
{
    [MenuItem("KitchenDesigner/Setup Test Scene")]
    public static void SetupTestScene()
    {
        EnsureURP();
        CreateTestScene();
    }

    private static void EnsureURP()
    {
        var pipelineAsset = QualitySettings.renderPipeline;
        if (pipelineAsset != null)
        {
            Debug.Log($"[SceneSetup] URP already configured: {pipelineAsset.name}");
            return;
        }

        var urpAsset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>("Assets/URPAsset.asset");
        if (urpAsset == null)
        {
            urpAsset = ScriptableObject.CreateInstance<UniversalRenderPipelineAsset>();
            AssetDatabase.CreateAsset(urpAsset, "Assets/URPAsset.asset");
            AssetDatabase.SaveAssets();
        }

        QualitySettings.renderPipeline = urpAsset;

        var so = new SerializedObject(urpAsset);
        so.FindProperty("m_SupportsHDR").boolValue = false;
        so.FindProperty("m_MSAA").intValue = 1;
        so.FindProperty("m_ShadowDistance").floatValue = 0f;
        so.ApplyModifiedProperties();

        EditorUtility.SetDirty(urpAsset);
        AssetDatabase.SaveAssets();
        Debug.Log("[SceneSetup] URP configured: HDR=off, MSAA=off, Shadows=off");
    }

    private static void CreateTestScene()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        scene.name = "TestScene";

        var camObj = new GameObject("Main Camera");
        var cam = camObj.AddComponent<Camera>();
        cam.transform.position = new Vector3(5f, 5f, -10f);
        cam.transform.LookAt(Vector3.zero);
        cam.farClipPlane = 1000f;
        cam.nearClipPlane = 0.1f;
        cam.clearFlags = CameraClearFlags.Skybox;
        camObj.tag = "MainCamera";

        var lightObj = new GameObject("Directional Light");
        var light = lightObj.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1f;
        light.transform.position = new Vector3(10f, 10f, 0f);
        light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

        var bootstrap = new GameObject("Bootstrap");
        bootstrap.AddComponent<KitchenDesigner.Core.Bootstrap>();

        var scenePath = "Assets/Scenes/TestScene.unity";
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene(), scenePath);
        EditorSceneManager.OpenScene(scenePath);

        Debug.Log($"[SceneSetup] TestScene created at {scenePath}");
    }
}
