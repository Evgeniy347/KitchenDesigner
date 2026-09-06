using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;

/// <summary>
/// Сторож правила «тип элемента спрашивают в ОДНОМ месте на слой»
/// (CONVENTIONS.md → «Element type checks live in ONE place per layer»),
/// перенесённый на слой Validation по образцу <c>GeometryArchitectureTests</c>
/// и <c>UiElementTypeLadderTests</c>.
///
/// <c>agents/SUBSYSTEMS.md</c> объявляет <c>Validation/ValidationSnapshot.cs</c>
/// ЕДИНСТВЕННЫМ файлом, где вопрос «это дверь / пол / мойка» разрешён — роли
/// элементов должны доходить до остального слоя как флаги <c>ElementKind</c> или
/// как типизированные аксессоры (<c>AsDishwasher</c>, <c>IsPanel</c> и т.п.).
/// До этого коммита правило нарушалось молча ровно потому, что сторожа не было:
/// ни один существующий тест не сканировал <c>Core/Validation</c>.
/// </summary>
public class ValidationLadderTests
{
    /// <summary>Файлы слоя Validation, которым спрашивать конкретный тип
    /// элемента РАЗРЕШЕНО. Каждому нужна причина.</summary>
    private static readonly (string file, string why)[] Allowed =
    {
        ("ValidationSnapshot.cs",
            "единственное место, где тип элемента превращается в данные — флаги ElementKind "
            + "и типизированные аксессоры вроде AsDishwasher/IsPanel"),
    };

    /// <summary>Формы вопроса «какого ты типа». Базовый <c>KitchenElement</c>
    /// исключён: это валюта всего слоя, а не конкретный тип.</summary>
    private static readonly (string pattern, string why)[] Banned =
    {
        (@"\bis\s+(?!KitchenElement\b)[A-Z]\w*Element\b",
            "лестница по типу: спросите ValidationSnapshot"),
        (@"\bas\s+(?!KitchenElement\b)[A-Z]\w*Element\b",
            "приведение к конкретному типу — та же лестница, просто через as"),
        (@"GetComponent\s*<\s*(?!KitchenElement\b)[A-Z]\w*Element\s*>",
            "поиск компонента конкретного типа — тот же вопрос, заданный сцене"),
        (@"\(\s*(?!KitchenElement\b)[A-Z]\w*Element\s*\)\s*[\w(]",
            "жёсткое приведение к конкретному типу"),
    };

    private static string ValidationSourceDir() =>
        RepoPathFrom("Assets", "Scripts", "Core", "Validation");

    private static string RepoPathFrom(params string[] parts)
    {
        var roots = new[]
        {
            Path.GetDirectoryName(typeof(ValidationLadderTests).Assembly.Location),
            AppContext.BaseDirectory,
            Directory.GetCurrentDirectory(),
        };

        foreach (var root in roots)
        {
            if (string.IsNullOrEmpty(root)) continue;
            var dir = new DirectoryInfo(root);
            while (dir != null)
            {
                var candidate = Path.Combine(dir.FullName, Path.Combine(parts));
                if (Directory.Exists(candidate)) return candidate;
                dir = dir.Parent;
            }
        }

        throw new DirectoryNotFoundException(
            "Не найден " + Path.Combine(parts) + " ни от одной из точек: " + string.Join(", ", roots));
    }

    private static bool IsAllowed(string fileName)
    {
        foreach (var (file, _) in Allowed)
            if (string.Equals(file, fileName, StringComparison.Ordinal)) return true;
        return false;
    }

    private static List<string> Violations(string name, string[] lines)
    {
        var found = new List<string>();

        for (int i = 0; i < lines.Length; i++)
        {
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
    public void ValidationSources_AskForAnElementType_OnlyInTheAllowedFiles()
    {
        var files = Directory.GetFiles(ValidationSourceDir(), "*.cs", SearchOption.AllDirectories);
        Assert.IsNotEmpty(files, "в слое Validation нет исходников — тест бесполезен");

        var violations = new List<string>();

        foreach (var file in files)
        {
            var name = Path.GetFileName(file);
            if (IsAllowed(name)) continue;
            violations.AddRange(Violations(name, File.ReadAllLines(file)));
        }

        Assert.IsEmpty(violations,
            "Тип элемента спрашивают один раз на слой (ValidationSnapshot.cs), дальше едут "
            + "данные — флаги ElementKind или типизированные аксессоры. Расширять список "
            + "разрешённых файлов — осознанная правка этого теста. Найдено:\n"
            + string.Join("\n", violations));
    }

    /// <summary>Скан должен реально видеть слой, а конкретно — файл, из-за
    /// которого этот тест написан. Сломанный путь дал бы пустой список файлов
    /// и зелёный тест, который ничего не проверяет.</summary>
    [Test]
    public void ValidationSources_AreActuallyScanned()
    {
        var names = new List<string>();
        foreach (var f in Directory.GetFiles(ValidationSourceDir(), "*.cs", SearchOption.AllDirectories))
            names.Add(Path.GetFileName(f));

        CollectionAssert.Contains(names, "ConstraintValidator.cs",
            "ConstraintValidator — главный подопечный правила и НЕ в списке разрешённых");
        Assert.IsFalse(IsAllowed("ConstraintValidator.cs"),
            "ConstraintValidator не имеет права спрашивать тип: он был поводом завести это правило");
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

    /// <summary>Список не должен переживать свои файлы: удалили файл — уберите
    /// и строку, иначе разрешение молча повиснет на новом файле с тем же именем.</summary>
    [Test]
    public void EveryAllowedFile_StillExists()
    {
        var dir = ValidationSourceDir();
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
        var dir = ValidationSourceDir();
        var idle = new List<string>();
        foreach (var (file, _) in Allowed)
        {
            var path = Path.Combine(dir, file);
            if (!File.Exists(path)) continue;
            if (Violations(file, File.ReadAllLines(path)).Count == 0) idle.Add(file);
        }

        Assert.IsEmpty(idle,
            "эти файлы больше не спрашивают тип вне разрешения — уберите их из списка: "
            + string.Join(", ", idle));
    }
}
