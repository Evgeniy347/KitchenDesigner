using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
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
            SnapshotFile.Write(outputPath, Normalize(json));
            Debug.Log($"[UISNAPSHOT] Saved: {outputPath}");
            ReportOverlaps(snapshot, Path.GetFileNameWithoutExtension(outputPath));
        }

        /// <summary>Единственный инвариант, который сличение без координат дать
        /// не может. Громкость та же, что у расхождения с эталоном, —
        /// незапрошенный LogType.Error роняет тест силами Unity Test Framework,
        /// и NUnit не приходится тащить в Assets/Scripts.</summary>
        private static void ReportOverlaps(UiSnapshotZero snapshot, string name)
        {
            var report = UiNodeOverlap.Report(name, snapshot.Children.Select(n => n.Placed));
            if (report.Length > 0) Debug.LogError(report);
        }

        /// <summary>
        /// Capture UI snapshot and verify against a golden-master file.
        /// Uses the same candidate/verified pattern as Snapshot.Match:
        /// no verified → writes candidate → fail (dev reviews, renames, commits).
        /// </summary>
        public static void CaptureVerified(GameObject root, string outputPath)
        {
            var snapshot = BuildSnapshot(root);
            var json = SerializeSnapshot(snapshot);

            SnapshotFile.Write(outputPath, Normalize(json));
            Debug.Log($"[UISNAPSHOT] Saved: {outputPath}");

            var testName = Path.GetFileNameWithoutExtension(outputPath);
            ReportOverlaps(snapshot, testName);
            MatchGolden(json, testName);
        }

        /// <summary>Compare actual JSON against a verified golden file, ignoring positions.</summary>
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
            public UiNodeOverlap.Placed Placed;
        }

        // ── Build ───────────────────────────────────────────────────────

        private static UiSnapshotZero BuildSnapshot(GameObject root)
        {
            var nodes = new List<UiNode>();
            var seen = new HashSet<GameObject>();
            Walk(root, nodes, seen);

            nodes.Sort(CompareNodes);

            return new UiSnapshotZero { Root = root.name, Children = nodes };
        }

        /// <summary>
        /// Полный (тотальный) порядок сортировки узлов: сверху вниз, затем слева
        /// направо, затем по типу/имени/тексту.
        ///
        /// Раньше сравнение шло ТОЛЬКО по Position.y, а List.Sort (интросорт)
        /// нестабилен: у десятка узлов тулбара y одинаковый (-6), поэтому их
        /// взаимный порядок зависел от общего числа узлов в списке. Стоило
        /// добавить одну кнопку — и весь файл эталона перетасовывался, а дифф
        /// становился нечитаемым. Тотальный порядок делает вывод независимым от
        /// порядка обхода и от количества узлов.
        /// </summary>
        private static int CompareNodes(UiNode a, UiNode b)
        {
            int c = b.Position.y.CompareTo(a.Position.y);
            if (c != 0) return c;
            c = a.Position.x.CompareTo(b.Position.x);
            if (c != 0) return c;
            c = string.CompareOrdinal(a.Type, b.Type);
            if (c != 0) return c;
            c = string.CompareOrdinal(a.Name, b.Name);
            if (c != 0) return c;
            c = string.CompareOrdinal(a.Text, b.Text);
            if (c != 0) return c;
            return string.CompareOrdinal(a.Label, b.Label);
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
                node.Placed = PlaceOnCanvas(go, node);
                seen.Add(go);
                nodes.Add(node);

                if (node.Type == "InputField")
                    MarkLabelSeen(go, seen);
            }

            foreach (Transform child in go.transform)
                Walk(child.gameObject, nodes, seen);
        }

        /// <summary>Точка узла в ОБЩЕМ для холста пространстве, а не
        /// anchoredPosition, которая локальна: у восьми переключателей одной
        /// вкладки она законно одинакова, каждый сидит в своей строке. Угловые
        /// точки берём у самого RectTransform, поэтому масштаб и вложенность
        /// учтены без ручной арифметики.</summary>
        private static UiNodeOverlap.Placed PlaceOnCanvas(GameObject go, UiNode node)
        {
            var id = node.Type + " «" + Describe(node) + "» (" + go.name + ")";

            var rt = go.GetComponent<RectTransform>();
            if (rt == null) return new UiNodeOverlap.Placed(id, 0f, 0f, 0f, 0f);

            var corners = new Vector3[4];
            rt.GetWorldCorners(corners);
            float minX = Mathf.Min(corners[0].x, corners[2].x);
            float maxX = Mathf.Max(corners[0].x, corners[2].x);
            float minY = Mathf.Min(corners[0].y, corners[2].y);
            float maxY = Mathf.Max(corners[0].y, corners[2].y);

            return new UiNodeOverlap.Placed(id,
                (minX + maxX) * 0.5f, (minY + maxY) * 0.5f, maxX - minX, maxY - minY);
        }

        private static string Describe(UiNode node)
        {
            if (!string.IsNullOrEmpty(node.Text)) return node.Text;
            if (!string.IsNullOrEmpty(node.Label)) return node.Label;
            return node.Options.Count > 0 ? string.Join("/", node.Options) : "";
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
                    return MaskLiveCounters(tmp.text);
            }
            return null;
        }

        /// <summary>Заменить «живой» счётчик в цветном бейдже на «(#)».
        ///
        /// Кнопка «Ошибки» несёт число проблем текущего проекта
        /// («Ошибки &lt;color=#E64040&gt;(52)&lt;/color&gt;»). Это ДАННЫЕ, а не вёрстка:
        /// любая правка геометрии меняет счётчик и роняет эталон, к UI отношения
        /// не имеющий. Маскируем только число внутри цветного тега — обычные
        /// подписи с цифрами (номера вкладок, размеры) остаются как есть.</summary>
        private static string MaskLiveCounters(string text) =>
            CounterBadge.Replace(text, "(#)");

        private static readonly Regex CounterBadge =
            new Regex(@"(?<=<color=[^>]*>)\(\d+\)(?=</color>)", RegexOptions.Compiled);

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

        // ── Маскирование волатильных значений ───────────────────────────

        /// <summary>
        /// Тексты, которые меняются сами по себе (номер версии, дата сборки) и
        /// протухают в эталоне при каждом бампе. Значение вырезается, признак
        /// «поле на месте и подписано так-то» остаётся.
        /// </summary>
        private static readonly (Regex Pattern, string Replacement)[] VolatileMasks =
        {
            (new Regex(@"^(Версия:\s*).+$", RegexOptions.Singleline), "$1<masked>"),
            (new Regex(@"^(Сборка:\s*).+$", RegexOptions.Singleline), "$1<masked>"),
            (new Regex(@"^(Version:\s*).+$", RegexOptions.Singleline), "$1<masked>"),
            (new Regex(@"^(Build:\s*).+$", RegexOptions.Singleline), "$1<masked>"),
        };

        private static string Mask(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            foreach (var (pattern, replacement) in VolatileMasks)
            {
                if (pattern.IsMatch(s))
                    return pattern.Replace(s, replacement);
            }
            return s;
        }

        private static string BoolStr(bool v) => v ? "true" : "false";

        private static string F(float f) => f.ToString("F0", CultureInfo.InvariantCulture);

        /// <summary>Экранирование + маскирование волатильных значений.</summary>
        private static string Esc(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            s = Mask(s);
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

        // ── Golden-master (like Snapshot.Match) ─────────────────────────

        private static string GoldenDir
        {
            get
            {
                var d = Path.Combine(Application.dataPath, "Tests", "EditMode", "Snapshots");
                Directory.CreateDirectory(d);
                return d;
            }
        }

        private static void MatchGolden(string actualJson, string testName)
        {
            var cleanName = SanitizeName(testName);
            var verifiedPath = Path.Combine(GoldenDir, "ui_" + cleanName + ".verified.json");
            var candidatePath = Path.Combine(GoldenDir, "ui_" + cleanName + ".candidate.json");

            if (!File.Exists(verifiedPath))
            {
                WriteGolden(candidatePath, actualJson);
                Debug.LogError(
                    $"[UISNAPSHOT] No verified golden for '{testName}'.\n" +
                    $"  Candidate: {candidatePath}\n" +
                    $"  Rename to accept: ui_{cleanName}.verified.json");
                return;
            }

            var expected = Normalize(File.ReadAllText(verifiedPath));
            var actual = Normalize(actualJson);

            if (expected == actual)
            {
                if (File.Exists(candidatePath)) File.Delete(candidatePath);
                return;
            }

            var expStripped = StripPositions(expected);
            var actStripped = StripPositions(actual);

            if (expStripped == actStripped)
            {
                Debug.Log($"[UISNAPSHOT] '{testName}' — position-only diff, accepted.");
                if (File.Exists(candidatePath)) File.Delete(candidatePath);
                return;
            }

            WriteGolden(candidatePath, actualJson);
            Debug.LogError(
                $"[UISNAPSHOT] Golden mismatch: {testName}\n" +
                BuildDiff(expStripped, actStripped) +
                $"\n  Verified: {verifiedPath}\n" +
                $"  Candidate: {candidatePath}");
        }

        private static void WriteGolden(string path, string json) =>
            SnapshotFile.Write(path, Normalize(json));

        private static string SanitizeName(string name)
        {
            var sb = new StringBuilder();
            foreach (var ch in name)
            {
                if (char.IsLetterOrDigit(ch) || ch == '_' || ch == '-' || ch == '.')
                    sb.Append(ch);
                else
                    sb.Append('_');
            }
            return sb.ToString();
        }
    }
}
