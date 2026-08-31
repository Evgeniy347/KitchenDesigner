using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class ProjectSetup
{
    static ProjectSetup()
    {
        EnsureTags();
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
