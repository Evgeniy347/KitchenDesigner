using NUnit.Framework;
using KitchenDesigner.Core;

/// <summary>Ловушка, дважды за две сессии уводившая расследование в чужой кадр: половина
/// величин строки CSV приходит от движка с опозданием на кадр, а половина — нет.
/// <c>ProfilerRecorder</c> («Main Thread», «GC Allocated In Frame», draw/batches/setpass) и
/// <c>Time.unscaledDeltaTime</c> на кадре N отдают величины кадра N−1; маркеры и
/// <c>PartRegistry.GetAll</c> считаются в том же кадре, в котором случились. В дампе
/// <c>perf_20260913_080209.csv</c> это видно глазами: работа клика лежит в строке 3001
/// (<c>getall_calls=3</c>, <c>ConstraintValidator.Validate=17,28</c>), а <c>main_ms=206,8</c>
/// и <c>gc=6 468 598</c> — в строке 3002, где ни один маркер не сработал. Читатель без этой
/// сноски делает вывод «кадр 3002 стоил 206 мс и ничего не делал» — и идёт искать не там.
///
/// Лечится выравниванием ПРИ ЗАПИСИ: строка кадра пишется, когда известны ОБЕ половины, то
/// есть на семпл позже. Этот тип и есть та строка: он принимает работу кадра сразу, счётчики
/// движка — следующим семплом, и до тех пор писать отказывается. Он же считает потерянные
/// кадры — работу, поверх которой легла следующая, так и не дождавшись своих счётчиков:
/// без этого счётчика перепутанный порядок вызовов в <c>PerfMonitor</c> был бы не виден
/// ничем.</summary>
public class AlignedFrameRowTests
{
    private static AlignedFrameRow RowOfTwoMarkers() => new AlignedFrameRow(2);

    private static bool Counters(AlignedFrameRow row, float dtMs, float mainMs, float gcBytes) =>
        row.TakeTheCountersTheEngineReportsAFrameLate(dtMs, mainMs, gcBytes, 512f, 900f, 700f, 120f);

    private static void Work(AlignedFrameRow row, int frame, params float[] markersMs) =>
        row.RememberTheWorkOfTheFrameThatJustRan(frame, 3, 3, "PartRegistry.GetAll ×3",
            "оповещений 1", "ContextMenuUI 92.10мс", markersMs);

    [Test]
    public void CountersWithoutWork_WriteNothing()
    {
        var row = RowOfTwoMarkers();

        Assert.IsFalse(Counters(row, 50f, 206.8f, 6468598f),
            "первый семпл замера знает счётчики кадра, которого он не мерил: строка о нём "
            + "была бы строкой с пустыми маркерами и настоящими 206 мс — ровно то враньё, "
            + "которое чинят");
        Assert.IsFalse(row.Complete);
    }

    [Test]
    public void TheWorkAndTheCountersOfOneFrame_MeetInOneRow()
    {
        var row = RowOfTwoMarkers();

        Work(row, 3001, 17.28f, 2.27f);
        Assert.IsFalse(row.Complete, "счётчики движка кадра 3001 придут только следующим семплом");

        Assert.IsTrue(Counters(row, 50f, 206.8f, 6468598f));

        Assert.IsTrue(row.Complete);
        Assert.AreEqual(3001, row.Frame);
        Assert.AreEqual(206.8f, row.MainMs, 1e-3f,
            "206 мс кадра-клика обязаны лежать в одной строке с его же 17,28 мс валидации");
        Assert.AreEqual(17.28f, row.MarkersMs[0], 1e-3f);
        Assert.AreEqual(3, row.GetAllCalls);
    }

    [Test]
    public void TheNextFrame_TakesItsOwnCounters_NotTheOnesOfThePreviousFrame()
    {
        var row = RowOfTwoMarkers();

        Work(row, 3001, 17.28f, 2.27f);
        Counters(row, 50f, 206.8f, 6468598f);

        Work(row, 3002, 0f, 0f);
        Assert.IsFalse(row.Complete, "кадр 3002 ещё не дописан — его счётчики впереди");

        Counters(row, 16.6f, 16.4f, 117600f);

        Assert.AreEqual(3002, row.Frame);
        Assert.AreEqual(16.4f, row.MainMs, 1e-3f,
            "иначе дешёвый кадр унаследует 206 мс соседа — тот же сдвиг, только в другую сторону");
        Assert.AreEqual(0f, row.MarkersMs[0], 1e-3f);
    }

    [Test]
    public void TheSecondCountersOfOneFrame_AreRefused_SoARowIsNeverWrittenTwice()
    {
        var row = RowOfTwoMarkers();
        Work(row, 3001, 17.28f, 2.27f);
        Counters(row, 50f, 206.8f, 6468598f);

        Assert.IsFalse(Counters(row, 16.6f, 16.4f, 117600f),
            "второй сброс — это вторая строка про один кадр, причём с чужими счётчиками");
        Assert.AreEqual(206.8f, row.MainMs, 1e-3f, "и первую строку он переписывать не вправе");
    }

    [Test]
    public void WorkOverwrittenBeforeItsCountersArrived_IsCountedAsALostFrame()
    {
        var row = RowOfTwoMarkers();

        Work(row, 3001, 17.28f, 2.27f);
        Assert.AreEqual(0, row.FramesLost);

        Work(row, 3002, 0f, 0f);

        Assert.AreEqual(1, row.FramesLost,
            "кадр 3001 ушёл, не попав в файл. Порядок «сначала дописать прошлый, потом "
            + "запомнить нынешний» — несущая часть правки, и без этого счётчика её нарушение "
            + "выглядело бы просто как файл покороче");
    }

    [Test]
    public void TheMarkersAreCopied_SoTheCallerMayReuseItsBuffer()
    {
        var row = RowOfTwoMarkers();
        var buffer = new[] { 17.28f, 2.27f };

        Work(row, 3001, buffer);
        buffer[0] = 0f;

        Assert.AreEqual(17.28f, row.MarkersMs[0], 1e-3f,
            "PerfMonitor переиспользует один буфер каждый кадр: ссылка на него превратила бы "
            + "строку кадра N в строку кадра N+1 молча");
    }

    [Test]
    public void Forget_DropsTheWorkHalf_SoAGapInMeasuringCannotPairTwoDifferentFrames()
    {
        var row = RowOfTwoMarkers();
        Work(row, 3001, 17.28f, 2.27f);

        row.Forget();

        Assert.IsFalse(Counters(row, 50f, 206.8f, 6468598f),
            "замер выключили и включили: счётчики движка теперь про другой кадр, и склеивать "
            + "их с работой, снятой до паузы, нельзя");
        Assert.AreEqual(0, row.Frame);
    }
}
