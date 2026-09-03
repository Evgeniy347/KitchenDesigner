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
/// <c>IWallMounted</c>, <c>IFacadeHost</c>, <c>ITabletop</c>).
///
/// Список разрешённых файлов лежит ЗДЕСЬ, а не в конфиге: расширить его можно
/// только осознанной правкой этого теста, которую видно в ревью.
/// </summary>
public class UiElementTypeLadderTests
{
    /// <summary>Файлы слоя UI, которым спрашивать конкретный тип элемента
    /// РАЗРЕШЕНО. Каждому нужна причина — иначе список станет свалкой.</summary>
    private static readonly (string file, string why)[] Allowed =
    {
        ("ElementFacets.cs", "единственное место, где тип превращается в данные — набор ElementFacet"),
        ("ElementTypeConverter.cs", "реестр конвертации типов: вопрос «во что можно превратить» и есть его предмет"),
        ("RadialFieldsEditor.cs", "реестр редакторов: ветка выбрана Handles(), внутри тип уже известен"),
        ("CooktopCutoutFieldsEditor.cs", "реестр редакторов"),
        ("DrawerBoxFieldsEditor.cs", "реестр редакторов"),
        ("PillarFieldsEditor.cs", "реестр редакторов"),
        ("TableLegFieldsEditor.cs", "реестр редакторов"),
        ("StoolFieldsEditor.cs", "реестр редакторов"),
        ("ChairFieldsEditor.cs", "реестр редакторов"),
        ("SofaFieldsEditor.cs", "реестр редакторов"),
        ("WallOpeningFieldsEditor.cs", "реестр редакторов"),
        ("LightFieldsEditor.cs", "реестр редакторов"),
        ("FacadeFieldsEditor.cs", "реестр редакторов"),
        ("AssembledFacadeFieldsEditor.cs", "реестр редакторов"),
        ("ScrewLegFieldsEditor.cs", "реестр редакторов"),
        ("BedFieldsEditor.cs", "реестр редакторов"),
        ("PouffeFieldsEditor.cs", "реестр редакторов"),
        ("BathtubFieldsEditor.cs", "реестр редакторов"),
        ("BathMixerFieldsEditor.cs", "реестр редакторов"),
        ("ShowerColumnFieldsEditor.cs", "реестр редакторов"),
        ("ToiletFieldsEditor.cs", "реестр редакторов: один на оба варианта унитаза — "
            + "строка «Высота чаши» у них общая, а «Высота панели» только у подвесного"),
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

            var lines = File.ReadAllLines(file);
            for (int i = 0; i < lines.Length; i++)
            {
                var trimmed = lines[i].TrimStart();
                if (trimmed.StartsWith("//") || trimmed.StartsWith("*") || trimmed.StartsWith("/*"))
                    continue;

                foreach (var (pattern, why) in Banned)
                    if (Regex.IsMatch(lines[i], pattern))
                        violations.Add($"{name}:{i + 1} — {why}\n    {trimmed}");
            }
        }

        Assert.IsEmpty(violations,
            "Тип элемента спрашивают один раз на слой, дальше едут данные. "
            + "Расширять список разрешённых файлов — осознанная правка теста. Найдено:\n"
            + string.Join("\n", violations));
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
}
