using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using KitchenDesigner.Tests.Geometry;
using NUnit.Framework;

/// <summary>
/// У сообщения строки состояния ОДИН путь, и на нём стоит запись в историю
/// (`CONVENTIONS.md` → «A derived field needs ONE writer and one occasion to
/// call it»). До этой правки таких путей было шесть: пять мест звали
/// `StatusBarUI.Instance?.ShowTransient` напрямую, шестым шёл `StatusBarSink`.
/// Пока дверь одна, история полна по построению; появится вторая — половина
/// сообщений перестанет доезжать, и заметить это будет нечем: строка состояния
/// покажет их как ни в чём не бывало, а в консоли их просто не будет.
///
/// Поэтому сторожат не список из пяти вызывающих (он устареет в тот день, когда
/// появится шестой), а МЕХАНИЗМ внутри самого StatusBarUI: текст на плашку
/// кладут только приватные методы, новое сообщение заводит только
/// `ShowTransient`, и `ShowTransient` пишет в `ConsoleLog`. Обходной путь — это
/// новый публичный метод, красящий плашку мимо `ShowTransient`; он валит R1 или
/// R2, ещё не будучи никем вызванным.
///
/// Скан по пути, который перестал резолвиться, зеленеет, ничего не проверив,
/// поэтому у сканера есть собственные тесты: ему подсовывают синтетический
/// исходник с обходным путём и требуют, чтобы он его НАШЁЛ.
/// </summary>
public class StatusBarSingleDoorTests
{
    private const string TheDoor = "ShowTransient";

    /// <summary>Строка объявления метода: модификатор доступа, что-то без «=»,
    /// затем имя и открывающая скобка. Свойства («public bool HasActive =>»)
    /// и поля с инициализатором («private static readonly Color X = new
    /// Color(…)») отсекает именно запрет на «=» до имени.</summary>
    private static readonly Regex MethodHeader =
        new Regex(@"^\s*(public|internal|protected|private)[^=]*?\b(\w+)\s*\(");

    private static readonly Regex PaintsTheChip =
        new Regex(@"_label\s*!?\s*\.\s*text\s*=[^=]");

    private static readonly Regex SeedsAMessage =
        new Regex(@"new\s+Message|_queue\s*\.\s*Enqueue\s*\(");

    private static readonly Regex WritesTheHistory =
        new Regex(@"ConsoleLog\s*\.\s*Shared\s*\.\s*Append\s*\(");

    internal readonly struct Site
    {
        public Site(int line, string method, bool exposed, string code)
        {
            Line = line;
            Method = method;
            Exposed = exposed;
            Code = code;
        }

        public int Line { get; }
        public string Method { get; }
        public bool Exposed { get; }
        public string Code { get; }

        public override string ToString() => Line + ": " + Method + " | " + Code.Trim();
    }

    internal static List<Site> SitesMatching(IEnumerable<string> lines, Regex what)
    {
        var found = new List<Site>();
        string method = "<вне метода>";
        bool exposed = false;
        int number = 0;

        foreach (var line in lines)
        {
            number++;
            var header = MethodHeader.Match(line);
            if (header.Success)
            {
                method = header.Groups[2].Value;
                exposed = header.Groups[1].Value is "public" or "internal";
            }

            if (what.IsMatch(line)) found.Add(new Site(number, method, exposed, line));
        }

        return found;
    }

    private static string[] StatusBarSource() => File.ReadAllLines(Path.Combine(
        RepoPaths.Subdir("Assets", "Scripts", "Core", "UI"), "StatusBarUI.cs"));

    [Test]
    public void R1_TheChipIsPainted_OnlyFromPrivateMethods()
    {
        var outside = SitesMatching(StatusBarSource(), PaintsTheChip)
            .Where(s => s.Exposed).ToArray();

        CollectionAssert.IsEmpty(outside,
            "публичный метод, кладущий текст на плашку, — это вторая дверь: он покажет "
            + "сообщение и не запишет его в историю, и никто этого не заметит, потому что "
            + "строка состояния выглядит ровно так же. Красить плашку имеют право только "
            + "приватные методы, а снаружи дверь одна — " + TheDoor);
    }

    [Test]
    public void R2_ANewMessage_IsBornOnlyInTheOneDoor()
    {
        var elsewhere = SitesMatching(StatusBarSource(), SeedsAMessage)
            .Where(s => s.Method != TheDoor).ToArray();

        CollectionAssert.IsEmpty(elsewhere,
            "сообщение заводит ровно один метод. Второй такой обошёл бы и запись в "
            + "историю, и минимальный таймаут, и склейку повторов — три правила разом");
    }

    [Test]
    public void R3_TheOneDoor_WritesTheHistory_Once()
    {
        var writes = SitesMatching(StatusBarSource(), WritesTheHistory);

        Assert.AreEqual(1, writes.Count,
            "запись в историю ровно одна: две означали бы, что какой-то путь пишет дважды, "
            + "ноль — что история молча опустела. Найдено: "
            + string.Join(", ", writes.Select(s => s.ToString())));
        Assert.AreEqual(TheDoor, writes[0].Method,
            "и стоит она в самой двери, а не в Render: Render зовётся каждый кадр и "
            + "залил бы ленту одним и тем же сообщением");
    }

    [Test]
    public void R4_TheHistory_HasExactlyTwoWritersInTheWholeApp()
    {
        var scripts = RepoPaths.Subdir("Assets", "Scripts");
        var writers = Directory.GetFiles(scripts, "*.cs", SearchOption.AllDirectories)
            .Where(f => !Path.GetFileName(f).StartsWith("ConsoleLog", StringComparison.Ordinal))
            .Where(f => File.ReadLines(f).Any(WritesTheHistory.IsMatch))
            .Select(f => Path.GetFileName(f))
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToArray();

        CollectionAssert.AreEqual(new[] { "ConsoleOverlay.cs", "StatusBarUI.cs" }, writers,
            "у ленты два источника и оба названы: журнал Unity (ConsoleOverlay) и строка "
            + "состояния (StatusBarUI). Третий обязан быть решением, а не побочным "
            + "эффектом: лента — общий кольцевой буфер на " + KitchenDesigner.Core.UI.ConsoleLog.Capacity
            + " строк, и новый болтливый писатель вытеснит из неё именно то, за чем "
            + "человек её и открыл");
    }

    [Test]
    public void TheScan_FindsABypassThatWasDeliberatelyPlanted()
    {
        var withABypass = new[]
        {
            "        public void FlashSomething(string t)",
            "        {",
            "            _label!.text = t;",
            "        }",
        };

        var found = SitesMatching(withABypass, PaintsTheChip);

        Assert.AreEqual(1, found.Count, "сканер обязан видеть подсунутый обходной путь");
        Assert.IsTrue(found[0].Exposed, "и понимать, что метод публичный");
        Assert.AreEqual("FlashSomething", found[0].Method,
            "строка приписывается тому методу, в котором она стоит — без этого R1 и R2 "
            + "судили бы не о том месте");
    }

    [Test]
    public void TheScan_DoesNotCallAPrivatePainterABypass()
    {
        var legitimate = new[]
        {
            "        private void Render()",
            "        {",
            "            if (_label.text != msg.Text) _label.text = msg.Text;",
            "        }",
        };

        var found = SitesMatching(legitimate, PaintsTheChip);

        Assert.AreEqual(1, found.Count);
        Assert.IsFalse(found[0].Exposed,
            "приватная отрисовка — не дверь. Сканер, зовущий обходным путём ВСЁ подряд, "
            + "заставил бы отключить R1 при первой же правке Render");
    }

    [Test]
    public void TheScan_TellsAComparisonFromAnAssignment()
    {
        Assert.IsFalse(PaintsTheChip.IsMatch("            if (_label.text != msg.Text)"),
            "сравнение текста плашки — чтение, а не запись");
        Assert.IsFalse(PaintsTheChip.IsMatch("            if (_label!.text == label) return;"),
            "и «==» тоже: иначе каждое условие в Render считалось бы покраской");
        Assert.IsTrue(PaintsTheChip.IsMatch("            _label!.text = label;"),
            "а настоящее присваивание — обязано находиться, иначе R1 зелен всегда");
    }

    [Test]
    public void TheScan_ActuallyReadsTheRealFile()
    {
        var source = StatusBarSource();

        Assert.Greater(source.Length, 100,
            "файл прочитан: скан по несуществующему пути зеленеет, ничего не проверив");
        CollectionAssert.IsNotEmpty(SitesMatching(source, PaintsTheChip),
            "в StatusBarUI СЕГОДНЯ есть покраска плашки — пустой список означает сломанный "
            + "сканер, а не безупречный класс");
        CollectionAssert.IsNotEmpty(SitesMatching(source, SeedsAMessage),
            "и заведение сообщения тоже есть");
    }
}
