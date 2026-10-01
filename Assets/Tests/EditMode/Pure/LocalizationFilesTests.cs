using System.IO;
using System.Linq;
using KitchenDesigner.Core;
using NUnit.Framework;

public class LocalizationFilesTests
{
    [Test]
    public void Resolve_FindsTheShippedFolder_FromTheTestRunner()
    {
        var directory = LocalizationFiles.Resolve();
        Assert.IsNotNull(directory,
            "без каталога Loc отдаёт ключи вместо текста; поиск вверх от рабочего каталога обязан его найти "
            + "и под dotnet test, и в редакторе Unity");
        Assert.IsTrue(File.Exists(Path.Combine(directory!, "ru.json")));
    }

    [Test]
    public void LoadAll_ReadsRussianAndEnglish_AndSkipsTheContextFile()
    {
        var tables = LocalizationFiles.LoadAll(LocalizationFiles.Resolve()!);
        var codes = tables.Select(t => t.Language).ToList();

        CollectionAssert.Contains(codes, "ru");
        CollectionAssert.Contains(codes, "en");
        CollectionAssert.DoesNotContain(codes, "_context",
            "файл контекста для переводчиков — не язык, в выпадающем списке его быть не должно");
    }

    [Test]
    public void ShippedLanguages_CarryTheirNativeNames()
    {
        var localizer = LocalizationFiles.Load("ru");
        var names = localizer.Languages.ToDictionary(l => l.Code, l => l.NativeName);

        Assert.AreEqual("Русский", names["ru"]);
        Assert.AreEqual("English", names["en"],
            "список «Язык» показывает языки их собственными именами — человек, не читающий по-русски, "
            + "должен найти свой язык");
    }

    [Test]
    public void IsLanguageFile_ExcludesUnderscoreFiles()
    {
        Assert.IsTrue(LocalizationFiles.IsLanguageFile("ar.json"));
        Assert.IsFalse(LocalizationFiles.IsLanguageFile("_context.json"));
        Assert.IsFalse(LocalizationFiles.IsLanguageFile("ru.json.meta"));
    }
}
