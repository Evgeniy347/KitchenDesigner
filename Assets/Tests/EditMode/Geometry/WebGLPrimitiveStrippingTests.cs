using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace KitchenDesigner.Tests.Geometry
{
    /// <summary>Стриппинг WebGL вырезает КЛАСС КОЛЛАЙДЕРА, на который нигде нет
    /// управляемой ссылки, и тогда CreatePrimitive соответствующего примитива
    /// падает уже в плеере: «class 'SphereCollider' doesn't exist». Ровно это
    /// наблюдалось на плафоне лампы (760fbeaa) и ровно ради стержня стрелки
    /// ресайза в Assets/link.xml лежит CapsuleCollider (b8d650e5, на следующий
    /// день после «feat: resize handles»).
    ///
    /// Вырезаются НЕ примитивы: CreatePrimitive(Cube) строит каждую деталь сцены
    /// и в собранном плеере работает — BoxCollider назван в полутора десятках
    /// файлов, и линкер оставляет его. Условие ровно одно: у каждого
    /// используемого примитива класс его коллайдера должен быть удержан — либо
    /// управляемой ссылкой в исходниках, либо строкой в link.xml.
    ///
    /// Тест проверяет это условие по ИСХОДНИКАМ; сам стриппинг он не запускает —
    /// тот виден только сборкой под WebGL. Но удержание класса и есть то, чем
    /// сборка оказывается рабочей.</summary>
    public class WebGLPrimitiveStrippingTests
    {
        private static readonly (string primitive, string collider)[] ImpliedCollider =
        {
            ("Cube", "BoxCollider"),
            ("Sphere", "SphereCollider"),
            ("Capsule", "CapsuleCollider"),
            ("Cylinder", "CapsuleCollider"),
            ("Plane", "MeshCollider"),
            ("Quad", "MeshCollider"),
        };

        private static readonly Regex PrimitiveLiteral =
            new Regex(@"PrimitiveType\s*\.\s*(\w+)", RegexOptions.Compiled);

        private static readonly Regex PrimitiveCall =
            new Regex(@"CreatePrimitive\s*\(", RegexOptions.Compiled);

        private static string ProductionDir() => RepoPaths.Subdir("Assets", "Scripts");

        private static string LinkXml() => Path.Combine(RepoPaths.Subdir("Assets"), "link.xml");

        private static bool IsComment(string line)
        {
            var t = line.TrimStart();
            return t.StartsWith("//", StringComparison.Ordinal)
                || t.StartsWith("*", StringComparison.Ordinal)
                || t.StartsWith("/*", StringComparison.Ordinal);
        }

        private static List<(string file, string[] lines)>? _sources;

        private static List<(string file, string[] lines)> Sources() =>
            _sources ??= Directory.GetFiles(ProductionDir(), "*.cs", SearchOption.AllDirectories)
                .Select(f => (Path.GetFileName(f) ?? string.Empty, File.ReadAllLines(f))).ToList();

        private static List<(string file, int line, string primitive)> Usages()
        {
            var found = new List<(string, int, string)>();

            foreach (var (file, lines) in Sources())
                for (int i = 0; i < lines.Length; i++)
                {
                    if (IsComment(lines[i])) continue;
                    foreach (Match m in PrimitiveLiteral.Matches(lines[i]))
                        found.Add((file, i + 1, m.Groups[1].Value));
                }

            return found;
        }

        private static List<string> FilesCallingCreatePrimitive() =>
            Sources().Where(s => s.lines.Any(l => !IsComment(l) && PrimitiveCall.IsMatch(l)))
                .Select(s => s.file).ToList();

        private static bool NamedInSources(string collider)
        {
            foreach (var (_, lines) in Sources())
                foreach (var line in lines)
                {
                    if (IsComment(line) || PrimitiveCall.IsMatch(line)) continue;
                    if (line.Contains(collider, StringComparison.Ordinal)) return true;
                }

            return false;
        }

        private static bool PreservedInLinkXml(string collider) =>
            File.ReadAllText(LinkXml()).Contains("UnityEngine." + collider, StringComparison.Ordinal);

        [Test]
        public void EveryPrimitiveUsed_HasItsColliderClassKeptAliveForWebGL()
        {
            var map = ImpliedCollider.ToDictionary(p => p.primitive, p => p.collider, StringComparer.Ordinal);
            var unheld = new List<string>();

            foreach (var use in Usages())
            {
                if (!map.TryGetValue(use.primitive, out var collider)) continue;
                if (NamedInSources(collider) || PreservedInLinkXml(collider)) continue;

                unheld.Add(use.file + ":" + use.line + " — " + use.primitive
                    + " тянет " + collider + ", а класс ничем не удержан");
            }

            Assert.IsEmpty(unheld,
                "Примитив строится классом коллайдера, которого в WebGL-сборке не останется: "
                + "стриппинг вырезает коллайдер без управляемых ссылок, и CreatePrimitive падает "
                + "в плеере (class ... doesn't exist). Удержи класс явным AddComponent или "
                + "строкой в Assets/link.xml — либо собирай меш вручную, как TextureOverlayHandles:\n"
                + string.Join("\n", unheld));
        }

        [Test]
        public void EveryPrimitiveUsed_IsNamedInTheColliderMap()
        {
            var known = ImpliedCollider.Select(p => p.primitive).ToHashSet(StringComparer.Ordinal);
            var unknown = Usages().Where(u => !known.Contains(u.primitive))
                .Select(u => u.file + ":" + u.line + " — " + u.primitive).Distinct().ToList();

            Assert.IsEmpty(unknown,
                "новый PrimitiveType в исходниках — допиши в таблицу, какой коллайдер он тянет, "
                + "иначе сторож молча его пропустит:\n" + string.Join("\n", unknown));
        }

        [Test]
        public void EveryFileCallingCreatePrimitive_NamesThePrimitiveItAsksFor()
        {
            var namesAPrimitive = Usages().Select(u => u.file).ToHashSet(StringComparer.Ordinal);
            var blind = FilesCallingCreatePrimitive().Where(f => !namesAPrimitive.Contains(f)).ToList();

            Assert.IsEmpty(blind,
                "аргумент CreatePrimitive не разрешается по исходнику — сторож не видит, какой "
                + "коллайдер тянет этот вызов, и молчит там, где обязан ругаться. CooktopMesh "
                + "зовёт CreatePrimitive(ChildPrimitive(idx)) и попадает в скан только потому, "
                + "что оба литерала PrimitiveType лежат в том же файле:\n" + string.Join("\n", blind));
        }

        [Test]
        public void TheScan_SeesTheCylinderOfTheResizeArrow_AndTheCubeOfEveryPart()
        {
            var usages = Usages();

            Assert.IsNotEmpty(usages, "скан не нашёл ни одного PrimitiveType — он смотрит не туда");
            Assert.IsTrue(usages.Any(u => u.file == "ResizeHandleManager.cs" && u.primitive == "Cylinder"),
                "стержень стрелки ресайза — тот самый случай, ради которого написан этот тест: "
                + "Cylinder тянет CapsuleCollider, на который в исходниках ссылок НЕТ, "
                + "и держит его только строка в Assets/link.xml");
            Assert.IsTrue(usages.Any(u => u.primitive == "Cube"),
                "Cube строит каждую деталь сцены — если он пропал из скана, сломан скан");
        }

        [Test]
        public void TheScan_TellsAHeldColliderFromAStrippedOne()
        {
            Assert.IsTrue(NamedInSources("BoxCollider"),
                "положительный контроль: BoxCollider назван в исходниках, поэтому Cube безопасен");
            Assert.IsFalse(NamedInSources("CapsuleCollider"),
                "CapsuleCollider в исходниках НЕ назван; появилась ссылка — link.xml перестал "
                + "быть единственным держателем, и условие этого теста изменилось");
            Assert.IsTrue(PreservedInLinkXml("CapsuleCollider"),
                "эта строка link.xml и держит стрелки ресайза живыми в WebGL-сборке");
            Assert.IsFalse(PreservedInLinkXml("BoxCollider"),
                "отрицательный контроль: чтение link.xml не отвечает «да» на что угодно");
        }
    }
}
