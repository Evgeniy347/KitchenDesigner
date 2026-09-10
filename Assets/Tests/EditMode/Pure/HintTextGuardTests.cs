using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using KitchenDesigner.Core.UI;
using KitchenDesigner.Tests.Geometry;

/// <summary>
/// Сторож наполнения подсказок. Механизм «i» + облачко заводится один раз, а тексты к
/// сотням контролов пишет отдельный исполнитель — и криво наполнить набор можно ровно
/// двумя способами: заявить у контрола ключ, для которого текста нет (человек видит «i»
/// и получает исключение), либо оставить текст, на который никто не ссылается (мёртвый
/// груз, который потом никто не решится удалить). Оба закрыты встречной парой тестов:
/// множество ключей ИЗ КОДА и множество ключей ИЗ ТЕКСТОВ обязаны совпасть.
///
/// Скан ищет именованный аргумент «hint:» — единственную разрешённую форму записи ключа
/// (см. docs/UI-GUIDELINES.md §13). Именно поэтому <see cref="EveryHintArgument_IsALiteral"/>
/// запрещает вычисляемый ключ: скан по исходнику увидел бы «hint: key» и не смог бы
/// сказать, какой текст требуется, — сторож бы позеленел вхолостую.
/// </summary>
public class HintTextGuardTests
{
    private const int MinTextLength = 30;
    private const int MaxTextLength = 220;
    private const int MaxSentences = 2;

    private static readonly Regex Declared = new Regex(@"\bhint\s*:\s*""([^""]*)""");
    private static readonly Regex AnyHintArgument = new Regex(@"\bhint\s*:");

    private static string CoreDir() => RepoPaths.Subdir("Assets", "Scripts", "Core");

    private static IEnumerable<(string file, string line)> CodeLines() =>
        Directory.GetFiles(CoreDir(), "*.cs", SearchOption.AllDirectories)
            .SelectMany(file => SourceLines.WithoutComments(File.ReadAllLines(file))
                .Select(line => (Path.GetFileName(file), line)));

    private static List<(string file, string key)> KeysInCode() =>
        CodeLines()
            .SelectMany(p => Declared.Matches(p.line).Cast<Match>()
                .Select(m => (p.file, m.Groups[1].Value)))
            .ToList();

    [Test]
    public void TheScan_FindsTheSamplePanel()
    {
        var keys = KeysInCode();
        Assert.That(keys.Count, Is.GreaterThanOrEqualTo(5),
            "Скан по «hint:» не нашёл размеченных контролов — значит он зеленеет вхолостую "
            + "и обе проверки ниже ничего не стерегут. Образец разметки — SettingsViewTab.cs.");
        CollectionAssert.Contains(keys.Select(k => k.file).Distinct().ToList(), "SettingsViewTab.cs",
            "образцовая панель обязана оставаться размеченной — по ней исполнитель копирует форму");
    }

    [Test]
    public void EveryKeyClaimedByAControl_HasText()
    {
        var missing = KeysInCode()
            .Where(k => !HintText.Has(k.key))
            .Select(k => k.file + " → " + k.key)
            .Distinct()
            .ToList();

        Assert.IsEmpty(missing,
            "Контрол заявил ключ подсказки, а текста для него нет: «i» нарисуется, а "
            + "HintText.Of упадёт при построении панели. Добавьте строку в "
            + "Assets/Scripts/Core/Pure/UI/HintText.cs. Без текста: " + string.Join(", ", missing));
    }

    [Test]
    public void EveryText_IsClaimedBySomeControl()
    {
        var claimed = KeysInCode().Select(k => k.key).ToHashSet(StringComparer.Ordinal);
        var dangling = HintText.All.Keys.Where(k => !claimed.Contains(k)).ToList();

        Assert.IsEmpty(dangling,
            "Текст подсказки есть, а контрола с таким ключом в коде нет — висячий ключ. "
            + "Либо разметьте контрол «hint: \"ключ\"», либо удалите строку из HintText. "
            + "Висят: " + string.Join(", ", dangling));
    }

    [Test]
    public void EveryHintArgument_IsALiteral()
    {
        var computed = CodeLines()
            .Where(p => AnyHintArgument.IsMatch(p.line) && !Declared.IsMatch(p.line))
            .Select(p => p.file + ": " + p.line.Trim())
            .ToList();

        Assert.IsEmpty(computed,
            "Ключ подсказки пишется литералом в той же строке, что и «hint:». Вычисляемый "
            + "ключ скану не виден, и сторож перестаёт видеть половину набора: "
            + string.Join(" | ", computed));
    }

    [Test]
    public void EveryKeyInTheDictionary_FollowsTheNamingShape()
    {
        var wrong = HintText.All.Keys.Where(k => !HintKey.IsValid(k)).ToList();

        Assert.IsEmpty(wrong,
            "Ключ подсказки — 2–4 сегмента вида «settings.view.wallOutline». Не по форме: "
            + string.Join(", ", wrong));
    }

    [Test]
    public void EveryText_IsOneOrTwoRussianSentences()
    {
        var bad = new List<string>();

        foreach (var kv in HintText.All)
        {
            var text = kv.Value;
            if (text.Length < MinTextLength) bad.Add(kv.Key + ": короче " + MinTextLength + " знаков");
            if (text.Length > MaxTextLength) bad.Add(kv.Key + ": длиннее " + MaxTextLength + " знаков");
            if (text != text.Trim()) bad.Add(kv.Key + ": пробел по краям");
            if (!text.Any(IsCyrillic)) bad.Add(kv.Key + ": текст не по-русски");
            if (text.Length > 0 && !".!?".Contains(text[text.Length - 1])) bad.Add(kv.Key + ": нет точки в конце");
            if (Sentences(text) > MaxSentences) bad.Add(kv.Key + ": больше двух предложений");
        }

        Assert.IsEmpty(bad,
            "Подсказка — одно-два предложения по-русски, законченных точкой. Нарушения: "
            + string.Join("; ", bad));
    }

    [Test]
    public void TheDictionary_IsNotEmpty()
    {
        Assert.That(HintText.All.Count, Is.GreaterThanOrEqualTo(5),
            "пустой словарь сделал бы все проверки выше зелёными и бессмысленными");
    }

    private static bool IsCyrillic(char c) => c >= 'А' && c <= 'я';

    private static int Sentences(string text) => text.Count(c => ".!?".Contains(c));
}
