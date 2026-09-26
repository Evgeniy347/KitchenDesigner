using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using KitchenDesigner.Tests.Geometry;

/// <summary>L6/L8: RecentProjectRowSource.For used to call ProjectFileVersion.Of(path) and
/// ProjectFileCreatedAt.Of(path) back to back - each one opens and reads the WHOLE project
/// file from disk on its own. Opening the Load window reads up to
/// RecentProjectsList.Capacity recent files this way, so every open paid for the same
/// multi-megabyte save file twice. Both classes already expose an .In(json) overload for an
/// in-memory string; the row source is expected to read the file once and hand that same
/// text to both, instead of re-reading it per field.</summary>
public class RecentProjectRowSourceReadCountTests
{
    [Test]
    public void For_ReadsTheProjectFileAtMostOnce()
    {
        string path = Path.Combine(
            RepoPaths.Subdir("Assets", "Scripts", "Core", "Persistence"),
            "RecentProjectRowSource.cs");
        string source = File.ReadAllText(path);

        int reads = Regex.Matches(source, @"File\.ReadAllText").Count;
        Assert.LessOrEqual(reads, 1,
            "RecentProjectRowSource читает файл проекта больше одного раза за вызов For() - "
            + "«Загрузить» показывает до 10 недавних файлов, и каждое открытие окна платит "
            + "за один и тот же файл дважды. Найдено вызовов File.ReadAllText: " + reads);

        bool readsVersionByPath = Regex.IsMatch(source, @"ProjectFileVersion\.Of\(");
        bool readsCreatedByPath = Regex.IsMatch(source, @"ProjectFileCreatedAt\.Of\(");
        Assert.IsFalse(readsVersionByPath && readsCreatedByPath,
            "ProjectFileVersion.Of и ProjectFileCreatedAt.Of читают файл каждый заново - "
            + "берите оба значения через общий .In(json) от одного File.ReadAllText");
    }
}
