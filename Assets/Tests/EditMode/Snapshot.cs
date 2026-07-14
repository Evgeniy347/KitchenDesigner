using System.Collections.Generic;
using System.IO;
using System.Text;
using NUnit.Framework;
using UnityEngine;

namespace KitchenDesigner.Core
{
    /// <summary>
    /// Golden-master snapshot testing для Unity EditMode.
    ///
    /// Почему не Verify.NUnit: Unity форк NUnit 3.5, Verify требует NUnit >= 4.3.
    ///
    /// verified-файлы НИКОГДА не создаются и не правятся автоматически —
    /// только вручную разработчиком через переименование candidate → verified.
    ///
    /// Snapshot.Match(actualJson, "MyTest");
    ///   → нет MyTest.verified.json: создаёт MyTest.candidate.json, FAIL
    ///   → verified есть и совпадает: PASS, чистит старый candidate
    ///   → verified есть и отличается: создаёт/обновляет MyTest.candidate.json, FAIL с diff
    ///
    /// Принять: переименовать *.candidate.json → *.verified.json, закоммитить.
    /// </summary>
    public static class Snapshot
    {
        private static string _snapshotDir = null!;

        public static string SnapshotDir
        {
            get
            {
                if (_snapshotDir != null && Directory.Exists(_snapshotDir))
                    return _snapshotDir;

                _snapshotDir = Path.Combine(Application.dataPath, "Tests", "EditMode", "Snapshots");
                Directory.CreateDirectory(_snapshotDir);
                return _snapshotDir;
            }
        }

        /// <summary>
        /// Сравнить actual JSON с эталонным verified-файлом.
        /// verified никогда не правится — при несовпадении создаётся candidate.
        /// </summary>
        public static void Match(string actualJson, string testName)
        {
            var cleanName = SanitizeFileName(testName);
            var verifiedPath = Path.Combine(SnapshotDir, cleanName + ".verified.json");
            var candidatePath = Path.Combine(SnapshotDir, cleanName + ".candidate.json");

            if (!File.Exists(verifiedPath))
            {
                WriteFile(candidatePath, actualJson);
                Assert.Fail(
                    $"No verified snapshot for '{testName}'.\n" +
                    $"Candidate written: {candidatePath}\n" +
                    $"Rename to accept:\n" +
                    $"  {cleanName}.candidate.json  →  {cleanName}.verified.json\n" +
                    "\nContent (first 500 chars):\n" +
                    Truncate(actualJson, 500));
            }

            var expected = NormalizeJson(File.ReadAllText(verifiedPath));
            var actual = NormalizeJson(actualJson);

            if (expected == actual)
            {
                if (File.Exists(candidatePath)) File.Delete(candidatePath);
                return; // PASS
            }

            WriteFile(candidatePath, actualJson);

            var diff = BuildUnifiedDiff(expected, actual, testName, verifiedPath, candidatePath);
            Assert.Fail(diff);
        }

        public static bool Exists(string testName)
        {
            var cleanName = SanitizeFileName(testName);
            return File.Exists(Path.Combine(SnapshotDir, cleanName + ".verified.json"));
        }

        private static void WriteFile(string path, string json)
        {
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);
            File.WriteAllText(path, NormalizeJson(json), Encoding.UTF8);
        }

        private static string NormalizeJson(string json)
        {
            if (string.IsNullOrEmpty(json)) return "";
            return json.Replace("\r\n", "\n").Replace("\r", "\n").TrimEnd();
        }

        private static string BuildUnifiedDiff(string expected, string actual,
            string testName, string verifiedPath, string candidatePath)
        {
            var expLines = expected.Split('\n');
            var actLines = actual.Split('\n');
            var sb = new StringBuilder();

            sb.AppendLine($"Snapshot mismatch: {testName}");
            sb.AppendLine($"  Verified : {verifiedPath}");
            sb.AppendLine($"  Candidate: {candidatePath}");
            sb.AppendLine();

            int diffCount = 0;
            int i = 0, j = 0;
            var chunk = new List<string>();

            while ((i < expLines.Length || j < actLines.Length) && diffCount < 30)
            {
                if (i < expLines.Length && j < actLines.Length && expLines[i] == actLines[j])
                {
                    if (chunk.Count > 0)
                    {
                        foreach (var l in chunk) sb.AppendLine(l);
                        chunk.Clear();
                    }
                    i++; j++;
                    continue;
                }

                if (chunk.Count == 0)
                    chunk.Add($"@@ L{i + 1} @@");

                if (i < expLines.Length)
                {
                    var isRemoved = j >= actLines.Length || expLines[i] != actLines[j];
                    if (isRemoved)
                    {
                        chunk.Add($"- {expLines[i].TrimEnd()}");
                        i++;
                        diffCount++;
                    }
                }
                if (j < actLines.Length)
                {
                    var isAdded = i >= expLines.Length || 
                        (i < expLines.Length && j < actLines.Length && expLines[i] != actLines[j]);
                    if (isAdded)
                    {
                        chunk.Add($"+ {actLines[j].TrimEnd()}");
                        j++;
                        diffCount++;
                    }
                }
            }
            foreach (var l in chunk) sb.AppendLine(l);

            if (diffCount >= 30)
                sb.AppendLine($"  ... ({diffCount} total differences, truncated)");

            sb.AppendLine();
            sb.AppendLine($"To accept: rename {Path.GetFileName(candidatePath)} → {Path.GetFileName(verifiedPath)} and commit.");
            return sb.ToString();
        }

        private static string SanitizeFileName(string name)
        {
            var sb = new StringBuilder();
            foreach (var ch in name)
            {
                if (char.IsLetterOrDigit(ch) || ch == '_' || ch == '-' || ch == '.')
                    sb.Append(ch);
                else
                    sb.Append('_');
            }
            var result = sb.ToString().Trim('_');
            return string.IsNullOrEmpty(result) ? "unnamed" : result;
        }

        private static string Truncate(string s, int maxLen)
            => s.Length <= maxLen ? s : s.Substring(0, maxLen) + "...";
    }
}
