using System.Collections.Generic;
using System.Text;

namespace KitchenDesigner.Core
{
    public static class RawElementRecords
    {
        public const string ElementsKey = "elements";

        public static List<string> Extract(string projectJson)
        {
            var records = new List<string>();
            if (string.IsNullOrEmpty(projectJson)) return records;
            var array = ElementsArray(projectJson);
            if (!array.Found) return records;
            foreach (var item in JsonText.ArrayItems(projectJson, array))
                records.Add(item.Text(projectJson));
            return records;
        }

        public static string Apply(string projectJson, IReadOnlyList<string?> rawByIndex)
        {
            if (string.IsNullOrEmpty(projectJson) || rawByIndex == null) return projectJson;
            var array = ElementsArray(projectJson);
            if (!array.Found) return projectJson;

            var items = JsonText.ArrayItems(projectJson, array);
            var result = projectJson;
            for (int i = items.Count - 1; i >= 0; i--)
            {
                if (i >= rawByIndex.Count) continue;
                string? raw = rawByIndex[i];
                if (string.IsNullOrEmpty(raw)) continue;
                string indent = JsonText.LineIndentBefore(projectJson, items[i].Start);
                string merged = Merge(raw!, items[i].Text(projectJson), indent);
                result = result.Substring(0, items[i].Start)
                    + merged
                    + result.Substring(items[i].End);
            }
            return result;
        }

        public static string Merge(string rawRecord, string currentRecord, string indent)
        {
            var rawMembers = JsonText.Members(rawRecord, JsonText.RootObject(rawRecord));
            var currentMembers = JsonText.Members(currentRecord, JsonText.RootObject(currentRecord));
            if (rawMembers.Count == 0) return currentRecord;

            var currentByKey = new Dictionary<string, string>(currentMembers.Count);
            foreach (var member in currentMembers)
                currentByKey[member.Key] = member.Value.Text(currentRecord);

            var keys = new List<string>(rawMembers.Count + currentMembers.Count);
            var values = new List<string>(keys.Capacity);
            var taken = new HashSet<string>();
            foreach (var member in rawMembers)
            {
                if (!taken.Add(member.Key)) continue;
                keys.Add(member.Key);
                values.Add(currentByKey.TryGetValue(member.Key, out var replacement)
                    ? replacement
                    : member.Value.Text(rawRecord));
            }
            foreach (var member in currentMembers)
            {
                if (!taken.Add(member.Key)) continue;
                keys.Add(member.Key);
                values.Add(member.Value.Text(currentRecord));
            }

            return Render(keys, values, indent);
        }

        private static string Render(List<string> keys, List<string> values, string indent)
        {
            string memberIndent = indent + "    ";
            var sb = new StringBuilder();
            sb.Append('{');
            for (int i = 0; i < keys.Count; i++)
            {
                sb.Append('\n').Append(memberIndent)
                    .Append('"').Append(keys[i]).Append("\": ").Append(values[i]);
                if (i < keys.Count - 1) sb.Append(',');
            }
            sb.Append('\n').Append(indent).Append('}');
            return sb.ToString();
        }

        private static JsonSpan ElementsArray(string projectJson)
        {
            var root = JsonText.RootObject(projectJson);
            if (!root.Found) return JsonSpan.None;
            var array = JsonText.MemberValue(projectJson, root, ElementsKey);
            if (!array.Found || projectJson[array.Start] != '[') return JsonSpan.None;
            return array;
        }
    }
}
