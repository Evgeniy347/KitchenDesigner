#if UNITY_EDITOR || DEVELOPMENT_BUILD

using System.Globalization;
using System.IO;
using System.Threading;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

public class PerfCsvLogTests
{
    private static readonly string[] Header = { "frame", "dt_ms" };

    private static PerfCsvLog Started(int capacityFrames)
    {
        var log = new PerfCsvLog(Header, capacityFrames);
        log.Start();
        return log;
    }

    [Test]
    public void Append_BeforeStart_RecordsNothing()
    {
        var log = new PerfCsvLog(Header, 4);
        log.Append(new[] { 1f, 2f });
        Assert.AreEqual(0, log.Rows);
    }

    [Test]
    public void Append_WithAWrongNumberOfValues_IsIgnored()
    {
        var log = Started(4);
        log.Append(new[] { 1f, 2f, 3f });
        Assert.AreEqual(0, log.Rows,
            "строка обязана совпадать с заголовком по числу колонок, иначе CSV "
            + "разъезжается и все последующие кадры читаются не по тем столбцам");
    }

    [Test]
    public void Append_WhenTheBufferIsFull_StopsRecording_InsteadOfOverwritingTheFirstFrames()
    {
        var log = Started(2);
        log.Append(new[] { 1f, 10f });
        log.Append(new[] { 2f, 20f });
        Assert.IsTrue(log.Full);

        log.Append(new[] { 3f, 30f });

        Assert.IsFalse(log.Recording, "переполнение останавливает запись, а не заворачивается по кругу");
        Assert.AreEqual(2, log.Rows);
        StringAssert.Contains("1;10", log.BuildCsv(),
            "интересен обычно ПЕРВЫЙ прогон целиком: кольцевой буфер затёр бы именно "
            + "его начало и оставил бы хвост, к которому нет разгона");
        StringAssert.DoesNotContain("3;30", log.BuildCsv());
    }

    [Test]
    public void BuildCsv_StartsWithTheHeaderRow()
    {
        var log = Started(2);
        log.Append(new[] { 1f, 10f });
        StringAssert.StartsWith("frame;dt_ms\n", log.BuildCsv());
    }

    [Test]
    public void FormatCell_UsesADotUnderARussianLocale_SoTheSeparatorStaysUnambiguous()
    {
        var before = Thread.CurrentThread.CurrentCulture;
        try
        {
            Thread.CurrentThread.CurrentCulture = new CultureInfo("ru-RU");
            Assert.AreEqual("1.5", PerfCsvLog.FormatCellInvariantly(1.5f),
                "под русской локалью запятая оказалась бы и в разделителе колонок, "
                + "и в дробной части — файл перестал бы разбираться вообще");
        }
        finally
        {
            Thread.CurrentThread.CurrentCulture = before;
        }
    }

    [Test]
    public void Stop_WithNothingRecorded_ReturnsNull_AndWritesNoFile()
    {
        var log = Started(4);
        Assert.IsNull(log.Stop());
        Assert.IsFalse(log.Recording);
    }

    [Test]
    public void NothingReachesTheDisk_UntilStop_SoFileIoStaysOutOfTheMeasuredFrames()
    {
        var dir = PerfOutputDir();
        int before = FileCount(dir);

        var log = Started(4);
        log.Append(new[] { 1f, 10f });
        log.Append(new[] { 2f, 20f });

        Assert.AreEqual(before, FileCount(dir),
            "буфер преаллоцирован и запись идёт в память: файловый I/O внутри Append "
            + "попал бы ровно в те кадры, которые мы измеряем");

        var path = log.Stop();
        Assert.IsNotNull(path, "после Stop файл обязан появиться");
        Assert.IsTrue(File.Exists(path!));
        StringAssert.Contains("1;10", File.ReadAllText(path!));
    }

    [Test]
    public void Stop_PutsTheFileUnderTheRepoRoot_FoundByWalkingUpToBuildCmd()
    {
        var log = Started(2);
        log.Append(new[] { 1f, 10f });
        var path = log.Stop();

        Assert.IsNotNull(path);
        var perfDir = new DirectoryInfo(Path.GetDirectoryName(path!)!);
        Assert.AreEqual("perf", perfDir.Name);
        Assert.AreEqual("test-results", perfDir.Parent!.Name);
        Assert.IsTrue(File.Exists(Path.Combine(perfDir.Parent!.Parent!.FullName, "build.cmd")),
            "корень ищется вверх по дереву до папки с build.cmd: в редакторе dataPath "
            + "это Assets/, в сборке — KitchenDesigner_Data/, и фиксированное число "
            + "«..» промахнулось бы в одном из двух случаев");
    }

    private static string PerfOutputDir()
    {
        var root = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        return Path.Combine(root, "test-results", "perf");
    }

    private static int FileCount(string dir) =>
        Directory.Exists(dir) ? Directory.GetFiles(dir).Length : 0;
}

#endif
