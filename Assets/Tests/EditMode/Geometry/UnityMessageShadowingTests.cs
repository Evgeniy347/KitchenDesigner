using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace KitchenDesigner.Tests.Geometry
{
    /// <summary>Сторож ловушки «сообщение Unity — не override»
    /// (CONVENTIONS.md → «<c>OnDestroy</c> is a Unity message, not an override»),
    /// обобщённый с одного <c>OnDestroy</c> на ВСЕ сообщения движка.
    ///
    /// Unity зовёт <c>Awake</c>, <c>OnDestroy</c>, <c>Update</c> и их родню ПО
    /// ИМЕНИ. Объявил подкласс своё — приватное базовое перестаёт вызываться:
    /// без предупреждения компилятора, без ключевого слова <c>override</c>,
    /// которое заметили бы в ревью. В этом проекте так молча пропала
    /// дерегистрация в <c>PartRegistry</c> сразу у двух классов, и уничтоженные
    /// элементы повисли в реестре.
    ///
    /// Правило здесь двухступенчатое, потому что таковы базовые классы:
    /// <list type="bullet">
    /// <item>если у пары (базовый класс, сообщение) записано, ЧТО обязан
    /// повторить наследник, — проверяется наличие этих вызовов в его теле;</item>
    /// <item>если не записано ничего — объявлять сообщение наследнику НЕЛЬЗЯ
    /// вовсе: <c>FacadeElement.Update</c> крутит анимацию двери, и подкласс,
    /// объявивший свой <c>Update</c>, просто остановит её.</item>
    /// </list>
    ///
    /// <c>override</c> сокрытием не является: раз базовый член виртуальный, цепочка
    /// цела, и такие объявления сторож пропускает.</summary>
    public class UnityMessageShadowingTests
    {
        private static readonly string[] UnityMessages =
        {
            "Awake", "Start", "OnEnable", "OnDisable", "OnDestroy",
            "Update", "LateUpdate", "FixedUpdate", "OnValidate", "Reset",
            "OnApplicationQuit", "OnApplicationPause", "OnApplicationFocus",
            "OnGUI", "OnTransformParentChanged", "OnTransformChildrenChanged",
            "OnBecameVisible", "OnBecameInvisible", "OnDrawGizmos",
            "OnRenderObject", "OnPreRender", "OnPostRender",
        };

        /// <summary>Что наследник ОБЯЗАН повторить, объявив своё сообщение поверх
        /// базового. Пара без записи запрещена целиком — см. описание класса.
        /// Причина обязательна, а <see cref="EveryRequiredRepeat_DescribesAMessageTheBaseActuallyDeclares"/>
        /// не даёт записи пережить тот метод, ради которого она написана.</summary>
        private static readonly (string baseClass, string message, string[] calls, string why)[] RequiredRepeats =
        {
            ("KitchenElement", "Awake", new[] { "PartRegistry.Register", "ApplyDimensions" },
                "базовый Awake строит геометрию и ставит элемент на учёт: без повтора элемент "
                + "родится без меша и невидимым для реестра"),
        };

        private static readonly string[][] ScannedDirs =
        {
            new[] { "Assets", "Scripts" },
            new[] { "Assets", "Editor" },
        };

        private sealed class TypeInfo
        {
            public string Name = string.Empty;
            public string BaseName = string.Empty;
            public string File = string.Empty;
            public readonly Dictionary<string, string> Messages = new Dictionary<string, string>();
        }

        private static readonly string ClassDeclPattern =
            @"\bclass\s+(?<name>\w+)\s*(?<generic><[^>{}()]*>)?\s*(?::\s*(?<base>[\w\.]+(?:<[^>{}()]*>)?))?";

        private static readonly string MessageDeclPrefix =
            @"(?<!\w)(?<mods>(?:\w+\s+)*)(?:void|IEnumerator)\s+";

        private static readonly string MessageDeclSuffix = @"\s*\(\s*\)\s*(?<open>\{|=>)";

        private static IEnumerable<string> SourceFiles()
        {
            foreach (var parts in ScannedDirs)
            {
                string dir;
                try { dir = RepoPaths.Subdir(parts); }
                catch (DirectoryNotFoundException) { continue; }

                foreach (var f in Directory.GetFiles(dir, "*.cs", SearchOption.AllDirectories))
                    yield return f;
            }
        }

        private static string BodyAt(string text, int braceIndex)
        {
            int depth = 0;
            for (int i = braceIndex; i < text.Length; i++)
            {
                if (text[i] == '{') depth++;
                else if (text[i] == '}')
                {
                    depth--;
                    if (depth == 0) return text.Substring(braceIndex, i - braceIndex + 1);
                }
            }
            return text.Substring(braceIndex);
        }

        private static void CollectMessages(string segment, TypeInfo type)
        {
            foreach (var message in UnityMessages)
            {
                if (segment.IndexOf(message, StringComparison.Ordinal) < 0) continue;

                var m = Regex.Match(segment, MessageDeclPrefix + message + MessageDeclSuffix);
                if (!m.Success) continue;
                if (m.Groups["mods"].Value.Split(' ', '\t', '\r', '\n').Contains("override")) continue;

                type.Messages[message] = m.Groups["open"].Value == "{"
                    ? BodyAt(segment, m.Groups["open"].Index)
                    : segment.Substring(m.Index, Math.Min(400, segment.Length - m.Index));
            }
        }

        private static Dictionary<string, TypeInfo>? _map;

        /// <summary>Разбор всех исходников стоит секунды, а за прогон не меняется:
        /// шесть тестов класса делят один разбор, иначе быстрый цикл под dotnet
        /// перестаёт быть быстрым.</summary>
        private static Dictionary<string, TypeInfo> TypeMap() => _map ??= BuildTypeMap();

        private static Dictionary<string, TypeInfo> BuildTypeMap()
        {
            var map = new Dictionary<string, TypeInfo>(StringComparer.Ordinal);

            foreach (var file in SourceFiles())
            {
                var text = File.ReadAllText(file);
                var decls = Regex.Matches(text, ClassDeclPattern).Cast<Match>().ToList();

                for (int i = 0; i < decls.Count; i++)
                {
                    var name = decls[i].Groups["name"].Value;
                    int from = decls[i].Index;
                    int to = i + 1 < decls.Count ? decls[i + 1].Index : text.Length;

                    if (!map.TryGetValue(name, out var type))
                    {
                        type = new TypeInfo { Name = name, File = Path.GetFileName(file) ?? string.Empty };
                        map[name] = type;
                    }

                    var baseName = decls[i].Groups["base"].Value;
                    if (baseName.Length > 0 && type.BaseName.Length == 0) type.BaseName = baseName;

                    CollectMessages(text.Substring(from, to - from), type);
                }
            }

            return map;
        }

        private static IEnumerable<TypeInfo> Ancestors(Dictionary<string, TypeInfo> map, TypeInfo type)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal) { type.Name };
            var current = type;

            while (current.BaseName.Length > 0 && map.TryGetValue(current.BaseName, out var parent))
            {
                if (!seen.Add(parent.Name)) yield break;
                yield return parent;
                current = parent;
            }
        }

        private static (string[] calls, bool known) Repeats(string baseClass, string message)
        {
            foreach (var (b, m, calls, _) in RequiredRepeats)
                if (b == baseClass && m == message) return (calls, true);
            return (Array.Empty<string>(), false);
        }

        private static List<string> Shadowings(Dictionary<string, TypeInfo> map, out List<string> checkedPairs)
        {
            var violations = new List<string>();
            var pairs = new List<string>();

            foreach (var type in map.Values)
            {
                foreach (var pair in type.Messages)
                {
                    foreach (var ancestor in Ancestors(map, type))
                    {
                        if (!ancestor.Messages.ContainsKey(pair.Key)) continue;

                        pairs.Add(type.Name + "." + pair.Key + " над " + ancestor.Name);

                        var (calls, known) = Repeats(ancestor.Name, pair.Key);
                        if (!known)
                        {
                            violations.Add(type.File + ": " + type.Name + " объявляет " + pair.Key
                                + ", который уже есть у " + ancestor.Name
                                + " — базовый БОЛЬШЕ НЕ ВЫЗОВЕТСЯ");
                            break;
                        }

                        var missing = calls.Where(c => !pair.Value.Contains(c)).ToArray();
                        if (missing.Length > 0)
                            violations.Add(type.File + ": " + type.Name + "." + pair.Key
                                + " закрыл собой " + ancestor.Name + "." + pair.Key
                                + ", но не повторил " + string.Join(", ", missing));
                        break;
                    }
                }
            }

            checkedPairs = pairs;
            return violations;
        }

        [Test]
        public void NoSubclass_SilentlyReplacesAUnityMessageOfItsBase()
        {
            var map = TypeMap();
            var violations = Shadowings(map, out _);

            Assert.IsEmpty(violations,
                "Unity зовёт сообщения ПО ИМЕНИ: объявив своё, подкласс закрыл приватное "
                + "базовое, и всё, что оно делало, молча исчезло. Либо повторите работу базового "
                + "класса в своём методе, либо не объявляйте сообщение вовсе. Найдено:\n"
                + string.Join("\n", violations));
        }

        /// <summary>Скан по несуществующему пути и сломанный regex прошли бы
        /// зелёными, ничего не проверив.</summary>
        [Test]
        public void TheScan_SeesTheBaseClassesAndTheirMessages()
        {
            var map = TypeMap();

            Assert.IsTrue(map.ContainsKey("KitchenElement"), "скан обязан видеть KitchenElement");
            CollectionAssert.Contains(map["KitchenElement"].Messages.Keys, "OnDestroy",
                "KitchenElement объявляет приватный OnDestroy — он и есть охраняемый базовый");
            CollectionAssert.Contains(map["KitchenElement"].Messages.Keys, "Awake");
            CollectionAssert.Contains(map["FacadeElement"].Messages.Keys, "Update",
                "FacadeElement крутит анимацию двери в приватном Update — второй охраняемый базовый");
            Assert.AreEqual("KitchenElement", map["FacadeElement"].BaseName,
                "скан обязан разбирать наследование, иначе цепочка предков пуста и всё зелено");
            Assert.AreEqual("FacadeElement", map["AssembledFacadeElement"].BaseName,
                "проверка должна доставать и НЕПРЯМЫХ предков");
        }

        private static Dictionary<string, TypeInfo> SyntheticMap(string subclassBody)
        {
            var baseType = new TypeInfo { Name = "Base", File = "Base.cs" };
            CollectMessages("class Base { private void OnDestroy() { Registry.Unregister(this); } }", baseType);

            var sub = new TypeInfo { Name = "Sub", BaseName = "Base", File = "Sub.cs" };
            CollectMessages("class Sub : Base { " + subclassBody + " }", sub);

            return new Dictionary<string, TypeInfo>(StringComparer.Ordinal)
            {
                ["Base"] = baseType,
                ["Sub"] = sub,
            };
        }

        /// <summary>После перевода уборки элементов на хук
        /// <c>KitchenElement.OnElementDestroyed</c> в дереве не осталось НИ ОДНОЙ
        /// пары «наследник закрыл сообщение базового», и предыдущая версия этой
        /// проверки — «в списке проверенных должны быть DoorElement и SinkElement» —
        /// стала невыполнимой. Ловилка обязана остаться доказуемой: детектор
        /// прогоняется на синтетической паре, которую он ОБЯЗАН найти.</summary>
        [Test]
        public void TheScan_ReportsASubclassThatShadowsAMessageNobodyAllowedIt()
        {
            var violations = Shadowings(SyntheticMap("private void OnDestroy() { }"), out var checkedPairs);

            CollectionAssert.Contains(checkedPairs, "Sub.OnDestroy над Base",
                "пара обязана попасть в проверенные, иначе сторож смотрит мимо");
            Assert.IsNotEmpty(violations,
                "пары (Base, OnDestroy) нет в RequiredRepeats — объявлять сообщение нельзя вовсе");
        }

        [Test]
        public void TheScan_LetsAnUnrelatedMessageThrough()
        {
            var violations = Shadowings(SyntheticMap("private void Update() { }"), out _);

            Assert.IsEmpty(violations,
                "базовый Update не объявлен — наследник ничего не закрывает и вправе завести свой");
        }

        /// <summary>Хук вместо повтора: пока он есть, у наследника нет ни одной
        /// причины объявлять свой OnDestroy, и запись-разрешение для этой пары
        /// удалена из RequiredRepeats. Исчезнет хук — вернётся и ловушка.</summary>
        [Test]
        public void KitchenElement_GivesSubclassesAHook_InsteadOfLettingThemDeclareOnDestroy()
        {
            var map = TypeMap();

            Assert.IsFalse(Repeats("KitchenElement", "OnDestroy").known,
                "разрешение шадоуить KitchenElement.OnDestroy снято: уборка идёт через хук");

            var source = File.ReadAllText(Path.Combine(
                RepoPaths.Subdir(new[] { "Assets", "Scripts", "Core", "Elements" }), "KitchenElement.cs"));

            StringAssert.Contains("protected virtual void OnElementDestroyed()", source,
                "хук — единственная замена запрещённому объявлению OnDestroy у наследника");
            StringAssert.Contains("OnElementDestroyed();", map["KitchenElement"].Messages["OnDestroy"],
                "базовый OnDestroy обязан звать хук, иначе уборка наследника не выполнится");
        }

        [Test]
        public void TheScan_RecognisesAnExpressionBodiedMessage()
        {
            var type = new TypeInfo();
            CollectMessages("class X : Y { private void Update() => StepDoor(Time.deltaTime); }", type);

            CollectionAssert.Contains(type.Messages.Keys, "Update",
                "сообщение через => объявлено ровно так же и закрывает базовое так же");
        }

        [Test]
        public void TheScan_LetsAnOverrideThrough()
        {
            var type = new TypeInfo();
            CollectMessages("class X : Y { protected override void OnValidate() { } }", type);

            CollectionAssert.DoesNotContain(type.Messages.Keys, "OnValidate",
                "override не сокрытие: базовый член виртуальный, цепочка цела");
        }

        [Test]
        public void EveryRequiredRepeat_DescribesAMessageTheBaseActuallyDeclares()
        {
            var map = TypeMap();

            foreach (var (baseClass, message, calls, why) in RequiredRepeats)
            {
                Assert.IsNotEmpty(why, "запись без причины через полгода не отличить от недосмотра: "
                    + baseClass + "." + message);
                Assert.IsNotEmpty(calls, "запись без обязательных вызовов разрешает пустое "
                    + "переопределение: " + baseClass + "." + message);
                Assert.IsTrue(map.ContainsKey(baseClass) && map[baseClass].Messages.ContainsKey(message),
                    "в списке числится " + baseClass + "." + message + ", которого больше нет — "
                    + "запись переживёт свой метод и молча разрешит сокрытие следующему");
                foreach (var call in calls)
                    Assert.IsTrue(map[baseClass].Messages[message].Contains(call),
                        baseClass + "." + message + " больше не делает " + call
                        + " — требование к наследникам устарело");
            }
        }
    }
}
