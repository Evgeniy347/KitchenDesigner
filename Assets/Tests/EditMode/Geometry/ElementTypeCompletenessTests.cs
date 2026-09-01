using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace KitchenDesigner.Tests.Geometry
{
    /// <summary>Сторож полноты при добавлении нового типа элемента —
    /// CONVENTIONS.md → «Adding a new element type», HARDENING-PLAN.md → A7.
    ///
    /// Правило Open/Closed здесь звучит так: новый тип обязан трогать ТОЛЬКО
    /// реестры и никогда лестницу if. Чеклист из восьми пунктов записан в
    /// CONVENTIONS.md и до сих пор держался на памяти: забыл пункт — узнаешь от
    /// пользователя. Пропуск не падает, он МОЛЧИТ: копия выходит обычной
    /// доской, элемент не переживает сохранения, массовый селектор считает его
    /// «board», MCP отвечает отказом.
    ///
    /// Список типов не записан здесь руками — он ВЫВОДИТСЯ из исходников:
    /// всякий класс, чья цепочка наследования доходит до KitchenElement.
    /// Поэтому новый тип попадает под правило сам, без правки этого теста.
    ///
    /// Ключ у каждого места свой и выведен из кода, а не из чеклиста: фабрика
    /// зовётся строго Create + имя типа без суффикса Element (это соблюдают все
    /// шестнадцать типов, поэтому проверять можно механически), а реестры,
    /// которые ветвятся ПО ТИПУ, обязаны назвать сам класс.
    ///
    /// Тест читает ИСХОДНИКИ и потому живёт в каталоге, который собирается
    /// вторым проходом под dotnet: быстрый цикл видит пропуск за 0,3 с.</summary>
    public class ElementTypeCompletenessTests
    {
        /// <summary>Чем именно место регистрирует тип.</summary>
        private enum Key
        {
            /// <summary>Ветвление по типу: место обязано НАЗВАТЬ класс.</summary>
            Names,

            /// <summary>Место обязано ПОЗВАТЬ фабричный метод CreateXxx.</summary>
            Creates,

            /// <summary>Достаточно любого из двух: слой заводит тип либо через
            /// фабрику, либо собирая компонент по имени класса.</summary>
            NamesOrCreates,
        }

        /// <summary>Обязательные места, каждое с причиной. Корень — либо файл,
        /// либо каталог: каталог берётся там, где реестр сейчас переезжает и имя
        /// файла устойчивым якорем быть не может.</summary>
        private static readonly (string place, string[] root, Key key, string why)[] Places =
        {
            ("фабрика",
             new[] { "Assets", "Scripts", "Core", "Elements", "ElementFactoryInstance.cs" },
             Key.Creates,
             "пункт 2 чеклиста: без CreateXxx тип нельзя создать вообще ничем"),

            ("реестр дублирования",
             new[] { "Assets", "Scripts", "Core", "Elements", "ElementDuplicators.cs" },
             Key.Names,
             "пропущенная запись не падает: «дублировать» молча возвращает обычную доску — "
             + "так уже было найдено три дефекта разом, коммит 6b819773"),

            ("восстановление сцены",
             new[] { "Assets", "Scripts", "Core", "Persistence" },
             Key.Creates,
             "пункт 3 чеклиста: без ветки в ElementRestorers элемент загружается обычной "
             + "деталью и правки пользователя пропадают молча. Корнем взят КАТАЛОГ, а не файл: "
             + "персистентность прямо сейчас разъезжается по двум каталогам, и имя файла "
             + "устойчивым якорем быть не может. Сторону ЗАПИСИ (FromElement) отдельным местом "
             + "не проверяем: восстановить тип, которого нет в снимке, всё равно нечем"),

            ("селектор массовых операций",
             new[] { "Assets", "Scripts", "Core", "Bulk", "ElementSelector.cs" },
             Key.Names,
             "ElementSelector.TypeOf — лестница с замыкающим «board»: забытый тип не "
             + "отказывает, а притворяется доской и попадает в чужую выборку"),

            ("слой MCP",
             new[] { "Assets", "Scripts", "Core", "MCP" },
             Key.NamesOrCreates,
             "пункт 6 чеклиста: типа, которого нет в ElementSpawners, для агента не "
             + "существует — create_elements отвечает отказом со списком разрешённых"),

            ("изометрический скриншот-тест",
             new[] { "Assets", "Tests", "PlayMode", "IsoScreenshotTests.cs" },
             Key.Creates,
             "пункт 5 чеклиста, помечен REQUIRED: единственная визуальная регрессия на "
             + "геометрию элемента"),
        };

        /// <summary>Долг, а не послабление правила: храповик. Запись обязана
        /// называть существующее место и существующий тип, а закрытая дыра
        /// обязана из списка ИСЧЕЗНУТЬ — иначе потолок переживает свой долг и
        /// молча освобождает следующий тип, который в эту дыру попадёт.</summary>
        private static readonly (string place, string type, string why)[] KnownGaps =
        {
            ("слой MCP", "LightSourceElement",
             "светильник заводится только из сайдбара; заводить ли его через MCP — решение о "
             + "продукте, а не пропущенная строка"),
        };

        private static string ShortNameOf(string type) => ElementTypeCatalog.ShortNameOf(type);

        private static List<string> ElementTypes() => ElementTypeCatalog.FromSources();

        private static string[] FilesOf(string[] root)
        {
            var last = root[root.Length - 1];

            if (last.EndsWith(".cs", StringComparison.Ordinal))
            {
                var dir = RepoPaths.Subdir(root.Take(root.Length - 1).ToArray());
                var path = Path.Combine(dir, last);
                return File.Exists(path) ? new[] { path } : Array.Empty<string>();
            }

            return Directory.GetFiles(RepoPaths.Subdir(root), "*.cs", SearchOption.AllDirectories);
        }

        /// <summary>Код места одной строкой, без комментариев и без содержимого
        /// строковых литералов: упоминание типа в тексте подсказки MCP — это не
        /// регистрация, а обещание.</summary>
        private static string CodeOf(string[] root)
        {
            var sb = new StringBuilder();
            foreach (var file in FilesOf(root))
                foreach (var line in SourceLines.CodeOnly(File.ReadAllLines(file)))
                {
                    sb.Append(line);
                    sb.Append('\n');
                }
            return sb.ToString();
        }

        private static bool Covered(string code, string type, Key key)
        {
            if (key != Key.Creates && Regex.IsMatch(code, @"\b" + Regex.Escape(type) + @"\b"))
                return true;
            if (key != Key.Names
                && Regex.IsMatch(code, @"\bCreate" + Regex.Escape(ShortNameOf(type)) + @"\s*\("))
                return true;
            return false;
        }

        private static List<(string place, string type)> Gaps()
        {
            var types = ElementTypes();
            var gaps = new List<(string place, string type)>();

            foreach (var (place, root, key, _) in Places)
            {
                var code = CodeOf(root);
                foreach (var type in types)
                    if (!Covered(code, type, key)) gaps.Add((place, type));
            }

            return gaps;
        }

        private static string Describe(IEnumerable<(string place, string type)> pairs) =>
            string.Join("\n", pairs.Select(p => "    " + p.type + " — " + p.place));

        [Test]
        public void EveryElementType_IsRegisteredInEveryPlace_ExceptTheRecordedDebt()
        {
            var gaps = Gaps();
            var known = KnownGaps.Select(g => (place: g.place, type: g.type)).ToList();

            var appeared = gaps.Except(known).ToList();
            var closed = known.Except(gaps).ToList();

            Assert.IsEmpty(appeared,
                "Новый тип элемента обязан попасть во ВСЕ реестры сразу — иначе пропуск не "
                + "падает, а молчит: копия выходит доской, элемент не переживает сохранения, "
                + "MCP отвечает отказом. Не зарегистрирован:\n"
                + Describe(appeared)
                + "\nЗакрыть пропуск или, если это осознанный долг, завести запись в KnownGaps "
                + "с причиной — но НЕ ослаблять правило.");

            Assert.IsEmpty(closed,
                "Долг закрыт, а потолок остался: запись в KnownGaps пережила свою дыру и теперь "
                + "молча освобождает следующий тип, который туда попадёт. Убрать из KnownGaps:\n"
                + Describe(closed));
        }

        /// <summary>Грепу по несуществующему пути нечего найти — он зеленеет,
        /// ничего не проверив. Проверяем, что список типов действительно выведен,
        /// а каждое место резолвится в файлы.</summary>
        [Test]
        public void TheScan_ActuallyFindsTheTypesAndEveryPlace()
        {
            var types = ElementTypes();

            CollectionAssert.Contains(types, "DrawerElement", "ящик — тип элемента");
            CollectionAssert.Contains(types, "AssembledFacadeElement",
                "наследник через промежуточный класс обязан попасть в список: сборный фасад "
                + "наследует FacadeElement, а не базу напрямую");
            CollectionAssert.DoesNotContain(types, ElementTypeCatalog.BaseType,
                "база — это и есть «доска», отдельным типом она не считается");
            CollectionAssert.DoesNotContain(types, "Wall",
                "стена не наследник базы: это отдельный компонент на том же объекте");
            Assert.GreaterOrEqual(types.Count, 16,
                "типов элементов в проекте шестнадцать; меньше — значит разбор наследования сломался");

            foreach (var (place, root, _, _) in Places)
                Assert.IsNotEmpty(FilesOf(root),
                    "место «" + place + "» не резолвится ни в один файл: сторож ослеп, а не позеленел");
        }

        /// <summary>Положительный контроль: сопоставитель обязан УМЕТЬ находить
        /// регистрацию, иначе «дыр нет» означало бы «я ничего не вижу». Обратную
        /// сторону — что он не находит её всегда — держит сам храповик: каждая
        /// запись KnownGaps обязана воспроизводиться.</summary>
        [Test]
        public void TheMatcher_FindsATypeThatIsRegisteredEverywhere()
        {
            var missing = new List<string>();
            foreach (var (place, root, key, _) in Places)
                if (!Covered(CodeOf(root), "DrawerElement", key)) missing.Add(place);

            Assert.IsEmpty(missing,
                "ящик заведён во всех местах чеклиста и обязан находиться в каждом; не найден в: "
                + string.Join(", ", missing));
        }

        [Test]
        public void TheMatcher_RecognisesTheShapeOfARegistration()
        {
            Assert.IsTrue(Covered("var oven = go.AddComponent<OvenElement>();", "OvenElement", Key.Names),
                "названный класс — это регистрация в реестре, который ветвится по типу");
            Assert.IsTrue(Covered("public static object CreateOven(string name)", "OvenElement", Key.Creates),
                "фабричный метод зовётся Create + имя типа без суффикса Element");
            Assert.IsFalse(Covered("factory.CreateOven(d.name, d.Position);", "OvenElement", Key.Names),
                "вызов фабрики класс не называет: у ключа Names это НЕ регистрация");
            Assert.IsFalse(Covered("var table = go.AddComponent<TableElement>();", "RadiusTableElement", Key.Names),
                "TableElement не должен засчитываться за RadiusTableElement — иначе подтип "
                + "прячется за базовым именем");
            Assert.IsFalse(Covered("factory.CreateRadiusTable(dims);", "TableElement", Key.Creates),
                "CreateRadiusTable не должен засчитываться за CreateTable");
            Assert.IsFalse(Covered("go.AddComponent<AssembledFacadeElement>();", "FacadeElement", Key.Names),
                "AssembledFacadeElement не содержит отдельного слова FacadeElement");
        }

        [Test]
        public void EveryRecordedGap_NamesARealPlaceAndARealType_AndExplainsWhy()
        {
            var places = Places.Select(p => p.place).ToList();
            var types = ElementTypes();

            foreach (var (place, type, why) in KnownGaps)
            {
                CollectionAssert.Contains(places, place,
                    "долг числится за местом, которого нет в списке обязательных: " + place);
                CollectionAssert.Contains(types, type,
                    "долг числится за типом, которого больше нет: " + type);
                Assert.IsNotEmpty(why,
                    "долг без причины через полгода не отличить от недосмотра: " + type + " — " + place);
            }
        }

        [Test]
        public void EveryPlace_ExplainsWhyItIsMandatory()
        {
            foreach (var (place, _, _, why) in Places)
                Assert.IsNotEmpty(why, "место без причины превращает список в свалку: " + place);
        }
    }
}
