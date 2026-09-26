using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using KitchenDesigner.Tests.Geometry;

/// <summary>L1: a Latin "o" (U+006F) hid inside "Однoскатная" (RoofFieldsEditor.cs dropdown
/// item) and inside the matching HintText entry - invisible in an editor at normal zoom,
/// spotted only by dumping bytes. Genuine mixed-script lines mix in a Latin technical term
/// ("Ctrl+Z", "PageUp", "CS0246") all the time, but always across a space, dash or
/// punctuation - never with a Latin letter touching a Cyrillic one inside the same word. A
/// hit on that boundary is a mistyped character, not an intentional string.</summary>
public class MixedScriptWordGuardTests
{
    private static readonly Regex MixedScriptBoundary =
        new Regex(@"[а-яёА-ЯЁ][a-zA-Z]|[a-zA-Z][а-яёА-ЯЁ]");

    [Test]
    public void NoStringLiteralInCoreSources_MixesLatinAndCyrillicInsideAWord()
    {
        var hits = new List<string>();
        foreach (var file in SourceCorpus.Files(RepoPaths.Subdir("Assets", "Scripts", "Core")))
            foreach (var line in SourceLines.WithoutComments(SourceCorpus.Lines(file)))
                if (MixedScriptBoundary.IsMatch(line))
                    hits.Add(Path.GetFileName(file) + ": " + line.Trim());

        Assert.IsEmpty(hits,
            "Латинская буква вплотную к кириллической ВНУТРИ одного слова - почти всегда "
            + "опечатка (латинская «o» вместо кириллической «о» и т.п.), а не осознанный "
            + "смешанный текст: настоящие технические термины всегда отделены пробелом или "
            + "знаком препинания. Найдено: " + string.Join(" | ", hits));
    }
}
