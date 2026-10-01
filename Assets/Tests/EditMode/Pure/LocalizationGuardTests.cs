using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using KitchenDesigner.Core;
using KitchenDesigner.Core.UI;
using KitchenDesigner.Tests.Geometry;
using NUnit.Framework;

/// <summary>
/// Сторож перевода. Интерфейс переведён, пока выполняются три условия, и каждое ломается
/// молча, без единой ошибки компиляции:
///
/// 1. В коде интерфейса не осталось русских литералов. Новая подпись, вписанная по-русски
///    прямо в панель, на английском экране так и останется русской — и никто, кроме
///    англоязычного пользователя, этого не увидит.
/// 2. Каждый ключ из кода есть в ru.json и en.json. Иначе человек видит на экране сам ключ.
/// 3. Таблицы согласованы между собой: у каждого перевода есть русский исходник, у каждого
///    исходника — запись контекста для переводчика, а местозаполнители {0}, {1} совпадают —
///    иначе string.Format упадёт или потеряет число.
///
/// Исключения из пункта 1 — ровно три вида, и все они записаны в
/// <see cref="LocalizationAllowList"/> поимённо и с причиной: строки журнала для
/// разработчика (Debug.Log, исключения, причины [NotUndoable]), данные-идентификаторы
/// (русские синонимы во входе MCP, значения из каталога текстур) и инструменты разработчика
/// (F9, тестовая инфраструктура). Нормативные ссылки «// СП …» — комментарии, а не литералы,
/// и сканом не видны вовсе.
/// </summary>
public class LocalizationGuardTests
{
    private static readonly Regex Literal = new Regex(@"(\$@|@\$|\$|@)?""(?:[^""\\]|\\.|"""")*""");
    private static readonly Regex Cyrillic = new Regex("[А-Яа-яЁё]");
    private static readonly Regex KeyCall = new Regex(@"\bLoc\.(T|F|Plural)\(\s*""([^""]+)""");
    private static readonly Regex AnyCall = new Regex(@"\bLoc\.(T|F|Plural)\(");
    private static readonly Regex Placeholder = new Regex(@"(?<!\{)\{(\d+)(?:[,:][^}]*)?\}");
    private static readonly Regex DeveloperStatement = new Regex(
        @"\bDebug\.Log\w*\(|\bthrow\b|Exception\(|\[NotUndoable\(|\bLogWarning\(|\bLogError\(");

    private const string Engine = "Deb" + "ug";

    private static string ScriptsDir() => RepoPaths.Subdir("Assets", "Scripts");

    private static string LocalizationDir() => RepoPaths.Subdir("Assets", "StreamingAssets", "Localization");

    private static string Relative(string path) =>
        path.Substring(ScriptsDir().Length + 1).Replace('\\', '/');

    private static StringTable Table(string language) =>
        StringTable.Parse(language, File.ReadAllText(Path.Combine(LocalizationDir(), language + ".json"), Encoding.UTF8));

    private static List<string> ContextKeys()
    {
        var json = File.ReadAllText(Path.Combine(LocalizationDir(), LocalizationFiles.ContextFileName), Encoding.UTF8);
        return JsonText.Members(json, JsonText.RootObject(json)).Select(m => m.Key).ToList();
    }

    private static IEnumerable<string> LanguageCodes() =>
        Directory.GetFiles(LocalizationDir(), "*.json")
            .Select(Path.GetFileName)
            .Where(n => LocalizationFiles.IsLanguageFile(n!))
            .Select(n => Path.GetFileNameWithoutExtension(n!));

    private static IEnumerable<string> ProductionFiles() =>
        SourceCorpus.Files(ScriptsDir());

    internal static List<(string file, int line, string literal)> CyrillicLiterals(string relative, IReadOnlyList<string> lines)
    {
        var found = new List<(string, int, string)>();
        var code = SourceLines.WithoutComments(lines).ToList();
        for (int i = 0; i < code.Count; i++)
        {
            foreach (Match m in Literal.Matches(code[i]))
            {
                if (!Cyrillic.IsMatch(m.Value)) continue;
                if (DeveloperStatement.IsMatch(StatementPrefix(code, i, m.Index))) continue;
                found.Add((relative, i + 1, m.Value));
            }
        }
        return found;
    }

    private static string StatementPrefix(IReadOnlyList<string> code, int line, int column)
    {
        var sb = new StringBuilder(code[line].Substring(0, column));
        for (int back = line - 1; back >= 0 && back >= line - 8; back--)
        {
            var text = code[back].TrimEnd();
            if (text.EndsWith(";") || text.EndsWith("{") || text.EndsWith("}")) break;
            sb.Insert(0, text + "\n");
        }
        return sb.ToString();
    }

    private static readonly Lazy<List<(string file, int line, string literal)>> CyrillicInProduction =
        new Lazy<List<(string, int, string)>>(() => ProductionFiles()
            .SelectMany(f => CyrillicLiterals(Relative(f), SourceCorpus.Lines(f)))
            .ToList());

    private static readonly Lazy<List<(string file, string call, string key)>> KeyCalls =
        new Lazy<List<(string, string, string)>>(ScanKeyCalls);

    private static List<(string file, int line, string literal)> AllCyrillicLiterals() => CyrillicInProduction.Value;

    private static List<(string file, string call, string key)> KeysInCode() => KeyCalls.Value;

    private static List<(string file, string call, string key)> ScanKeyCalls() =>
        ProductionFiles()
            .SelectMany(f => SourceLines.WithoutComments(SourceCorpus.Lines(f))
                .SelectMany(l => KeyCall.Matches(l).Cast<Match>()
                    .Select(m => (Relative(f), m.Groups[1].Value, m.Groups[2].Value))))
            .ToList();

    private static HashSet<string> BaseKeys(StringTable table) =>
        new HashSet<string>(table.Keys.Select(StringTable.BaseKey), StringComparer.Ordinal);

    private static HashSet<string> UsedKeys() =>
        new HashSet<string>(
            KeysInCode().Select(k => k.key).Concat(HintText.Keys.Select(HintText.TableKey)),
            StringComparer.Ordinal);

    [Test]
    public void TheScan_SeesTheInterfaceCode_AndASampleUiFileIsNotExempt()
    {
        var files = ProductionFiles().Select(Relative).ToList();
        Assert.That(files.Count, Is.GreaterThan(400), "скан не нашёл исходников — сторож зеленел бы вхолостую");
        CollectionAssert.Contains(files, "Core/UI/SettingsProjectTab.cs");
        Assert.IsFalse(LocalizationAllowList.Files.ContainsKey("Core/UI/SettingsProjectTab.cs"),
            "образцовая панель обязана оставаться под сторожем");
        Assert.That(KeysInCode().Count, Is.GreaterThan(500), "скан ключей не нашёл вызовов Loc.T/Loc.F");
    }

    [Test]
    public void TheDetector_CatchesAPlantedLabel_AndLetsALogLineThrough()
    {
        var planted = new[]
        {
            "            _rows.AddToggle(page, ref y, \"Сетка\", s.GridEnabled,",
            "            " + Engine + ".LogWarning(\"[Textures] Индекс не найден\");",
            "            " + Engine + ".LogError(",
            "                \"Нет строки «\" + key + \"»\");",
            "            var x = 1; // «Комментарий»",
        };
        var found = CyrillicLiterals("Planted.cs", planted);
        CollectionAssert.AreEqual(new[] { "\"Сетка\"" }, found.Select(f => f.literal).ToArray(),
            "сторож обязан видеть подпись и не видеть журнал, многострочный журнал и комментарий");
    }

    [Test]
    public void NoCyrillicLiteral_RemainsInProductionCode_OutsideTheAllowList()
    {
        var offenders = AllCyrillicLiterals()
            .Where(l => !LocalizationAllowList.Allows(l.file, l.literal))
            .Select(l => l.file + ":" + l.line + "  " + l.literal)
            .ToList();

        Assert.IsEmpty(offenders,
            "Русский литерал в коде интерфейса на английском экране останется русским. Вынесите "
            + "текст в Assets/StreamingAssets/Localization/ru.json (+ en.json и _context.json) и "
            + "зовите Loc.T(\"ключ\") / Loc.F(\"ключ\", …). Если строка НЕ видна человеку "
            + "(журнал, идентификатор данных, инструмент разработчика) — внесите её в "
            + "LocalizationAllowList с причиной. Найдено " + offenders.Count + ":\n"
            + string.Join("\n", offenders.Take(80)));
    }

    [Test]
    public void EveryAllowListEntry_StillMatchesSomething()
    {
        var literals = AllCyrillicLiterals();
        var stale = LocalizationAllowList.Files.Keys
            .Where(f => !File.Exists(Path.Combine(ScriptsDir(), f)))
            .Concat(LocalizationAllowList.Literals
                .Where(e => !literals.Any(l => l.file == e.file && l.literal.Contains(e.fragment)))
                .Select(e => e.file + " ∋ " + e.fragment))
            .ToList();

        Assert.IsEmpty(stale,
            "Исключение пережило свой литерал или файл — и молча освободит от сторожа следующий, "
            + "который займёт это место. Удалите из LocalizationAllowList: " + string.Join(", ", stale));
    }

    [Test]
    public void EveryKeyPassedToLoc_IsALiteral()
    {
        var computed = ProductionFiles()
            .Where(f => !LocalizationAllowList.ComputesKeys.Contains(Relative(f)))
            .SelectMany(f => SourceLines.WithoutComments(SourceCorpus.Lines(f))
                .Where(l => AnyCall.Matches(l).Count > KeyCall.Matches(l).Count)
                .Select(l => Relative(f) + ": " + l.Trim()))
            .ToList();

        Assert.IsEmpty(computed,
            "Ключ перевода пишется литералом: вычисленный ключ скану не виден, и проверка «ключ есть в "
            + "ru.json и en.json» молча пропускает его. " + string.Join(" | ", computed));
    }

    [Test]
    public void EveryKeyUsedInCode_ExistsInRussianAndEnglish()
    {
        var ru = BaseKeys(Table("ru"));
        var en = BaseKeys(Table("en"));
        var missing = UsedKeys()
            .Where(k => !ru.Contains(k) || !en.Contains(k))
            .Select(k => k + (ru.Contains(k) ? "" : " [нет в ru]") + (en.Contains(k) ? "" : " [нет в en]"))
            .ToList();

        Assert.IsEmpty(missing, "На экране вместо текста окажется сам ключ: " + string.Join(", ", missing));
    }

    [Test]
    public void EveryKeyInTheTables_IsUsedByTheCode()
    {
        var used = UsedKeys();
        var dead = BaseKeys(Table("ru")).Where(k => !used.Contains(k)).ToList();

        Assert.IsEmpty(dead,
            "Ключ есть в ru.json, а кода, который его показывает, нет — переводчики переводят мёртвый "
            + "текст. Удалите строку из ru.json, en.json и _context.json: " + string.Join(", ", dead));
    }

    [Test]
    public void EveryKeyOfEveryLanguage_ExistsInTheRussianSource()
    {
        var ru = BaseKeys(Table("ru"));
        var orphans = LanguageCodes()
            .Where(c => c != Localizer.SourceLanguage)
            .SelectMany(c => Table(c).Keys.Where(k => !ru.Contains(StringTable.BaseKey(k))).Select(k => c + ": " + k))
            .ToList();

        Assert.IsEmpty(orphans,
            "Перевод без русского исходника — либо опечатка в ключе, либо исходник удалили, а перевод "
            + "забыли: " + string.Join(", ", orphans));
    }

    [Test]
    public void EverySourceKey_HasATranslatorContextEntry_AndBack()
    {
        var ru = BaseKeys(Table("ru"));
        var context = new HashSet<string>(ContextKeys(), StringComparer.Ordinal);

        var noContext = ru.Where(k => !context.Contains(k)).ToList();
        var noSource = context.Where(k => !ru.Contains(k)).ToList();

        Assert.IsEmpty(noContext, "Переводчику не сказано, где и что это за строка: " + string.Join(", ", noContext));
        Assert.IsEmpty(noSource, "Контекст для ключа, которого нет в ru.json: " + string.Join(", ", noSource));
    }

    [Test]
    public void Placeholders_MatchTheRussianSource_InEveryLanguage()
    {
        var source = Table("ru");
        var bad = new List<string>();
        foreach (var code in LanguageCodes().Where(c => c != Localizer.SourceLanguage))
        {
            var table = Table(code);
            foreach (var key in table.Keys)
            {
                var baseKey = StringTable.BaseKey(key);
                var expected = PlaceholdersOfAllForms(source, baseKey);
                table.TryGet(key, out var text);
                var actual = Placeholders(text);
                if (!actual.SetEquals(expected) && !(key != baseKey && actual.IsSubsetOf(expected)))
                    bad.Add(code + ": " + key + " {" + string.Join(",", actual) + "} ≠ ru {" + string.Join(",", expected) + "}");
            }
        }

        Assert.IsEmpty(bad,
            "Местозаполнители перевода не совпадают с исходником: лишний {n} роняет string.Format, "
            + "потерянный — теряет число на экране. " + string.Join("; ", bad));
    }

    [Test]
    public void EnglishHints_FitTheBubble()
    {
        var en = Table("en");
        var bad = HintText.Keys
            .Select(HintText.TableKey)
            .Where(k => en.TryGet(k, out var text) && (text.Length > 220 || !".!?".Contains(text.TrimEnd().Last())))
            .ToList();

        Assert.IsEmpty(bad, "Подсказка — одно-два законченных предложения не длиннее 220 знаков: " + string.Join(", ", bad));
    }

    [Test]
    public void ShippedLanguages_AreRussianAndEnglishAtLeast_AndRussianIsLeftToRight()
    {
        CollectionAssert.IsSubsetOf(new[] { "ru", "en" }, LanguageCodes().ToList());
        Assert.IsFalse(Table("ru").IsRightToLeft);
        Assert.IsFalse(Table("en").IsRightToLeft);
    }

    private static HashSet<int> PlaceholdersOfAllForms(StringTable table, string baseKey)
    {
        var all = new HashSet<int>();
        foreach (var key in table.Keys.Where(k => StringTable.BaseKey(k) == baseKey))
        {
            table.TryGet(key, out var text);
            all.UnionWith(Placeholders(text));
        }
        return all;
    }

    private static HashSet<int> Placeholders(string text) =>
        new HashSet<int>(Placeholder.Matches(text).Cast<Match>().Select(m => int.Parse(m.Groups[1].Value)));
}
