using System;
using System.Linq;
using KitchenDesigner.Core;
using KitchenDesigner.Core.UI;
using NUnit.Framework;

/// <summary>
/// «О программе» показывает сведения таблицей подпись | значение. Подпись берётся из тех же
/// строк локализации, что и «скопировать сведения» (<see cref="AboutLines"/>), — отрезая от
/// шаблона «Сборка: {0}» всё, что идёт после и включая подстановку, поэтому новых ключей нет,
/// а язык с другим двоеточием не ломает таблицу.
/// </summary>
public class AboutRowsTests
{
    private static readonly string[] Languages =
        { "ru", "en", "de", "es", "fr", "it", "pt", "ja", "zh-Hans", "ar-TN" };

    private static AboutEnvironment Environment() =>
        new AboutEnvironment("0.1234", "2026-10-03 09:00", "WindowsPlayer", "6000.4.3f1", "Direct3D11",
            "NVIDIA GeForce RTX 3060");

    private static IDisposable In(string language) => Loc.Scope(Loc.Current.WithLanguage(language));

    [Test]
    public void Rows_AreBuildPlatformUnityApiGpu_WithTheirValuesAndRussianCaptions()
    {
        using var _ = In("ru");
        var rows = AboutRows.For(Environment());

        CollectionAssert.AreEqual(new[] { "Сборка", "Платформа", "Unity", "Графика (API)", "Видеокарта" },
            rows.Select(r => r.Caption).ToList(), "подпись — это шаблон без подстановки и без двоеточия");
        CollectionAssert.AreEqual(new[] { "2026-10-03 09:00", "WindowsPlayer", "6000.4.3f1", "Direct3D11",
                "NVIDIA GeForce RTX 3060" },
            rows.Select(r => r.Value).ToList(), "значение — дословно то, что принесла среда");
    }

    [TestCaseSource(nameof(Languages))]
    public void EveryCaption_IsNonEmpty_AndFreeOfThePlaceholderAndTheColon(string language)
    {
        using var _ = In(language);

        foreach (var row in AboutRows.For(Environment()))
        {
            Assert.IsNotEmpty(row.Caption, language);
            StringAssert.DoesNotContain("{0}", row.Caption, language);
            Assert.IsFalse(row.Caption.EndsWith(":") || row.Caption.EndsWith("："),
                $"{language}: подпись «{row.Caption}» оканчивается двоеточием");
        }
    }

    [Test]
    public void ALanguageScope_IsPrivateToItsThread_AndLeavesTheGlobalLanguageAlone()
    {
        string globalLanguage = Loc.Language;
        int revision = Loc.Revision;
        string? seenByNeighbour = null;

        using (In("ja"))
        {
            var neighbour = new System.Threading.Thread(() => seenByNeighbour = Loc.Language);
            neighbour.Start();
            neighbour.Join();
            Assert.AreEqual("ja", Loc.Language, "внутри области язык свой");
        }

        Assert.AreEqual(globalLanguage, seenByNeighbour,
            "фикстуры идут параллельно, а Loc глобален: язык, выставленный одной, видел бы чужой тест посреди " +
            "прогона — так «ja» попадал в проверку русских подписей");
        Assert.AreEqual(revision, Loc.Revision, "область не трогает глобальный язык");
        Assert.AreEqual(globalLanguage, Loc.Language, "после области язык вернулся");
    }

    [Test]
    public void TheRowsAndTheCopiedLines_CarryTheSameValues_InTheSameOrder()
    {
        using var _ = In("en");
        var env = Environment();
        var lines = AboutLines.For(env);
        var rows = AboutRows.For(env);

        for (int i = 0; i < rows.Count; i++)
            StringAssert.Contains(rows[i].Value, lines[i + 1],
                "строка копирования номер " + (i + 1) + " обязана нести значение той же строки таблицы");
    }
}
