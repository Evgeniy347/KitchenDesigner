using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace KitchenDesigner.Tests.Geometry
{
    /// <summary>Сторож направления зависимостей между слоями
    /// (CONVENTIONS.md → «Known debts»). Объявленный граф прост: UI знает про всех,
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
            ("Persistence", 4, "автосохранение и снимок сцены зовут StatusBarUI и ProjectWindows "
                + "— самый вредный из долгов: он же и делает Persistence непроверяемым без сцены"),
            ("Platform", 0, ""),
            ("Pure", 0, "чистый слой собирается вторым проходом и обязан жить без движка вовсе"),
            ("Rendering", 10, "CameraController открывает контекстное меню, SideHighlighter берёт "
                + "цвет из UIStyle — долг. Плюс один осознанный: ValidityTint берёт красный "
                + "тона нарушения из UIStyle.HighlightError, потому что «красный» обязан "
                + "значить одно и то же в плашке статуса и в сцене (docs/UI-GUIDELINES.md "
                + "§ 9, § 10: локальные цветовые константы запрещены), — как это уже делают "
                + "HighlightOverlay и ScenePreview. Литерал вместо ссылки закрывал бы этот "
                + "потолок ценой пятого оттенка красного"),
            ("Snap", 4, "ручки растягивания показывают подсказки и берут цвета из UIStyle — долг"),
            ("Tools", 0, ""),
            ("Update", 4, "панели обновления сами по себе UI и живут не в том каталоге — долг, "
                + "лечится переездом, а не ссылкой"),
            ("Validation", 0, ""),
        };

        /// <summary>Слой MCP — контракт наружу; знать про него вправе только тот,
        /// кто его поднимает.</summary>
        private static readonly (string layer, int ceiling, string why)[] McpBudgets =
        {
            ("Infrastructure", 2, "Bootstrap поднимает мост MCP (HTTP-эндпоинт и перехват "
                + "консоли) — композиционный корень; больше про MCP не знает никто"),
        };

        /// <summary>Три формы ОДНОЙ зависимости, и сторож обязан видеть все три.
        /// Долго видел две: <c>using</c> и квалификатор <c>UI.</c>. Третья —
        /// полное имя <c>KitchenDesigner.Core.UI.UIStyle</c> прямо в выражении —
        /// проходила мимо, потому что перед <c>UI</c> стоит точка и запрет
        /// <c>(?&lt;![\w.])</c> гасил совпадение. Так обошли сторожа один раз
        /// вручную (conventions/STRUCTURE.md → «Обойти сторожа — это нарушить
        /// правило и спрятать нарушение»), и этого хватило: правило, которое
        /// обходится опечаткой в стиле, правилом не является.
        ///
        /// Префикс <c>KitchenDesigner.</c> необязателен: внутри
        /// <c>namespace KitchenDesigner.Core.*</c> короткое <c>Core.UI.UIStyle</c>
        /// компилируется так же и значит то же самое.</summary>
        private static string ReferencePattern(string layer) =>
            @"using\s+(?:static\s+)?KitchenDesigner\s*\.\s*Core\s*\.\s*" + layer + @"\s*;"
            + @"|(?<![\w.])(?:global\s*::\s*)?(?:KitchenDesigner\s*\.\s*)?Core\s*\.\s*"
            + layer + @"\s*\.\s*[A-Z]\w*"
            + @"|(?<![\w.])" + layer + @"\s*\.\s*[A-Z]\w*";

        private static readonly string UiReferencePattern = ReferencePattern("UI");

        private static readonly string McpReferencePattern = ReferencePattern("MCP");

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

            foreach (var file in SourceCorpus.Files(dir))
            {
                var lines = SourceCorpus.Lines(file);
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

        /// <summary>Форма, которая обходила сторожа: полное имя типа вместо
        /// <c>using</c>. Каждая строка здесь — настоящая зависимость слоя от UI,
        /// записанная так, как её пишет тот, кто не хочет добавлять using.</summary>
        [Test]
        public void TheScan_SeesAFullyQualifiedName_NotOnlyAUsing()
        {
            Assert.IsTrue(Regex.IsMatch(
                    "            var c = KitchenDesigner.Core.UI.UIStyle.HighlightError;",
                    UiReferencePattern),
                "полное имя — такая же зависимость, как using, и именно ею сторожа обошли");
            Assert.IsTrue(Regex.IsMatch(
                    "            var c = Core.UI.UIStyle.HighlightError;", UiReferencePattern),
                "внутри KitchenDesigner.* короткий префикс Core. значит ровно то же самое");
            Assert.IsTrue(Regex.IsMatch(
                    "            global::KitchenDesigner.Core.UI.StatusBarUI.Instance?.Show();",
                    UiReferencePattern),
                "global:: — та же ссылка, лишь записанная от корня");
            Assert.IsTrue(Regex.IsMatch(
                    "using static KitchenDesigner.Core.UI.UIStyle;", UiReferencePattern),
                "using static тянет тот же тип из того же слоя");
            Assert.IsTrue(Regex.IsMatch(
                    "using Style = KitchenDesigner.Core.UI.UIStyle;", UiReferencePattern),
                "псевдоним прячет имя слоя от читателя, но не от компилятора");
            Assert.IsTrue(Regex.IsMatch(
                    "            KitchenDesigner.Core.MCP.McpBridge.Stop();", McpReferencePattern),
                "то же правило и для MCP — пара сторожей обязана видеть одинаково");
        }

        /// <summary>Обратная сторона: полное имя ловится по СЛОЮ, а не по строке
        /// «KitchenDesigner.Core». Объявление собственного пространства имён и
        /// соседние слои остаться чистыми обязаны, иначе новый шаблон закроет
        /// потолки шумом и его придётся ослаблять обратно.</summary>
        [Test]
        public void TheFullNameForm_DoesNotFireOnNamespacesAndNeighbourLayers()
        {
            Assert.IsFalse(Regex.IsMatch("namespace KitchenDesigner.Core.UI", UiReferencePattern),
                "объявление пространства имён — не ссылка вверх: так живёт Core/Pure/UI");
            Assert.IsFalse(Regex.IsMatch("using KitchenDesigner.Core.Elements;", UiReferencePattern),
                "соседний слой не имеет отношения к UI");
            Assert.IsFalse(Regex.IsMatch(
                    "            var g = KitchenDesigner.Core.Rendering.GUIHelper.Draw();",
                    UiReferencePattern),
                "имя типа, начинающееся на UI, внутри другого слоя — не слой UI");
        }
    }
}
