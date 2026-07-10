using UnityEditor;
using UnityEngine;
using KitchenDesigner.Core;

[InitializeOnLoad]
public static class ProjectSetup
{
    static ProjectSetup()
    {
        EnsureKitchenSettings();
        EnsureTags();
    }

    private static void EnsureKitchenSettings()
    {
        if (KitchenSettings.Instance != null)
            return;

        var settings = ScriptableObject.CreateInstance<KitchenSettings>();
        var path = "Assets/Resources/KitchenSettings.asset";
        var dir = "Assets/Resources";
        if (!AssetDatabase.IsValidFolder(dir))
            AssetDatabase.CreateFolder("Assets", "Resources");

        AssetDatabase.CreateAsset(settings, path);
        AssetDatabase.SaveAssets();
        Debug.Log("[ProjectSetup] Created KitchenSettings.asset");
    }

    private static void EnsureTags()
    {
        var tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
        var tagsProp = tagManager.FindProperty("tags");

        AddTag(tagsProp, "KitchenElement");
        AddTag(tagsProp, "Floor");

        tagManager.ApplyModifiedProperties();
    }

    private static void AddTag(SerializedProperty tagsProp, string tag)
    {
        for (int i = 0; i < tagsProp.arraySize; i++)
        {
            if (tagsProp.GetArrayElementAtIndex(i).stringValue == tag)
                return;
        }
        tagsProp.InsertArrayElementAtIndex(tagsProp.arraySize);
        tagsProp.GetArrayElementAtIndex(tagsProp.arraySize - 1).stringValue = tag;
        Debug.Log($"[ProjectSetup] Added tag: {tag}");
    }
}
