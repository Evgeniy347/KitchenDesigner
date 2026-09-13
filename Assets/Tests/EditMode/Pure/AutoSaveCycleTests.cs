using System.Collections.Generic;
using NUnit.Framework;
using KitchenDesigner.Core;

/// <summary>Цикл автосохранения, найденный в дампе пользователя: кадры по 110–114 мс
/// и 16,5 МБ мусора ЗА ОДИН КАДР. Прибор назвал виновника — за один тик цикл строил
/// снимок проекта в 411 деталей ДВАЖДЫ: сперва <c>CaptureCurrentJson</c> ради сравнения
/// «изменилось ли», затем путь записи захватывал сцену заново и сериализовал её второй
/// раз. Два обхода сцены, две <c>ProjectData</c>, две сериализации — ради одного файла.
///
/// Здесь считается РАБОТА, а не миллисекунды: сколько раз цикл спросил снимок и сколько
/// раз отдал его на запись. Обе стороны — подставные, поэтому тест живёт в быстром
/// наборе без Unity, без сцены и без диска.
///
/// Главная проверка — ОБРАТНЫЙ ВХОД: на запись обязана уйти РОВНО та строка, которую
/// сравнивали. Пока это один и тот же снимок, правка не может быть замечена сравнением
/// и потеряна записью; второй захват такую щель открывал.</summary>
public class AutoSaveCycleTests
{
    private sealed class Spy
    {
        public readonly List<string> Captured = new List<string>();
        public readonly List<string> Written = new List<string>();
        public bool WriteSucceeds = true;
        private readonly Queue<string> _snapshots;

        public Spy(params string[] snapshots) => _snapshots = new Queue<string>(snapshots);

        public string Capture()
        {
            string next = _snapshots.Count > 1 ? _snapshots.Dequeue() : _snapshots.Peek();
            Captured.Add(next);
            return next;
        }

        public bool Write(string json)
        {
            Written.Add(json);
            return WriteSucceeds;
        }
    }

    [Test]
    public void AChangedScene_IsCapturedOnce_AndWrittenOnce()
    {
        var spy = new Spy("scene-after-the-edit");

        var outcome = AutoSaveCycle.Run(spy.Capture, spy.Write, "scene-before-the-edit");

        Assert.AreEqual(1, spy.Captured.Count,
            "снимок строится РОВНО один раз за тик; раньше их было два — "
            + "один на сравнение, второй внутри записи");
        Assert.AreEqual(1, spy.Written.Count, "и записывается ровно один раз");
        Assert.IsTrue(outcome.Wrote);
    }

    [Test]
    public void TheSnapshotThatWasCompared_IsTheSnapshotThatGetsWritten()
    {
        var spy = new Spy("Board renamed to Shelf");

        AutoSaveCycle.Run(spy.Capture, spy.Write, "Board");

        Assert.AreEqual(spy.Captured[0], spy.Written[0],
            "обратный вход: на диск уходит ТА ЖЕ строка, которую признали изменившейся. "
            + "Пока захват один, между сравнением и записью нет щели, в которую могла бы "
            + "провалиться правка пользователя");
    }

    [Test]
    public void AnUnchangedScene_IsCapturedOnce_AndNotWrittenAtAll()
    {
        var spy = new Spy("nothing moved");

        var outcome = AutoSaveCycle.Run(spy.Capture, spy.Write, "nothing moved");

        Assert.AreEqual(1, spy.Captured.Count,
            "сравнить всё равно нужно, и сравнивается САМ снимок — не ревизия сцены: "
            + "переименование детали ревизию не поднимает, и калитка по ревизии "
            + "пропустила бы правку. Потерянная работа пользователя дороже снимка");
        Assert.IsEmpty(spy.Written, "неизменная сцена не пишется на диск вовсе");
        Assert.IsFalse(outcome.Wrote);
    }

    [Test]
    public void AnUnchangedScene_KeepsTheLastSavedSnapshot()
    {
        var spy = new Spy("same");

        var outcome = AutoSaveCycle.Run(spy.Capture, spy.Write, "same");

        Assert.AreEqual("same", outcome.LastSavedJson);
    }

    [Test]
    public void AfterAWrite_TheNewSnapshotBecomesTheBaseline()
    {
        var spy = new Spy("second version");

        var outcome = AutoSaveCycle.Run(spy.Capture, spy.Write, "first version");

        Assert.AreEqual("second version", outcome.LastSavedJson,
            "иначе следующий тик снова сочтёт сцену изменившейся и запишет её ещё раз");
    }

    [Test]
    public void AFailedWrite_DoesNotMoveTheBaseline()
    {
        var spy = new Spy("edited") { WriteSucceeds = false };

        var outcome = AutoSaveCycle.Run(spy.Capture, spy.Write, "original");

        Assert.IsFalse(outcome.Wrote);
        Assert.AreEqual("original", outcome.LastSavedJson,
            "диск отказал — правка НЕ сохранена; сдвинуть отметку значило бы больше "
            + "никогда её не записать, и пользователь потерял бы работу молча");
    }

    [Test]
    public void TwoTicksOverOneEdit_WriteOnce_NotTwice()
    {
        var spy = new Spy("edited");

        var first = AutoSaveCycle.Run(spy.Capture, spy.Write, "original");
        var second = AutoSaveCycle.Run(spy.Capture, spy.Write, first.LastSavedJson);

        Assert.AreEqual(1, spy.Written.Count,
            "правка одна — запись одна; второй тик обязан увидеть, что с прошлого раза "
            + "ничего не поменялось");
        Assert.AreEqual(2, spy.Captured.Count, "по одному снимку на тик, не больше");
        Assert.IsFalse(second.Wrote);
    }
}
