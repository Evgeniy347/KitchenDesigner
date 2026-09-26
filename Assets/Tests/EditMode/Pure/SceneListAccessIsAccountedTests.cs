using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using KitchenDesigner.Core;
using KitchenDesigner.Tests.Geometry;

/// <summary>Сторож НА САМ ПРИБОР, а не на код, который прибор меряет.
///
/// Нашёл дыру ревью: <c>SceneScanLog</c>, <c>SceneScanCounter</c> и колонка
/// <c>getall_calls</c> считали только <c>PartRegistry.GetAll()</c>. Второй путь к
/// тому же списку — свойство <c>PartRegistry.All</c>, 43 места в 26 файлах — не
/// назывался ни одним из них. Три следствия, по возрастанию тяжести: строка
/// <c>[Perf] кадр N: … — имена</c> читалась как ПОЛНАЯ, хотя полной не была; любой
/// сторож вида «ноль обходов» обходился заменой <c>GetAll()</c> на <c>.All</c>
/// молча и с зелёными тестами; и поиск следующего виновника этим прибором мог
/// снова назвать не того.
///
/// Назвать <c>.All</c> по имени вызывающего нельзя: <c>CallerMemberName</c> живёт
/// на ПАРАМЕТРЕ, а у свойства параметров нет. Поэтому выбор такой: **дешёвый путь
/// СЧИТАЕТСЯ отдельным числом и честно объявляется безымянным**
/// (<c>«и 12 раз список отдан без копии через PartRegistry.All — имён нет»</c>), а
/// от бесшумного роста его защищает не счётчик, а этот скан: он ловит ПОЯВЛЕНИЕ
/// нового места, а не его срабатывание.
///
/// Счёт нарочно ведётся ОТДЕЛЬНО от <c>Scans</c>: на <c>Scans</c> стоят чужие
/// сторожи (<c>SceneRestoreCostTests</c>), и подмешать туда второй путь значило бы
/// поменять смысл их чисел задним числом.
///
/// Потолок здесь точный, как у <c>CommentRatchetTests</c>: выше — падение с именем
/// файла, ниже — тоже падение, с просьбой опустить число. Второе не придирка:
/// незакрытый храповик отдаёт назад ровно то, что только что вычистили.
///
/// [Parallelizable(ParallelScope.None)]: TheTwoCountsStayApart_SoOldNumbersKeepTheirMeaning проверяет точную
/// дельту на process-global SceneScanCounter.Scans/Shares; под ParallelScope.Fixtures сосед
/// на другом потоке (SceneScanLogTests, SceneScanCounterTests) вклинил бы свой Note() между
/// «before» и проверкой (SceneScanCounterIsolationTests).</summary>
[Parallelizable(ParallelScope.None)]
public class SceneListAccessIsAccountedTests
{
    private static readonly (string file, int uses)[] TheUnnamedPath =
    {
        ("Infrastructure/SceneChangeTracker.cs", 5),
        ("Elements/WallOpeningElement.cs", 3),
        ("Elements/FacadeElement.cs", 3),
        ("Elements/DrawerLinks.cs", 3),
        ("UI/SpecificationPanelUI.cs", 2),
        ("UI/HierarchyPanelUI.cs", 2),
        ("Infrastructure/ElementNaming.cs", 2),
        ("Elements/PartCutoutElement.cs", 2),
        ("Elements/LightSwitchNetwork.cs", 2),
        ("Elements/DrawerElement.cs", 2),
        ("Elements/CutoutBody.cs", 2),
        ("Elements/AttachLinks.cs", 2),
        ("Validation/OpeningCollision.cs", 1),
        ("UI/AttachTargetHover.cs", 1),
        ("Rendering/HoverAnchor.cs", 1),
        ("Rendering/EdgeOutlineRenderer.cs", 1),
        ("Persistence/SceneElements.cs", 1),
        ("Measure/MeasureController.cs", 1),
        ("MCP/McpCommandHandler.Helpers.cs", 1),
        ("MCP/McpCommandHandler.Elements.Mutation.cs", 1),
        ("Infrastructure/SceneVisibilityManager.cs", 1),
        ("Elements/WallProximity.cs", 1),
        ("Elements/PartMount.cs", 1),
        ("Elements/DishwasherElement.cs", 1),
        ("Elements/AttachRider.cs", 1),
        ("Commands/ConvertElementCommand.cs", 1),
    };

    /// <summary>Каталоги самого прибора: там «PartRegistry.All» встречается как
    /// ТЕКСТ — это строка, которую прибор печатает, и имя, которое он объясняет.
    /// Скан по образцу не отличает строковый литерал от вызова, поэтому владелец
    /// образца исключается явно, как это уже сделано у соседних сторожей.</summary>
    private static readonly string[] WhereTheNameIsJustText =
    {
        Path.Combine("Core", "Pure", "Diagnostics"),
        Path.Combine("Core", "Diagnostics"),
    };

    private const string TheRule =
        "ПРАВИЛО: список деталей выдают два пути. PartRegistry.GetAll() называет "
        + "вызывающего сам (CallerMemberName/CallerFilePath) и попадает в строку "
        + "[Perf] поимённо. PartRegistry.All назвать себя не может — у свойства нет "
        + "параметров, — поэтому он только СЧИТАЕТСЯ, отдельным безымянным числом. "
        + "Новое место .All делает прибор слепее ровно на себя: либо возьми "
        + "GetAll(), либо впиши файл сюда с причиной, по которой копия списка здесь "
        + "не годится.";

    private static string CoreDir() => RepoPaths.Subdir("Assets", "Scripts", "Core");

    private static bool IsTheInstrumentsOwnText(string file)
    {
        foreach (var dir in WhereTheNameIsJustText)
            if (file.Contains(dir, StringComparison.Ordinal)) return true;
        return false;
    }

    internal static int UnnamedUsesIn(string[] lines)
    {
        int uses = 0;
        foreach (var line in lines)
        {
            string trimmed = line.TrimStart();
            if (trimmed.StartsWith("//", StringComparison.Ordinal)
                || trimmed.StartsWith("*", StringComparison.Ordinal)) continue;

            int at = 0;
            while ((at = line.IndexOf("PartRegistry.All", at, StringComparison.Ordinal)) >= 0)
            {
                int after = at + "PartRegistry.All".Length;
                bool wholeWord = after >= line.Length
                    || (!char.IsLetterOrDigit(line[after]) && line[after] != '_');
                if (wholeWord) uses++;
                at = after;
            }
        }
        return uses;
    }

    private static Dictionary<string, int> UsesFoundInTheCore()
    {
        var found = new Dictionary<string, int>(StringComparer.Ordinal);
        string root = CoreDir();
        foreach (var file in SourceCorpus.Files(root))
        {
            if (IsTheInstrumentsOwnText(file)) continue;
            int uses = UnnamedUsesIn(SourceCorpus.Lines(file));
            if (uses == 0) continue;
            found[Path.GetRelativePath(root, file).Replace('\\', '/')] = uses;
        }
        return found;
    }

    [Test]
    public void TheScan_SeesARealCall_AndIgnoresACommentedOne()
    {
        Assert.IsNotEmpty(SourceCorpus.Files(CoreDir()),
            "исходников ядра не найдено — сторож проверяет пустоту и потому бесполезен");

        Assert.AreEqual(1, UnnamedUsesIn(new[] { "var all = PartRegistry.All;" }),
            "скан не видит обычного вызова — он зелен на чём угодно");
        Assert.AreEqual(0, UnnamedUsesIn(new[] { "// раньше тут был PartRegistry.All" }),
            "закомментированный вызов — не вызов");
        Assert.AreEqual(0, UnnamedUsesIn(new[] { "var x = PartRegistry.AllWalls;" }),
            "PartRegistry.AllWalls — другое имя, скан не имеет права его считать");
        Assert.AreEqual(2, UnnamedUsesIn(new[] { "F(PartRegistry.All, PartRegistry.All);" }),
            "два вызова в строке — это два вызова");
    }

    [Test]
    public void EveryUseOfTheUnnamedPath_IsOnTheList()
    {
        var found = UsesFoundInTheCore();
        var listed = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var (file, uses) in TheUnnamedPath) listed[file] = uses;

        var complaints = new List<string>();
        foreach (var pair in found)
        {
            if (!listed.TryGetValue(pair.Key, out int ceiling))
            {
                complaints.Add($"{pair.Key}: {pair.Value} — нового файла нет в списке");
                continue;
            }
            if (pair.Value > ceiling)
                complaints.Add($"{pair.Key}: {pair.Value} против потолка {ceiling} — "
                    + "мест стало больше");
        }

        Assert.IsEmpty(complaints, TheRule + "\nНайдено: " + string.Join("; ", complaints));
    }

    [Test]
    public void TheList_DoesNotOutlive_TheFilesItNames()
    {
        var found = UsesFoundInTheCore();
        var complaints = new List<string>();

        foreach (var (file, uses) in TheUnnamedPath)
        {
            string full = Path.Combine(CoreDir(), file.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(full))
            {
                complaints.Add($"{file}: файла больше нет — вычеркни строку");
                continue;
            }
            int now = found.TryGetValue(file, out int n) ? n : 0;
            if (now < uses)
                complaints.Add($"{file}: осталось {now} из {uses} — опусти число, "
                    + "иначе храповик отдаст назад только что вычищенное");
        }

        Assert.IsEmpty(complaints, string.Join("; ", complaints));
    }

    [Test]
    public void TheNamedPath_StillNamesItself()
    {
        string registry = Path.Combine(RepoPaths.Subdir("Assets", "Scripts", "Core",
            "Infrastructure"), "PartRegistryInstance.cs");
        string text = SourceCorpus.Text(registry);

        StringAssert.Contains("CallerMemberName", text,
            "GetAll перестал брать имя вызывающего — поимённой половины прибора больше нет");
        StringAssert.Contains("CallerFilePath", text,
            "GetAll перестал брать файл вызывающего — в строке [Perf] останутся одни методы");
    }

    [Test]
    public void TheUnnamedPath_IsStillCounted()
    {
        string registry = Path.Combine(RepoPaths.Subdir("Assets", "Scripts", "Core",
            "Infrastructure"), "PartRegistryInstance.cs");
        string text = SourceCorpus.Text(registry);

        StringAssert.Contains("NoteTheListWasSharedWithoutAName", text,
            "свойство All перестало считаться — дешёвый путь снова невидим, "
            + "и строка [Perf] снова читается как полная");
    }

    [Test]
    public void TheTwoCountsStayApart_SoOldNumbersKeepTheirMeaning()
    {
        long scans = SceneScanCounter.Scans;
        long shares = SceneScanCounter.Shares;

        SceneScanCounter.NoteShare();

        Assert.AreEqual(scans, SceneScanCounter.Scans,
            "безымянная выдача списка подмешалась в Scans — числа чужих сторожей "
            + "(SceneRestoreCostTests) поменяли смысл задним числом");
        Assert.AreEqual(shares + 1, SceneScanCounter.Shares, "а сама считаться обязана");
    }
}
