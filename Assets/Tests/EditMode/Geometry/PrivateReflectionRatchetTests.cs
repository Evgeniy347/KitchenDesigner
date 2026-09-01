using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace KitchenDesigner.Tests.Geometry
{
    /// <summary>Сторож части A4: тест не лезет рефлексией в приватные члены
    /// (CONVENTIONS.md → «How a test reaches the thing it tests»). Такой тест
    /// проверяет УСТРОЙСТВО, а не поведение: имя приватного члена становится
    /// публичным API, переименовать и передвинуть его больше нельзя, а при
    /// промахе тест зеленеет на NullReferenceException вместо того, чтобы
    /// краснеть по делу.
    ///
    /// Считается ДВА разных признака, и они не одно и то же:
    ///  • ЗАХОД ВНУТРЬ — привязка BindingFlags.NonPublic, GetSetMethod/GetGetMethod
    ///    с nonPublic: true, Type.InvokeMember и SendMessage/BroadcastMessage
    ///    (последним зовут приватный Awake, и никакого BindingFlags в тексте при
    ///    этом нет — без отдельного образца форма невидима);
    ///  • ОБРАЩЕНИЕ ПО ИМЕНИ (GetField("_x") и родня) — то, что и замораживает
    ///    имя. Перечисление полей без имени этого не делает: переименовали поле —
    ///    обзор по-прежнему зелен и по-прежнему прав, и такой приём CONVENTIONS
    ///    прямо предписывает для сверки списков полей.
    ///
    /// Рефлексия по ПУБЛИЧНЫМ членам нарушением не является и в счёт не идёт —
    /// на ней держатся сами сторожа (обход всех типов элементов, поиск атрибута).
    /// Поэтому файл попадает в счёт только при ненулевом ЗАХОДЕ ВНУТРЬ.
    ///
    /// Счёт ТЕКСТОВЫЙ и по файлу: сканер не связывает конкретный Get* с
    /// конкретными флагами. Где это даёт слепое пятно, сказано в причине записи.
    ///
    /// Форма — храповик: превысил потолок — красное с именами строк, опустился
    /// ниже — тоже красное, с просьбой опустить число. Файл, вычищенный до нуля,
    /// исчезает из скана целиком, поэтому обход идёт по ОБЪЕДИНЕНИЮ найденного и
    /// записанного — иначе последняя починка осталась бы незамеченной.</summary>
    public class PrivateReflectionRatchetTests
    {
        private static readonly (string file, int reachIns, int named, string why)[] Budgets =
        {
            ("EditMode/ContextMenuUndoTests.cs", 1, 4,
                "перечисляет приватные поля панели по именам и зовёт приватный Apply — "
                + "ровно тот случай, из-за которого в ContextMenuUI замерли переименования. "
                + "Лечится доступом к виджетам по имени объекта сцены, тем путём, которым "
                + "ходит пользователь. Core/UI сейчас чистит другой агент"),
            ("EditMode/Pure/KitchenSettingsContractTests.cs", 1, 0,
                "ОБЗОР, а не обращение по имени: перечисляет приватные поля, чтобы сверить "
                + "список настроек со списком полей файла сохранения. Ни одного имени не "
                + "заморожено, переименование поля тест не ломает — CONVENTIONS этот приём "
                + "предписывает. Долгом не является, потолок стоит, чтобы обзор не оброс "
                + "обращениями по имени"),
            ("EditMode/Pure/KitchenSettingsTests.cs", 1, 1,
                "обнуляет приватное статическое _instance, чтобы сбросить синглтон между "
                + "тестами. Лечится internal-методом сброса на KitchenSettings; класс лежит "
                + "в Core/Pure, который сейчас правит другой агент"),
            ("EditMode/TextureOverlayTests.cs", 1, 1,
                "зовёт приватный Update у DropdownHover — сообщение, которое EditMode не "
                + "шлёт. Лечится internal-входом или переносом в PlayMode; DropdownHover "
                + "лежит в Core/UI"),
            ("EditMode/ToastNotificationTests.cs", 2, 0,
                "зовёт приватные Awake и OnDestroy и подменяет приватный сеттер Instance. "
                + "ИМЯ метода приезжает параметром, поэтому именованных обращений сканер "
                + "здесь не видит: ноль в этой колонке — не заслуга файла, а слепое пятно "
                + "текстового счёта. Лечится так же, как вылечен SelectionManager.Instance "
                + "(internal-сеттер); ToastNotification лежит в Core/UI"),
        };

        private const string ReachInPattern =
            @"(?<![\w])NonPublic(?![\w])"
            + @"|Get(?:Set|Get)Method\s*\(\s*nonPublic\s*:\s*true"
            + @"|(?<![\w])InvokeMember\s*\("
            + @"|(?<![\w])(?:SendMessage|SendMessageUpwards|BroadcastMessage)\s*\(";

        private const string NamedLookupPattern = @"Get(?:Field|Method|Property)\s*\(\s*""";

        private static string TestsDir() => RepoPaths.Subdir("Assets", "Tests");

        private const string OwnFileName = "Geometry/PrivateReflectionRatchetTests.cs";

        private const string SurveyFile = "EditMode/Pure/KitchenSettingsContractTests.cs";

        private const string NamedFile = "EditMode/ContextMenuUndoTests.cs";

        private static readonly string[] PublicReflectionFiles =
        {
            "EditMode/FacadeDoorWireNameTests.cs",
            "EditMode/MeasurePlaneHitTests.cs",
        };

        public static int CountReachIns(IEnumerable<string> lines) =>
            Count(SourceLines.CodeOnly(lines), ReachInPattern);

        public static int CountNamedLookups(IEnumerable<string> lines) =>
            Count(SourceLines.WithoutComments(lines), NamedLookupPattern);

        private static int Count(IEnumerable<string> lines, string pattern)
        {
            int n = 0;
            foreach (var line in lines)
                n += Regex.Matches(line, pattern).Count;
            return n;
        }

        private static Dictionary<string, (int reachIns, int named)> Scan()
        {
            var root = TestsDir();
            var counts = new Dictionary<string, (int reachIns, int named)>(StringComparer.Ordinal);

            foreach (var file in Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories))
            {
                var lines = File.ReadAllLines(file);
                int reachIns = CountReachIns(lines);
                if (reachIns == 0) continue;
                counts[Relative(root, file)] = (reachIns, CountNamedLookups(lines));
            }

            return counts;
        }

        private static string Relative(string root, string file) =>
            file.Substring(root.Length)
                .TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                .Replace(Path.DirectorySeparatorChar, '/');

        private static (int reachIns, int named) Ceiling(string file)
        {
            foreach (var (name, reachIns, named, _) in Budgets)
                if (string.Equals(name, file, StringComparison.Ordinal)) return (reachIns, named);
            return (0, 0);
        }

        private static string Row(string file, (int reachIns, int named) found,
            (int reachIns, int named) ceiling) =>
            file + ": заходов внутрь " + found.reachIns + " при потолке " + ceiling.reachIns
            + ", обращений по имени " + found.named + " при потолке " + ceiling.named;

        [Test]
        public void NoTest_ReachesIntoAPrivateMember_BeyondTheRecordedDebt()
        {
            var scan = Scan();
            var files = new SortedSet<string>(scan.Keys, StringComparer.Ordinal);
            foreach (var (name, _, _, _) in Budgets) files.Add(name);

            var over = new List<string>();
            var under = new List<string>();

            foreach (var file in files)
            {
                (int reachIns, int named) found = scan.TryGetValue(file, out var v) ? v : (0, 0);
                var ceiling = Ceiling(file);

                if (found.reachIns > ceiling.reachIns || found.named > ceiling.named)
                    over.Add(Row(file, found, ceiling));
                else if (found.reachIns < ceiling.reachIns || found.named < ceiling.named)
                    under.Add(Row(file, found, ceiling));
            }

            Assert.IsEmpty(over,
                "тест, читающий приватный член, проверяет устройство, а не поведение: имя "
                + "члена замирает как публичное API, а промах даёт зелень на "
                + "NullReferenceException. Путь наружу — вести объект так, как его ведёт "
                + "пользователь, или сделать член internal (InternalsVisibleTo для "
                + "EditMode уже выдан в Core/AssemblyInfo.cs). Найдено:\n"
                + string.Join("\n", over));

            Assert.IsEmpty(under,
                "рефлексии стало меньше — опусти потолок тем же коммитом, иначе храповик "
                + "отдаст назад вычищенное:\n" + string.Join("\n", under));
        }

        [Test]
        public void TheSurveyOfFieldNames_IsNotCountedAsReachingIn()
        {
            var scan = Scan();

            Assert.AreEqual(0, scan[SurveyFile].named,
                "сверка списков полей перечисляет их без имён — она ничего не замораживает "
                + "и долгом не считается, иначе сторож гнал бы приём, который CONVENTIONS "
                + "предписывает");
            Assert.Greater(scan[NamedFile].named, 0,
                "а подстановка полей по именам — считается: контраст к предыдущему, без "
                + "него сторож мог бы не считать вообще ничего");
        }

        [Test]
        public void ReflectionOverPublicMembers_IsNotCountedAtAll()
        {
            var scan = Scan();

            foreach (var file in PublicReflectionFiles)
                CollectionAssert.DoesNotContain(scan.Keys, file,
                    "рефлексия по ПУБЛИЧНОМУ члену — не заход внутрь: так устроены и сами "
                    + "сторожа (обход типов элементов, поиск атрибута). Запретив её, сторож "
                    + "запретил бы приём, на котором стоит половина набора. Попал в счёт: "
                    + file);
        }

        [Test]
        public void TheScan_SeesTheTests_AndItsOwnPatternsDoNotCountThemselves()
        {
            var scan = Scan();

            Assert.GreaterOrEqual(scan.Count, Budgets.Length,
                "скан обязан видеть по меньшей мере все файлы, что записаны потолками — "
                + "пустой или укоротившийся результат означает грепнутый мимо каталог, а "
                + "не вычищенный набор");
            CollectionAssert.Contains(scan.Keys, NamedFile,
                "известный нарушитель обязан быть в скане: без него «ноль находок» нельзя "
                + "отличить от грепа мимо каталога");
            CollectionAssert.DoesNotContain(scan.Keys, OwnFileName,
                "сам сторож держит свои образцы в строковых литералах, а литералы сканер "
                + "вырезает: попади он в счёт, храповик считал бы собственный текст");
        }

        [Test]
        public void TheScan_TellsRealReflectionFromATextThatLooksLikeIt()
        {
            Assert.AreEqual(1, CountReachIns(
                new[] { "BindingFlags.NonPublic | BindingFlags.Instance" }),
                "приватная привязка — основная форма");
            Assert.AreEqual(1, CountReachIns(
                new[] { "prop.GetSetMethod(nonPublic: true).Invoke(o, a)" }),
                "приватный сеттер достают без единого BindingFlags в тексте");
            Assert.AreEqual(1, CountReachIns(
                new[] { "t.InvokeMember(\"_x\", f, null, o, null)" }),
                "InvokeMember — тот же заход внутрь другой дверью");
            Assert.AreEqual(1, CountReachIns(
                new[] { "go.SendMessage(\"Awake\");" }),
                "SendMessage — способ позвать приватный Awake в обход всего");

            Assert.AreEqual(0, CountReachIns(
                new[] { "var setter = p.GetSetMethod(nonPublic: false);" }),
                "nonPublic: false — противоположный запрос, ровно так UndoableCoverageTests "
                + "перебирает ПУБЛИЧНЫЕ сеттеры; спутать их значит запретить сам приём");
            Assert.AreEqual(0, CountReachIns(
                new[] { "        // BindingFlags.NonPublic" }),
                "закомментированная привязка нарушением не является");
            Assert.AreEqual(0, CountReachIns(
                new[] { "        var s = \"NonPublic\";" }),
                "и текст в строке тоже");
            Assert.AreEqual(0, CountReachIns(
                new[] { "        var x = NotNonPublicAtAll;" }),
                "слово внутри другого слова — не привязка");
            Assert.AreEqual(0, CountReachIns(
                new[] { "        bus.DispatchSendMessages(list);" }),
                "и SendMessage внутри другого имени — не сообщение Unity");

            Assert.AreEqual(1, CountNamedLookups(
                new[] { "typeof(T).GetField(\"_x\", f)" }),
                "обращение по имени — то, что замораживает имя");
            Assert.AreEqual(0, CountNamedLookups(
                new[] { "typeof(T).GetFields(f)" }),
                "перечисление без имени имя не замораживает");
            Assert.AreEqual(0, CountNamedLookups(
                new[] { "typeof(T).GetMethod(name, f)" }),
                "имя из переменной сканер не видит — это его слепое пятно, и оно названо "
                + "в причине записи EditMode/ToastNotificationTests.cs");
        }

        [Test]
        public void EveryBudgetRow_NamesAnExistingFile_AndExplainsTheDebt()
        {
            var root = TestsDir();
            var found = new HashSet<string>(StringComparer.Ordinal);
            foreach (var file in Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories))
                found.Add(Relative(root, file));

            foreach (var (file, reachIns, named, why) in Budgets)
            {
                Assert.IsTrue(found.Contains(file),
                    "потолок для " + file + " пережил свой файл: запись начнёт молча "
                    + "освобождать следующий файл с этим путём");
                Assert.IsNotEmpty(why,
                    "потолок " + reachIns + "/" + named + " у " + file + " без причины "
                    + "через полгода не отличить от недосмотра");
            }
        }
    }
}
