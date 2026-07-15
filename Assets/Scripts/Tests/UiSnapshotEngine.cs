using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KitchenDesigner.Tests
{
    public static class UiSnapshotEngine
    {
        public static void Capture(GameObject root, string outputPath)
        {
            var snapshot = BuildSnapshot(root);
            var json = SerializeSnapshot(snapshot);
            var dir = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);
            File.WriteAllText(outputPath, json, Encoding.UTF8);
            Debug.Log($"[UISNAPSHOT] Saved: {outputPath}");
        }

        public static void AssertSnapshot(string expectedJsonPath, string actualJson)
        {
            var expected = Normalize(File.ReadAllText(expectedJsonPath));
            var actual = Normalize(actualJson);

            if (expected == actual) return;

            var expStripped = StripPositions(expected);
            var actStripped = StripPositions(actual);

            if (expStripped == actStripped) return;

            throw new Exception(
                $"UI snapshot mismatch: {expectedJsonPath}\n" +
                BuildDiff(expStripped, actStripped));
        }

        // ── Internal types ──────────────────────────────────────────────

        private sealed class UiNode
        {
            public string Type = "";
            public string Text = "";
            public string Label = "";
            public bool Interactable = true;
            public List<string> Options = new();
            public int Selected;
            public bool IsOn;
            public Vector2 Position;
            public string Name = "";
        }

        // ── Build ───────────────────────────────────────────────────────

        private static UiSnapshotZero BuildSnapshot(GameObject root)
        {
            var nodes = new List<UiNode>();
            var seen = new HashSet<GameObject>();
            Walk(root, nodes, seen);

            nodes.Sort((a, b) => b.Position.y.CompareTo(a.Position.y));

            return new UiSnapshotZero { Root = root.name, Children = nodes };
        }

        private sealed class UiSnapshotZero
        {
            public string Root = "";
            public List<UiNode> Children = new();
        }

        private static void Walk(GameObject go, List<UiNode> nodes, HashSet<GameObject> seen)
        {
            if (!go.activeSelf || seen.Contains(go)) return;

            var node = DetectNode(go);
            if (node != null)
            {
                seen.Add(go);
                nodes.Add(node);

                if (node.Type == "InputField")
                    MarkLabelSeen(go, seen);
            }

            foreach (Transform child in go.transform)
                Walk(child.gameObject, nodes, seen);
        }

        private static UiNode? DetectNode(GameObject go)
        {
            var rt = go.GetComponent<RectTransform>();
            var pos = rt != null ? rt.anchoredPosition : Vector2.zero;

            var input = go.GetComponent<TMP_InputField>();
            if (input != null)
            {
                return new UiNode
                {
                    Type = "InputField",
                    Label = FindInputLabel(go) ?? FallbackLabel(go.name),
                    Text = input.text,
                    Interactable = input.interactable,
                    Position = pos,
                    Name = go.name,
                };
            }

            var dropdown = go.GetComponent<TMP_Dropdown>();
            if (dropdown != null)
            {
                return new UiNode
                {
                    Type = "Dropdown",
                    Options = dropdown.options.Select(o => o.text).ToList(),
                    Selected = dropdown.value,
                    Text = dropdown.captionText != null ? dropdown.captionText.text : "",
                    Position = pos,
                    Name = go.name,
                };
            }

            var toggle = go.GetComponent<Toggle>();
            if (toggle != null)
            {
                return new UiNode
                {
                    Type = "Toggle",
                    IsOn = toggle.isOn,
                    Label = FindChildText(go) ?? go.name,
                    Position = pos,
                    Name = go.name,
                };
            }

            var button = go.GetComponent<Button>();
            if (button != null)
            {
                return new UiNode
                {
                    Type = "Button",
                    Text = FindChildText(go) ?? go.name,
                    Interactable = button.interactable,
                    Position = pos,
                    Name = go.name,
                };
            }

            var tmp = go.GetComponent<TextMeshProUGUI>();
            if (tmp != null)
            {
                var parent = go.transform.parent;
                if (parent != null)
                {
                    if (parent.GetComponent<Button>() != null ||
                        parent.GetComponent<Toggle>() != null ||
                        parent.GetComponent<TMP_Dropdown>() != null ||
                        parent.GetComponent<TMP_InputField>() != null)
                        return null;
                }

                return new UiNode
                {
                    Type = "Label",
                    Text = tmp.text,
                    Position = pos,
                    Name = go.name,
                };
            }

            return null;
        }

        private static string? FindInputLabel(GameObject inputGo)
        {
            if (inputGo.name.StartsWith("F_"))
            {
                var labelName = "L_" + inputGo.name.Substring(2);
                var parent = inputGo.transform.parent;
                if (parent != null)
                {
                    var labelT = parent.Find(labelName);
                    if (labelT != null)
                    {
                        var tmp = labelT.GetComponent<TextMeshProUGUI>();
                        if (tmp != null) return tmp.text;
                    }
                }
            }
            return null;
        }

        private static string FallbackLabel(string fieldName)
        {
            if (fieldName.StartsWith("F_"))
                return fieldName.Substring(2);
            return fieldName;
        }

        private static void MarkLabelSeen(GameObject inputGo, HashSet<GameObject> seen)
        {
            if (inputGo.name.StartsWith("F_"))
            {
                var labelName = "L_" + inputGo.name.Substring(2);
                var parent = inputGo.transform.parent;
                if (parent != null)
                {
                    var labelT = parent.Find(labelName);
                    if (labelT != null)
                        seen.Add(labelT.gameObject);
                }
            }
        }

        private static string? FindChildText(GameObject go)
        {
            foreach (Transform child in go.transform)
            {
                var tmp = child.GetComponent<TextMeshProUGUI>();
                if (tmp != null && !string.IsNullOrEmpty(tmp.text))
                    return tmp.text;
            }
            return null;
        }

        // ── Serialization ───────────────────────────────────────────────

        private static string SerializeSnapshot(UiSnapshotZero snapshot)
        {
            var sb = new StringBuilder();
            sb.AppendLine("{");
            sb.AppendLine($"  \"root\": \"{Esc(snapshot.Root)}\",");
            sb.AppendLine("  \"children\": [");

            for (int i = 0; i < snapshot.Children.Count; i++)
            {
                SerializeNode(sb, snapshot.Children[i], "    ");
                if (i < snapshot.Children.Count - 1) sb.AppendLine(",");
                else sb.AppendLine();
            }

            sb.AppendLine("  ]");
            sb.Append('}');
            return sb.ToString();
        }

        private static void SerializeNode(StringBuilder sb, UiNode node, string ind)
        {
            sb.AppendLine($"{ind}{{");
            sb.AppendLine($"{ind}  \"type\": \"{node.Type}\",");

            switch (node.Type)
            {
                case "Label":
                case "Button":
                    sb.AppendLine($"{ind}  \"text\": \"{Esc(node.Text)}\",");
                    break;
                case "InputField":
                    sb.AppendLine($"{ind}  \"label\": \"{Esc(node.Label)}\",");
                    sb.AppendLine($"{ind}  \"value\": \"{Esc(node.Text)}\",");
                    sb.AppendLine($"{ind}  \"interactable\": {BoolStr(node.Interactable)},");
                    break;
                case "Dropdown":
                    sb.Append($"{ind}  \"options\": [");
                    if (node.Options.Count > 0)
                    {
                        for (int i = 0; i < node.Options.Count; i++)
                        {
                            if (i > 0) sb.Append(", ");
                            sb.Append($"\"{Esc(node.Options[i])}\"");
                        }
                    }
                    sb.AppendLine("],");
                    sb.AppendLine($"{ind}  \"selected\": {node.Selected},");
                    break;
                case "Toggle":
                    sb.AppendLine($"{ind}  \"label\": \"{Esc(node.Label)}\",");
                    sb.AppendLine($"{ind}  \"isOn\": {BoolStr(node.IsOn)},");
                    break;
            }

            sb.AppendLine($"{ind}  \"position\": [{F(node.Position.x)}, {F(node.Position.y)}]");
            sb.Append($"{ind}}}");
        }

        private static string BoolStr(bool v) => v ? "true" : "false";

        private static string F(float f) => f.ToString("F0", CultureInfo.InvariantCulture);

        private static string Esc(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Replace("\\", "\\\\").Replace("\"", "\\\"")
                    .Replace("\n", "\\n").Replace("\r", "\\r").Replace("\t", "\\t");
        }

        // ── Comparison ──────────────────────────────────────────────────

        private static string Normalize(string json) =>
            (json ?? "").Replace("\r\n", "\n").Replace("\r", "\n").Trim();

        private static string StripPositions(string json)
        {
            var sb = new StringBuilder();
            foreach (var line in json.Split('\n'))
            {
                if (!line.TrimStart().StartsWith("\"position\""))
                    sb.AppendLine(line);
            }
            return sb.ToString().Trim();
        }

        private static string BuildDiff(string expected, string actual)
        {
            var expLines = expected.Split('\n');
            var actLines = actual.Split('\n');
            var sb = new StringBuilder();
            int lines = Math.Max(expLines.Length, actLines.Length);
            int diffs = 0;

            for (int i = 0; i < lines && diffs < 15; i++)
            {
                string e = i < expLines.Length ? expLines[i] : "<eof>";
                string a = i < actLines.Length ? actLines[i] : "<eof>";
                if (e != a)
                {
                    sb.AppendLine($"  L{i + 1}: - {e}");
                    sb.AppendLine($"  L{i + 1}: + {a}");
                    diffs++;
                }
            }

            return sb.ToString();
        }
    }
}
