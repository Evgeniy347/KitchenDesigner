using System.Collections.Generic;
using UnityEngine;

namespace KitchenDesigner.Core
{
    internal static class ProjectJson
    {
        private const string TypeKey = "\"" + nameof(ElementData.elementType) + "\"";

        public static string Serialize(ProjectData data) =>
            RawElementRecords.Apply(JsonUtility.ToJson(data, true), RawRecordsOf(data));

        public static ProjectData? Deserialize(string json)
        {
            if (string.IsNullOrEmpty(json)) return null;
            var data = JsonUtility.FromJson<ProjectData>(json);
            AttachRawRecords(data, json);
            return data;
        }

        public static bool IsVersionCompatible(ProjectData data) =>
            data != null && data.version == AppConstants.SAVE_FORMAT_VERSION;

        private static List<string?> RawRecordsOf(ProjectData data)
        {
            var records = new List<string?>();
            if (data == null || data.elements == null) return records;
            foreach (var element in data.elements)
                records.Add(element?.rawJson);
            return records;
        }

        private static void AttachRawRecords(ProjectData? data, string json)
        {
            if (data == null || data.elements == null) return;
            if (json.IndexOf(TypeKey, System.StringComparison.Ordinal) < 0) return;
            var records = RawElementRecords.Extract(json);
            int count = records.Count < data.elements.Length ? records.Count : data.elements.Length;
            for (int i = 0; i < count; i++)
                if (data.elements[i] != null)
                    data.elements[i].rawJson = records[i];
        }
    }
}
