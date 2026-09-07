using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace KitchenDesigner.Tests.Geometry
{
    /// <summary>Храповик по цветовым литералам в UI (docs/UI-GUIDELINES.md, п. 10:
    /// «палитра только из UIStyle, локальные цветовые константы запрещены»).
    /// Правило записано давно и всё это время держалось только на памяти: две
    /// панели носят побайтово одинаковую копию цвета выделенной строки, а
    /// прокрутка трижды продублирована в трёх файлах.
    ///
    /// Потолок — на ФАЙЛ, и у каждой записи обязательна причина. Рост валит
    /// тест с именами файлов; падение валит тоже — незакрытый храповик отдаёт
    /// назад ровно то, что только что вычистили. Файл без записи обязан иметь
    /// ноль: новая панель начинает с чистого листа и берёт цвет из UIStyle.
    ///
    /// Сам UIStyle.cs не сканируется: он и ЕСТЬ палитра.</summary>
    public class UiColorLiteralTests
    {
        private const string PaletteFile = "UIStyle.cs";

        /// <summary>Что считается цветовым литералом. «#RRGGBB» ищем в строковых
        /// литералах тоже — TMP-разметка «&lt;color=#FFCC00&gt;» это тот же зашитый
        /// цвет, только мимо типа Color.</summary>
        private static readonly string Literal =
            @"\bnew\s+Color(32)?\s*\(|\bColorUtility\s*\.|color\s*=\s*#[0-9a-fA-F]{3,8}\b"
            + @"|\bColor\s*\.\s*(red|green|blue|yellow|cyan|magenta|gray|grey|white|black|clear)\b";

        /// <summary>Потолок на файл + причина, почему цвет всё ещё здесь.
        /// Причина обязательна: без неё через полгода не отличить осознанное
        /// исключение от недосмотра. Существование каждого файла проверяет
        /// <see cref="EveryBudgetLine_NamesAFileThatExists"/>.</summary>
        private static readonly (string file, int ceiling, string why)[] Budgets =
        {
            ("ConsoleOverlay.cs", 2,
                "оверлей разработчика (F9), собственный чёрный фон и зелёный текст консоли"),
            ("HierarchyPanelUI.cs", 6,
                "фон строк дерева: токена ряда в UIStyle нет. Полоса прокрутки и выделенный"
                + " ряд уже переехали в UIStyle.ScrollHandle/RowSelected — потолок опущен 8 -> 6"),
            ("IconFactory.cs", 5,
                "рисование иконок в текстуру: чернила и прозрачный фон, а не палитра панелей"),
            ("IssueTableView.cs", 2,
                "остаток после переезда полосы прокрутки в UIStyle.ScrollHandle: 3 -> 2"),
            ("ModuleEditBannerUI.cs", 2,
                "синяя плашка режима редактирования модуля: токена для неё в UIStyle нет"),
            ("MultiSelectDropdown.cs", 2,
                "невидимые ловушки кликов (alpha 0.01), а не цвет"),
            ("NameDropdownBinder.cs", 2,
                "Color.red для отвязанного имени — просится в UIStyle.HighlightError"),
            ("SidebarUI.cs", 1,
                "зелёный фон кнопки «закреплено»: токена состояния тоггла в UIStyle нет"),
            ("SpecificationPanelUI.cs", 3,
                "разделитель; полоса прокрутки ушла в UIStyle.ScrollHandle: 4 -> 3"),
            ("StatusBarUI.cs", 1,
                "красный индикатор выключенного автосохранения — просится в UIStyle.HighlightError"),
            ("ToolbarUI.cs", 2,
                "Color.white как «текстура вместо цвета» в свотче пипетки и ToHtmlStringRGB "
                + "для TMP-разметки бейджа: разметке нужен именно hex, а не Color"),
            ("UIFactory.cs", 7,
                "ColorBlock кнопок (умножители hover/pressed/disabled) и заливка ползунка: "
                + "в UIStyle нет ни блока состояний, ни цвета заполнения"),
            ("WindowDrag.cs", 1,
                "полностью прозрачная картинка-приёмник raycast, а не цвет"),
        };

        private static string UiDir() => RepoPaths.Subdir("Assets", "Scripts", "Core", "UI");

        public static int LiteralsIn(IEnumerable<string> lines) =>
            SourceLines.WithoutComments(lines).Sum(line => Regex.Matches(line, Literal).Count);

        private static Dictionary<string, int> CountsByFile()
        {
            var counts = new Dictionary<string, int>(StringComparer.Ordinal);

            foreach (var file in Directory.GetFiles(UiDir(), "*.cs", SearchOption.AllDirectories))
            {
                var name = Path.GetFileName(file);
                if (name == PaletteFile) continue;
                counts[name] = LiteralsIn(File.ReadAllLines(file));
            }

            return counts;
        }

        [Test]
        public void UiSources_TakeEveryColourFromUIStyle()
        {
            var counts = CountsByFile();
            var budget = Budgets.ToDictionary(b => b.file, b => b.ceiling, StringComparer.Ordinal);
            var grew = new List<string>();
            var shrank = new List<string>();

            foreach (var pair in counts.OrderBy(p => p.Key, StringComparer.Ordinal))
            {
                int ceiling = budget.TryGetValue(pair.Key, out int c) ? c : 0;
                if (pair.Value > ceiling) grew.Add(pair.Key + ": " + pair.Value + " > " + ceiling);
                else if (pair.Value < ceiling) shrank.Add(pair.Key + ": " + pair.Value + " < " + ceiling);
            }

            Assert.IsEmpty(grew,
                "Цвет в панели мимо UIStyle (docs/UI-GUIDELINES.md, п. 10). Добавь токен в "
                + "UIStyle и ссылайся на него: локальная копия расходится с палитрой молча — "
                + "так уже появились два байтово одинаковых цвета выделенной строки в разных "
                + "панелях. Файл без записи в Budgets обязан иметь ноль. Выросло:\n"
                + string.Join("\n", grew));

            Assert.IsEmpty(shrank,
                "Цвета вынесены в UIStyle — опусти потолок в Budgets тем же коммитом, иначе "
                + "храповик отдаст назад вычищенное:\n" + string.Join("\n", shrank));
        }

        [Test]
        public void TheCounter_SeesEveryFormOfAColour_AndIgnoresComments()
        {
            Assert.AreEqual(1, LiteralsIn(new[] { "img.color = new Color(0.1f, 0.2f, 0.3f, 1f);" }),
                "конструктор Color");
            Assert.AreEqual(1, LiteralsIn(new[] { "var px = new Color32(10, 20, 30, 255);" }),
                "Color32 — тот же зашитый цвет, только в байтах");
            Assert.AreEqual(1, LiteralsIn(new[] { "ColorUtility.TryParseHtmlString(s, out var c);" }));
            Assert.AreEqual(1, LiteralsIn(new[] { "label.color = Color.red;" }),
                "именованная константа движка — такой же цвет мимо палитры");
            Assert.AreEqual(1, LiteralsIn(new[] { "text.text = \"<color=#FFCC00>!</color>\";" }),
                "hex в TMP-разметке живёт в строковом литерале: его нельзя вырезать вместе "
                + "со строками, иначе сторож его не увидит");

            Assert.AreEqual(0, LiteralsIn(new[] { "// раньше тут был new Color(1f, 0f, 0f, 1f)" }),
                "в комментарии цвет упоминают, а не задают");
            Assert.AreEqual(0, LiteralsIn(new[] { "var url = \"https://example.com/#abcdef\";" }),
                "две косые в строке не начинают комментарий, а якорь ссылки из шести "
                + "шестнадцатеричных букв — не цвет: hex засчитывается только после «color=»");
            Assert.AreEqual(0, LiteralsIn(new[] { "img.color = UIStyle.Accent;" }),
                "ссылка на палитру — это и есть правильный способ");
        }

        [Test]
        public void TheScan_ActuallySeesTheUiLayer_AndDoesNotExemptThePaletteUsers()
        {
            var counts = CountsByFile();
            CollectionAssert.Contains(counts.Keys, "ContextMenuUI.cs",
                "скан не видит файлов Core/UI — грепу по несуществующему пути нечего найти, "
                + "и он зеленеет, ничего не проверив");
            CollectionAssert.Contains(counts.Keys, "UIFactory.cs");
            CollectionAssert.DoesNotContain(counts.Keys, PaletteFile,
                "UIStyle.cs — сама палитра, её цвета считать нечего");

            CollectionAssert.DoesNotContain(Budgets.Select(b => b.file).ToArray(), "ContextMenuUI.cs",
                "самая большая панель проекта обязана оставаться на нуле: если она попадёт "
                + "в белый список, правило перестанет что-либо значить");
        }

        [Test]
        public void EveryBudgetLine_NamesAFileThatExists()
        {
            var counts = CountsByFile();
            foreach (var (file, _, why) in Budgets)
            {
                CollectionAssert.Contains(counts.Keys, file,
                    "потолок для " + file + " пережил свой файл: запись начнёт молча освобождать "
                    + "следующий файл с этим именем");
                Assert.IsNotEmpty(why, "у записи " + file + " нет причины");
            }
        }
    }
}
