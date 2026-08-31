using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace KitchenDesigner.Tests.Geometry
{
    /// <summary>Сторож направления зависимостей между слоями
    /// (docs/HARDENING-PLAN.md → A6). Объявленный граф прост: UI знает про всех,
    /// про UI не знает никто. Одного ребра «Persistence → UI» хватает, чтобы ядро
    /// снова перестало собираться без Unity, а `dotnet test` со Stryker — работать;
    /// этот путь уже был молча сломан и чинился отдельным коммитом (5917ac94).
    ///
    /// Слой UI виден по имени: почти всё ядро живёт в одном пространстве имён
    /// <c>KitchenDesigner.Core</c>, а UI и MCP — в своих. Поэтому ссылка «снизу
    /// вверх» всегда выглядит одинаково: <c>using KitchenDesigner.Core.UI;</c> или
    /// квалификатор <c>UI.</c> — и ловится точно, без гадания по именам типов.
    ///
    /// Форма — ХРАПОВИК, как у <see cref="CommentRatchetTests"/>, а не запрет с
    /// нуля: ссылок на UI сегодня 30, одним коммитом их не убрать, а ждать
    /// «когда-нибудь» значит не иметь сторожа вовсе. Превысил потолок — красное с
    /// именами строк; опустился ниже — тоже красное, с просьбой опустить число.
    /// Второе не придирка: незакрытый храповик отдаёт назад ровно то, что только
    /// что вычистили.</summary>
    public class LayerDependencyDirectionTests
    {
        /// <summary>Потолок ссылок на слой UI для каждого слоя ядра. Ноль —
        /// нормальное состояние; у каждой ненулевой записи причина, почему долг
        /// ещё жив. Слой UI себя не проверяет, его здесь нет.</summary>
        private static readonly (string layer, int ceiling, string why)[] UiBudgets =
        {
            ("Analysis", 0, ""),
            ("Bulk", 0, ""),
            ("Commands", 1, "UndoHandler дёргает UIManager.SaveCurrent после отмены — долг, "
                + "лечится событием «сцена изменилась»"),
            ("Diagnostics", 0, ""),
            ("Elements", 2, "ElementMover показывает подсказку через StatusBarUI — долг, "
                + "лечится тем же событием"),
            ("Geometry", 0, "ядро: любая ссылка на UI ломает второй проход сборки под dotnet"),
            ("Infrastructure", 2, "Bootstrap — композиционный корень: он и обязан знать про UI, "
                + "чтобы его собрать"),
            ("Interfaces", 0, ""),
            ("MCP", 0, "MCP отвечает наружу данными и не вправе трогать панели"),
            ("Materials", 0, ""),
            ("Measure", 2, "размерные подписи берут глиф и стиль из UIStyle — долг"),
            ("Networking", 0, ""),
            ("Persistence", 4, "автосохранение и снимок сцены зовут StatusBarUI и ProjectWindows "
                + "— самый вредный из долгов: он же и делает Persistence непроверяемым без сцены"),
            ("Platform", 0, ""),
            ("Pure", 0, "чистый слой собирается вторым проходом и обязан жить без движка вовсе"),
            ("Rendering", 9, "CameraController открывает контекстное меню, SideHighlighter берёт "
                + "цвет из UIStyle — долг"),
            ("Snap", 6, "ручки растягивания показывают подсказки и берут цвета из UIStyle — долг"),
            ("Tools", 0, ""),
            ("Update", 4, "панели обновления сами по себе UI и живут не в том каталоге — долг, "
                + "лечится переездом, а не ссылкой"),
            ("Validation", 0, ""),
        };

        /// <summary>Слой MCP — контракт наружу; знать про него вправе только тот,
        /// кто его поднимает.</summary>
        private static readonly (string layer, int ceiling, string why)[] McpBudgets =
        {
            ("Infrastructure", 4, "Bootstrap поднимает мост MCP (TCP, WebSocket и перехват консоли) — "
                + "композиционный корень; больше про MCP не знает никто"),
        };

        private static readonly string UiReferencePattern =
            @"using\s+KitchenDesigner\s*\.\s*Core\s*\.\s*UI\s*;|(?<![\w.])UI\s*\.\s*[A-Z]\w*";

        private static readonly string McpReferencePattern =
            @"using\s+KitchenDesigner\s*\.\s*Core\s*\.\s*MCP\s*;|(?<![\w.])MCP\s*\.\s*[A-Z]\w*";

        private static string CoreDir() => RepoPaths.Subdir("Assets", "Scripts", "Core");

        private static string[] LayerDirs()
        {
            var dirs = Directory.GetDirectories(CoreDir());
            Array.Sort(dirs, StringComparer.Ordinal);
            return dirs;
        }

        private static List<string> References(string layer, string pattern)
        {
            var hits = new List<string>();
            var dir = Path.Combine(CoreDir(), layer);
            if (!Directory.Exists(dir)) return hits;

            foreach (var file in Directory.GetFiles(dir, "*.cs", SearchOption.AllDirectories))
            {
                var lines = File.ReadAllLines(file);
                for (int i = 0; i < lines.Length; i++)
                {
                    var trimmed = lines[i].TrimStart();
                    if (trimmed.StartsWith("//") || trimmed.StartsWith("*") || trimmed.StartsWith("/*"))
                        continue;
                    if (Regex.IsMatch(lines[i], pattern))
                        hits.Add(layer + "/" + Path.GetFileName(file) + ":" + (i + 1) + "\n    " + trimmed);
                }
            }

            return hits;
        }

        private static int Ceiling((string layer, int ceiling, string why)[] budgets, string layer)
        {
            foreach (var (name, ceiling, _) in budgets)
                if (string.Equals(name, layer, StringComparison.Ordinal)) return ceiling;
            return 0;
        }

        /// <summary>Проверяются ВСЕ каталоги слоёв, а не строки таблицы: новый слой
        /// попадает под правило сам, с потолком ноль, и не ждёт, пока про него
        /// вспомнят.</summary>
        private static List<string> CheckedLayers(string target)
        {
            var layers = new List<string>();
            foreach (var dir in LayerDirs())
            {
                var name = Path.GetFileName(dir) ?? string.Empty;
                if (name.Length == 0 || name == target) continue;
                layers.Add(name);
            }
            return layers;
        }

        private static void CheckRatchet((string layer, int ceiling, string why)[] budgets,
            string pattern, string target, string ownLayer)
        {
            var over = new List<string>();
            var under = new List<string>();

            foreach (var layer in CheckedLayers(target))
            {
                int ceiling = Ceiling(budgets, layer);
                var hits = References(layer, pattern);
                if (hits.Count > ceiling)
                    over.Add(layer + ": " + hits.Count + " при потолке " + ceiling + "\n"
                        + string.Join("\n", hits));
                else if (hits.Count < ceiling)
                    under.Add(layer + ": " + hits.Count + " при потолке " + ceiling);
            }

            Assert.IsEmpty(over,
                "Слой " + target + " знает про всех, про него — никто: " + ownLayer
                + " отвечает данными и событиями, а не дёргает панели. Новая ссылка вверх "
                + "запрещена. Найдено:\n" + string.Join("\n", over));

            Assert.IsEmpty(under,
                "Ссылок стало меньше — опустите потолок в этом же коммите, иначе храповик "
                + "отдаст назад то, что только что вычищено: " + string.Join("; ", under));
        }

        [Test]
        public void NoCoreLayer_ReachesUp_IntoTheUiLayer()
        {
            CheckRatchet(UiBudgets, UiReferencePattern, "UI", "ядро");
        }

        [Test]
        public void NoCoreLayer_ReachesSideways_IntoTheMcpLayer()
        {
            CheckRatchet(McpBudgets, McpReferencePattern, "MCP", "ядро");
        }

        /// <summary>Правило обязано накрывать КАЖДЫЙ каталог слоя: таблица задаёт
        /// только потолки долгов, а не список проверяемых. Иначе новый слой родится
        /// вне правила и напишет свои ссылки вверх, пока про него не вспомнят.</summary>
        [Test]
        public void EveryLayerDirectory_IsCovered_NotOnlyTheOnesInTheTable()
        {
            var checkedLayers = CheckedLayers("UI");

            CollectionAssert.Contains(checkedLayers, "Validation",
                "слой без строки в таблице обязан проверяться с потолком ноль");
            CollectionAssert.Contains(checkedLayers, "Pure");
            CollectionAssert.DoesNotContain(checkedLayers, "UI", "слой UI себя не проверяет");
            Assert.AreEqual(LayerDirs().Length - 1, checkedLayers.Count,
                "проверяются все каталоги слоёв, кроме целевого");
        }

        [Test]
        public void EveryBudgetRow_NamesAnExistingLayer_AndExplainsAnyDebt()
        {
            var existing = new HashSet<string>(StringComparer.Ordinal);
            foreach (var dir in LayerDirs()) existing.Add(Path.GetFileName(dir) ?? string.Empty);

            foreach (var (layer, ceiling, why) in UiBudgets)
            {
                Assert.IsTrue(existing.Contains(layer),
                    "в таблице числится несуществующий слой " + layer
                    + " — запись переживёт свой каталог и начнёт освобождать следующий с этим именем");
                if (ceiling > 0)
                    Assert.IsNotEmpty(why,
                        "потолок " + ceiling + " у слоя " + layer + " без причины через полгода "
                        + "не отличить от недосмотра");
            }

            foreach (var (layer, ceiling, why) in McpBudgets)
            {
                Assert.IsTrue(existing.Contains(layer), "несуществующий слой " + layer);
                Assert.IsNotEmpty(why, "потолок " + ceiling + " у слоя " + layer + " без причины");
            }
        }

        /// <summary>Грепу по несуществующему пути нечего найти — он зеленеет,
        /// ничего не проверив.</summary>
        [Test]
        public void TheScan_ActuallySeesTheLayersAndTheirReferences()
        {
            Assert.Greater(LayerDirs().Length, 10, "скан обязан видеть слои ядра");
            CollectionAssert.IsNotEmpty(References("Persistence", UiReferencePattern),
                "у Persistence сегодня есть ссылки на UI — пустой список означает сломанный скан, "
                + "а не вычищенный слой");
            CollectionAssert.IsEmpty(References("Geometry", UiReferencePattern),
                "ядро на UI не ссылается — если здесь что-то нашлось, сломан не скан, а ядро");
        }

        [Test]
        public void TheScan_TellsALayerReferenceFromASimilarLookingName()
        {
            Assert.IsTrue(Regex.IsMatch("using KitchenDesigner.Core.UI;", UiReferencePattern),
                "ссылка через using — основная форма зависимости от слоя");
            Assert.IsTrue(Regex.IsMatch("            UI.StatusBarUI.Instance?.ShowTransient(", UiReferencePattern),
                "квалификатор UI. — вторая форма той же зависимости");
            Assert.IsFalse(Regex.IsMatch("            GUI.Label(rect, text);", UiReferencePattern),
                "GUI движка не имеет отношения к слою UI проекта");
            Assert.IsFalse(Regex.IsMatch("            var c = panelUI.Color;", UiReferencePattern),
                "поле с UI в имени — не пространство имён");
            Assert.IsFalse(Regex.IsMatch("        private string _ui = \"x\";", UiReferencePattern),
                "строчное ui не квалификатор");
        }
    }
}
