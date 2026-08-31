using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using UnityEngine;

namespace KitchenDesigner.Tests.EditMode
{
    public class CmdScriptEncodingTests
    {
        private static readonly string[] SkippedDirectories =
        {
            "Library", "Temp", "obj", "bin", "node_modules",
            "Build", "Builds", "Build_Debug", "test-results", "tmp-scripts"
        };

        private static readonly char[] Separators = { '\u005C', '/' };

        private const string ReleaseScript = "publish-github.cmd";
        private const string InstallerScript = "build-installer.cmd";
        private const string GatewayScript = "build.cmd";

        private static string RepoRoot => Path.GetFullPath(Path.Combine(Application.dataPath, ".."));

        private static List<string> CollectScripts()
        {
            var root = RepoRoot;
            return Directory.EnumerateFiles(root, "*.cmd", SearchOption.AllDirectories)
                .Where(p => !RelativeParts(root, p).Any(part => SkippedDirectories.Contains(part)))
                .OrderBy(p => p)
                .ToList();
        }

        private static IEnumerable<string> RelativeParts(string root, string fullPath)
        {
            string relative = fullPath.Substring(root.Length).TrimStart(Separators);
            string directory = Path.GetDirectoryName(relative) ?? string.Empty;
            return directory.Split(Separators);
        }

        [Test]
        public void CmdScan_ReachesTheRepository_AndSeesTheReleaseScripts()
        {
            var scripts = CollectScripts();
            var names = scripts.Select(Path.GetFileName).ToList();

            Assert.That(scripts.Count, Is.GreaterThan(0),
                "Скан не нашёл ни одного .cmd — путь до корня репозитория перестал разрешаться, " +
                "и оба сторожа ниже стали зелёными, ничего не проверяя.");
            Assert.That(names, Does.Contain(ReleaseScript),
                "installer/publish-github.cmd обязан попадать в скан: именно на нём кириллица " +
                "под chcp 65001 уже ломала публикацию релиза.");
            Assert.That(names, Does.Contain(InstallerScript),
                "installer/build-installer.cmd обязан попадать в скан — он собирает установщик.");
            Assert.That(names, Does.Contain(GatewayScript),
                "build.cmd обязан попадать в скан: build-installer.cmd зовёт его при сборке плеера.");
        }

        [Test]
        public void CmdScripts_ContainOnlyAscii_SoCmdExeDoesNotMisparseThem()
        {
            var offenders = new List<string>();

            foreach (string path in CollectScripts())
            {
                byte[] bytes = File.ReadAllBytes(path);
                int line = 1;
                var lines = new List<int>();
                foreach (byte b in bytes)
                {
                    if (b == (byte)'\n') line++;
                    else if (b >= 0x80 && !lines.Contains(line)) lines.Add(line);
                }
                if (lines.Count > 0)
                    offenders.Add($"{Relative(path)}: строки {string.Join(", ", lines.Take(10))}");
            }

            Assert.That(offenders, Is.Empty,
                "cmd.exe ищет метки в батнике по БАЙТОВОМУ смещению, поэтому многобайтные символы " +
                "под chcp 65001 сдвигают разбор, и интерпретатор начинает исполнять хвосты " +
                "собственных комментариев ('he' is not recognized). Русский текст живёт в .ps1:\n" +
                string.Join("\n", offenders));
        }

        [Test]
        public void CmdScripts_EndEveryLineWithCrLf_SoGotoStillFindsItsLabels()
        {
            var offenders = new List<string>();

            foreach (string path in CollectScripts())
            {
                string text = File.ReadAllText(path, Encoding.UTF8);
                int bare = 0;
                for (int i = 0; i < text.Length; i++)
                    if (text[i] == '\n' && (i == 0 || text[i - 1] != '\r')) bare++;
                if (bare > 0)
                    offenders.Add($"{Relative(path)}: {bare} перевод(ов) строки без CR");
            }

            Assert.That(offenders, Is.Empty,
                "Голый LF склеивает строки батника и ломает goto — а sed -i из git-bash " +
                "переписывает CRLF в LF молча, так что проверять надо после каждой правки:\n" +
                string.Join("\n", offenders));
        }

        private static string Relative(string path) =>
            path.Substring(RepoRoot.Length).TrimStart(Separators).Replace('\u005C', '/');
    }
}
