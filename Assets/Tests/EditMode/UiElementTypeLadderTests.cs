using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;

/// <summary>
/// Сторож правила «тип элемента спрашивают в ОДНОМ месте на слой»
/// (CONVENTIONS.md → «Element type checks live in ONE place per layer»),
/// перенесённый на слой UI по образцу <c>GeometryArchitectureTests</c>.
///
/// Ядро эту защиту получило давно; UI — нет, и <c>ContextMenuUI</c> дошёл до
/// пятидесяти с лишним <c>is XxxElement</c>: одна и та же лестница жила в пяти
/// методах, новый тип элемента означал пять правок, а забыть можно было любую.
/// Теперь тип решается один раз (<c>ElementFacets.Of</c> — фасеты, реестр
/// <c>ElementFieldsEditor.Handles</c> — редакторы), а дальше по слою едут
/// данные: набор фасетов, редактор, интерфейс (<c>IOpenable</c>,
/// <c>IWallMounted</c>, <c>IFacadeHost</c>, <c>IHasTwoDecorSlots</c>).
///
/// <c>Handles</c> — это и есть тот единственный вопрос для редактора, поэтому
/// он разрешён ПРАВИЛОМ, а не строкой в списке: скан пропускает выражение
/// <c>override bool Handles(...)</c> и ровно его. Раньше ради этой одной строки
/// файл целиком попадал в список разрешённых и мог спрашивать тип где угодно и
/// сколько угодно раз — типизированный <c>NumberFieldsEditor.Bind&lt;T&gt;</c>
/// убрал вопрос из геттеров и сеттеров, и шесть редакторов вышли из списка.
///
/// Список разрешённых файлов лежит ЗДЕСЬ, а не в конфиге: расширить его можно
/// только осознанной правкой этого теста, которую видно в ревью.
/// </summary>
public class UiElementTypeLadderTests
{
    /// <summary>Файлы слоя UI, которым спрашивать конкретный тип элемента
    /// РАЗРЕШЕНО и ВНЕ <c>Handles</c>. Каждому нужна причина — иначе список
    /// станет свалкой.</summary>
    private static readonly (string file, string why)[] Allowed =
    {
        ("ElementFacets.cs", "единственное место, где тип превращается в данные — набор ElementFacet"),
        ("ElementTypeConverter.cs", "реестр конвертации типов: вопрос «во что можно превратить» и есть его предмет"),
        ("RadialFieldsEditor.cs", "реестр редакторов: ветка выбрана Handles(), внутри тип уже известен"),
        ("CooktopCutoutFieldsEditor.cs", "реестр редакторов"),
        ("DrawerBoxFieldsEditor.cs", "реестр редакторов"),
        ("PillarFieldsEditor.cs", "реестр редакторов"),
        ("WallOpeningFieldsEditor.cs", "реестр редакторов"),
        ("LightFieldsEditor.cs", "реестр редакторов"),
        ("FacadeFieldsEditor.cs", "реестр редакторов"),
        ("AssembledFacadeFieldsEditor.cs", "реестр редакторов"),
        ("ScrewLegFieldsEditor.cs", "реестр редакторов"),
        ("PipeFieldsEditor.cs", "реестр редакторов"),
        ("PipeFittingFieldsEditor.cs", "реестр редакторов: ветку выбрал Handles(), схема портов "
            + "строится по PipeFittingSpec.Legs, а не по виду фитинга"),
        ("BathtubFieldsEditor.cs", "видимость строк: RowVisibility.When по Host.Target"),
        ("BathMixerFieldsEditor.cs", "видимость строк: RowVisibility.When по Host.Target"),
        ("ShowerColumnFieldsEditor.cs", "видимость строк: RowVisibility.When по Host.Target"),
        ("ToiletFieldsEditor.cs", "видимость строк: один редактор на оба варианта унитаза — "
            + "строка «Высота чаши» у них общая, а «Высота панели» только у подвесного"),
        ("PipeEndsDiagram.cs", "делегат Paint для общей двери PipePortHover: конструктор берёт "
            + "только Func<PipeElement?>, поэтому owner здесь не может быть ничем другим — "
            + "проверка не ветвит на несколько типов, а лишь возвращает callback к типу, "
            + "стёртому общим сигнатурой Action<KitchenElement,int>"),
        ("PipeFittingPortsDiagram.cs", "тот же делегат Paint для той же общей двери "
            + "PipePortHover, конструктор берёт только Func<PipeFittingElement?>"),
    };

    /// <summary>Формы вопроса «какого ты типа». Базовый <c>KitchenElement</c>
    /// исключён: это валюта всего слоя, а не конкретный тип.</summary>
    private static readonly (string pattern, string why)[] Banned =
    {
        (@"\bis\s+(?!KitchenElement\b)[A-Z]\w*Element\b",
            "лестница по типу: спросите фасет, редактор или интерфейс"),
        (@"\bas\s+(?!KitchenElement\b)[A-Z]\w*Element\b",
            "приведение к конкретному типу — та же лестница, просто через as"),
        (@"GetComponent\s*<\s*(?!KitchenElement\b)[A-Z]\w*Element\s*>",
            "поиск компонента конкретного типа — тот же вопрос, заданный сцене"),
        (@"\(\s*(?!KitchenElement\b)[A-Z]\w*Element\s*\)\s*[\w(]",
            "жёсткое приведение к конкретному типу"),
    };

    /// <summary>Сколько строк отводится ответу <c>Handles</c>. Предикат из двух
    /// вариантов («унитаз обычный или подвесной») занимает две; всё, что длиннее,
    /// — уже не одна проверка, и хвост попадает под общий запрет.</summary>
    private const int MaxHandlesLines = 3;

    private static readonly Regex HandlesStart =
        new Regex(@"\boverride\s+bool\s+Handles\s*\(", RegexOptions.Compiled);

    private static string UiSourceDir()
    {
        var roots = new[]
        {
            Path.GetDirectoryName(typeof(UiElementTypeLadderTests).Assembly.Location),
            AppContext.BaseDirectory,
            Directory.GetCurrentDirectory(),
        };

        foreach (var root in roots)
        {
            if (string.IsNullOrEmpty(root)) continue;
            var dir = new DirectoryInfo(root);
            while (dir != null)
            {
                var candidate = Path.Combine(dir.FullName, "Assets", "Scripts", "Core", "UI");
                if (Directory.Exists(candidate)) return candidate;
                dir = dir.Parent;
            }
        }

        throw new DirectoryNotFoundException(
            "Не найден Assets/Scripts/Core/UI ни от одной из точек: " + string.Join(", ", roots));
    }

    private static bool IsAllowed(string fileName)
    {
        foreach (var (file, _) in Allowed)
            if (string.Equals(file, fileName, StringComparison.Ordinal)) return true;
        return false;
    }

    /// <summary>Строки единственного разрешённого вопроса — выражения
    /// <c>override bool Handles(...)</c> от его начала до точки с запятой.</summary>
    private static HashSet<int> HandlesLines(string[] lines)
    {
        var exempt = new HashSet<int>();
        for (int i = 0; i < lines.Length; i++)
        {
            if (!HandlesStart.IsMatch(lines[i])) continue;
            for (int j = i; j < lines.Length && j < i + MaxHandlesLines; j++)
            {
                exempt.Add(j);
                if (lines[j].Contains(";")) break;
            }
        }

        return exempt;
    }

    private static List<string> Violations(string name, string[] lines)
    {
        var found = new List<string>();
        var exempt = HandlesLines(lines);

        for (int i = 0; i < lines.Length; i++)
        {
            if (exempt.Contains(i)) continue;

            var trimmed = lines[i].TrimStart();
            if (trimmed.StartsWith("//") || trimmed.StartsWith("*") || trimmed.StartsWith("/*"))
                continue;

            foreach (var (pattern, why) in Banned)
                if (Regex.IsMatch(lines[i], pattern))
                    found.Add($"{name}:{i + 1} — {why}\n    {trimmed}");
        }

        return found;
    }

    [Test]
    public void UiSources_AskForAnElementType_OnlyInTheAllowedFiles()
    {
        var files = Directory.GetFiles(UiSourceDir(), "*.cs", SearchOption.AllDirectories);
        Assert.IsNotEmpty(files, "в слое UI нет исходников — тест бесполезен");

        var violations = new List<string>();

        foreach (var file in files)
        {
            var name = Path.GetFileName(file);
            if (IsAllowed(name)) continue;
            violations.AddRange(Violations(name, File.ReadAllLines(file)));
        }

        Assert.IsEmpty(violations,
            "Тип элемента спрашивают один раз на слой, дальше едут данные. "
            + "Расширять список разрешённых файлов — осознанная правка теста. Найдено:\n"
            + string.Join("\n", violations));
    }

    /// <summary>Послабление — ровно ответ <c>Handles</c>, а не файл вокруг него:
    /// такой же вопрос строкой ниже обязан быть найден.</summary>
    [Test]
    public void HandlesExemption_CoversThePredicateAndNothingElse()
    {
        var sample = new[]
        {
            "        public override bool Handles(KitchenElement element)",
            "            => element is ToiletElement || element is WallHungToiletElement;",
            "",
            "        private static int SeatOf(KitchenElement element) =>",
            "            element is ToiletElement toilet ? toilet.SeatHeightMM : 0;",
        };

        var violations = Violations("Sample.cs", sample);

        Assert.AreEqual(1, violations.Count,
            "должен остаться ровно вопрос из геттера:\n" + string.Join("\n", violations));
        StringAssert.Contains("Sample.cs:5", violations[0]);
    }

    /// <summary>Разросшийся <c>Handles</c> перестаёт быть одной проверкой:
    /// послабление обрывается, и хвост снова виден сторожу.</summary>
    [Test]
    public void HandlesExemption_StopsWhenThePredicateGrows()
    {
        var sample = new[]
        {
            "        public override bool Handles(KitchenElement element)",
            "            => element is AElement",
            "            || element is BElement",
            "            || element is CElement;",
        };

        Assert.AreEqual(sample.Length - MaxHandlesLines, Violations("Big.cs", sample).Count,
            "хвост длинного предиката обязан попасть под общий запрет");
    }

    /// <summary>Скан должен реально видеть слой. Сломанный путь дал бы пустой
    /// список файлов и зелёный тест, который ничего не проверяет.</summary>
    [Test]
    public void UiSources_AreActuallyScanned()
    {
        var names = new List<string>();
        foreach (var f in Directory.GetFiles(UiSourceDir(), "*.cs", SearchOption.AllDirectories))
            names.Add(Path.GetFileName(f));

        CollectionAssert.Contains(names, "ContextMenuUI.cs",
            "ContextMenuUI — главный подопечный правила и НЕ в списке разрешённых");
        Assert.IsFalse(IsAllowed("ContextMenuUI.cs"),
            "ContextMenuUI не имеет права спрашивать тип: он был анти-примером этого правила");
    }

    /// <summary>Каждому послаблению — причина, как у [NotUndoable].</summary>
    [Test]
    public void EveryAllowedFile_ExplainsWhy()
    {
        var empty = new List<string>();
        foreach (var (file, why) in Allowed)
            if (string.IsNullOrWhiteSpace(why)) empty.Add(file);

        Assert.IsEmpty(empty,
            "разрешение без причины через полгода не отличить от недосмотра: "
            + string.Join(", ", empty));
    }

    /// <summary>Список не должен переживать свои файлы: удалили редактор —
    /// уберите и строку, иначе разрешение молча повиснет на новом файле с тем
    /// же именем.</summary>
    [Test]
    public void EveryAllowedFile_StillExists()
    {
        var dir = UiSourceDir();
        var missing = new List<string>();
        foreach (var (file, _) in Allowed)
            if (!File.Exists(Path.Combine(dir, file))) missing.Add(file);

        Assert.IsEmpty(missing,
            "в списке разрешённых числятся несуществующие файлы: " + string.Join(", ", missing));
    }

    /// <summary>Разрешение, которое больше не нужно, — это разрешение, выданное
    /// на будущее без причины. Файл из списка обязан ДЕЙСТВИТЕЛЬНО нарушать
    /// правило; перестал — строка уходит вместе с долгом.</summary>
    [Test]
    public void EveryAllowedFile_ActuallyNeedsItsPermission()
    {
        var dir = UiSourceDir();
        var idle = new List<string>();
        foreach (var (file, _) in Allowed)
        {
            var path = Path.Combine(dir, file);
            if (!File.Exists(path)) continue;
            if (Violations(file, File.ReadAllLines(path)).Count == 0) idle.Add(file);
        }

        Assert.IsEmpty(idle,
            "эти файлы больше не спрашивают тип вне Handles — уберите их из списка: "
            + string.Join(", ", idle));
    }
}
