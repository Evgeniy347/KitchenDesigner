using UnityEngine;

namespace KitchenDesigner.Core
{
    internal static class ProjectJson
    {
        public static string Serialize(ProjectData data) => JsonUtility.ToJson(data, true);

        public static ProjectData? Deserialize(string json)
        {
            if (string.IsNullOrEmpty(json)) return null;
            return JsonUtility.FromJson<ProjectData>(json);
        }

        public static bool IsVersionCompatible(ProjectData data) =>
            data != null && data.version == AppConstants.SAVE_FORMAT_VERSION;
    }
}
