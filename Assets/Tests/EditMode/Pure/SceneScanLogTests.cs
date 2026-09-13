using NUnit.Framework;
using KitchenDesigner.Core;

/// <summary>Прибор, названный дампом <c>perf_20260912_185912.csv</c>: три кадра
/// по ~200 мс (f2588, f2747, f3514) с аллоком, одинаковым ДО БАЙТА
/// (6 480 898 / 6 480 938 / 6 482 388), и ни один маркер их не покрывает. Общая
/// у них одна цифра — колонка <c>getall_calls</c> = 7 при обычных 0–2. То есть
/// кадр семь раз обходит всю сцену, и кто именно её обходит, дамп не говорит.
///
/// <c>PartRegistry.GetAll</c> имеет 32 места вызова в 32 файлах — угадывать
/// среди них и есть та ошибка, которую вся эта охота запрещает. Поэтому вызов
/// называет себя сам, через <c>CallerMemberName</c>/<c>CallerFilePath</c>: это
/// константы времени компиляции, на кадре они не стоят ничего, а
/// <c>PerfMonitor</c> печатает их для кадра дороже
/// <c>PerfMonitor.SlowFrameMs</c>.
///
/// Здесь проверяется сам прибор, и на быстром наборе: тип извлекается из ПУТИ
/// (у половины файлов имя вида <c>McpCommandHandler.Elements.Query.cs</c> — по
/// первой точке, а не по последней), повторы одного места схлопываются в «×N»
/// вместо шестнадцати одинаковых строк, а <c>Take</c> отдаёт кадр и сбрасывает
/// счётчик, иначе следующий кадр унаследует чужие обходы.</summary>
public class SceneScanLogTests
{
    [SetUp]
    public void SetUp() => SceneScanLog.Forget();

    [TearDown]
    public void TearDown() => SceneScanLog.Forget();

    [Test]
    public void TypeName_ComesFromTheFileName_CutAtTheFirstDot()
    {
        Assert.AreEqual("McpCommandHandler.Handle",
            SceneScanLog.Where("Handle",
                @"F:\repos\kd\Assets\Scripts\Core\MCP\McpCommandHandler.Elements.Query.cs"),
            "у файла с составным именем тип — до ПЕРВОЙ точки");
    }

    [Test]
    public void TypeName_WorksWithForwardSlashes()
    {
        Assert.AreEqual("ContextMenuUI.Build",
            SceneScanLog.Where("Build", "Assets/Scripts/Core/UI/ContextMenuUI.cs"));
    }

    [Test]
    public void UnknownCaller_StillReadsAsSomething()
    {
        Assert.AreEqual("?.?", SceneScanLog.Where(null, null),
            "без имени прибор обязан сказать «не знаю», а не промолчать строкой");
    }

    [Test]
    public void RepeatedCaller_CollapsesIntoATimesCount()
    {
        for (int i = 0; i < 4; i++) SceneScanLog.Note("Refresh", "x/ToolbarUI.cs");
        SceneScanLog.Note("Analyze", "x/SceneAnalyzer.cs");

        Assert.AreEqual("ToolbarUI.Refresh ×4, SceneAnalyzer.Analyze", SceneScanLog.Take());
    }

    [Test]
    public void SingleCaller_CarriesNoTimesCount()
    {
        SceneScanLog.Note("Refresh", "x/ToolbarUI.cs");

        Assert.AreEqual("ToolbarUI.Refresh", SceneScanLog.Take());
    }

    [Test]
    public void Take_ClearsTheFrame_SoTheNextOneInheritsNothing()
    {
        SceneScanLog.Note("Refresh", "x/ToolbarUI.cs");
        SceneScanLog.Take();

        Assert.AreEqual(string.Empty, SceneScanLog.Take(),
            "кадр без обходов обязан быть пустым, иначе прибор врёт на каждом следующем кадре");
    }

    [Test]
    public void TimesNoted_CountsEveryScan_SoAFrameCannotLoseNamesSilently()
    {
        for (int i = 0; i < 5; i++) SceneScanLog.Note("All", "x/SceneElements.cs");
        SceneScanLog.Note("Analyze", "x/SceneAnalyzer.cs");

        Assert.AreEqual(6, SceneScanLog.TimesNoted,
            "счёт имён — второй свидетель к счёту обходов; кадр 2789 потерял 548 имён "
            + "именно потому, что сравнивать было не с чем");
    }

    [Test]
    public void TimesNoted_ResetsWithTheFrame()
    {
        SceneScanLog.Note("Refresh", "x/ToolbarUI.cs");
        SceneScanLog.Take();

        Assert.AreEqual(0, SceneScanLog.TimesNoted,
            "не сброшенный счётчик перенёс бы чужие обходы в следующий кадр");
    }

    [Test]
    public void TimesNoted_ResetsEvenWhenTheFrameHadNothingToShow()
    {
        SceneScanLog.Note("Refresh", "x/ToolbarUI.cs");
        SceneScanLog.Forget();
        SceneScanLog.Note("All", "x/SceneElements.cs");
        SceneScanLog.Take();

        Assert.AreEqual(0, SceneScanLog.TimesNoted);
    }

    [Test]
    public void MoreDistinctCallersThanTheLimit_AreCountedNotDropped()
    {
        for (int i = 0; i < SceneScanLog.MostCallersRemembered + 3; i++)
            SceneScanLog.Note("Do", $"x/Caller{i}.cs");

        string line = SceneScanLog.Take();

        StringAssert.Contains("и ещё 3 сверх предела", line,
            "потерять хвост молча — это снова кадр без виновника");
        StringAssert.Contains("Caller0.Do", line);
    }
}
