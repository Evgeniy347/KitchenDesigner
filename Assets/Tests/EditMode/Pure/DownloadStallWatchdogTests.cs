#nullable disable
using System;
using NUnit.Framework;
using KitchenDesigner.Core.Update;

/// <summary>
/// Детектор простоя загрузки. Он заменил общий таймаут в 300 с, который рубил
/// одинаково и зависшую закачку, и просто медленную: стомегабайтный установщик
/// на слабом канале не успевал, а «живой» сокет без единого байта висел все
/// пять минут. Вопрос здесь ровно один — растут ли байты; время без роста
/// подаётся в тесты числом, поэтому проверка мгновенная и не ждёт секунд.
/// </summary>
public class DownloadStallWatchdogTests
{
    private const float Idle = 30f;

    private static DownloadStallWatchdog Fresh() => new DownloadStallWatchdog(Idle);

    [Test]
    public void BeforeTheFirstObservation_NothingIsStalled()
    {
        Assert.IsFalse(Fresh().IsStalled(10_000f),
            "сторож, который считает простой от нуля времени, обрывает загрузку "
            + "на первом же кадре: приложение работало до неё сколько угодно долго");
    }

    [Test]
    public void GrowingBytes_KeepTheDownloadAlive_HoweverLongItTakes()
    {
        var watchdog = Fresh();
        long bytes = 0;
        for (float now = 0f; now <= 600f; now += 20f)
        {
            watchdog.Observe(bytes += 1024, now);
            Assert.IsFalse(watchdog.IsStalled(now),
                "медленная, но идущая загрузка — не повод её рубить: ровно это "
                + "делал общий таймаут в 300 с на большом файле");
        }
    }

    [Test]
    public void BytesThatStopGrowing_AreCalledStalled_AfterTheIdleWindow()
    {
        var watchdog = Fresh();
        watchdog.Observe(1024, 100f);

        Assert.IsFalse(watchdog.IsStalled(100f + Idle - 0.1f),
            "до конца окна ожидания загрузка ещё жива: чуть более ранний обрыв "
            + "превращает окно в настройку, которая ничего не значит");
        Assert.IsTrue(watchdog.IsStalled(100f + Idle),
            "байты не растут дольше окна — соединение мёртвое, а попытка ещё "
            + "может быть повторена, пока пользователь не ушёл");
    }

    [Test]
    public void RepeatedObservationsOfTheSameByteCount_DoNotResetTheClock()
    {
        var watchdog = Fresh();
        watchdog.Observe(1024, 0f);
        for (float now = 0f; now < Idle; now += 1f) watchdog.Observe(1024, now);

        Assert.IsTrue(watchdog.IsStalled(Idle),
            "сторож опрашивается каждый кадр. Если засекать время от последнего "
            + "ОПРОСА, а не от последнего роста байтов, простой не наступит никогда");
    }

    [Test]
    public void AByteCountThatWentBackwards_IsNotTreatedAsProgress()
    {
        var watchdog = Fresh();
        watchdog.Observe(4096, 0f);
        watchdog.Observe(1024, 10f);

        Assert.IsTrue(watchdog.IsStalled(Idle),
            "счётчик скачанного не может уменьшиться сам по себе; принять "
            + "уменьшение за прогресс — значит подарить зависшей загрузке ещё одно окно");
    }

    [Test]
    public void SecondsWithoutGrowth_AreReportedForTheFailureMessage()
    {
        var watchdog = Fresh();
        watchdog.Observe(1024, 50f);

        Assert.AreEqual(0f, watchdog.SecondsWithoutGrowth(50f), 0.001f,
            "в момент роста простоя нет");
        Assert.AreEqual(12f, watchdog.SecondsWithoutGrowth(62f), 0.001f,
            "причина обрыва уходит в лог числом: «нет данных 30 с» разбирается, "
            + "а «загрузка не удалась» — нет");
    }

    [Test]
    public void AWatchdogWithoutAnIdleWindow_IsRejectedAtConstruction()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new DownloadStallWatchdog(0f),
            "нулевое окно объявляет простоем любой кадр без нового байта, то есть "
            + "почти каждый: загрузка не завершится никогда");
    }

    [Test]
    public void TheDefaultIdleWindow_IsLongEnoughToSurviveANetworkHiccup()
    {
        Assert.GreaterOrEqual(DownloadStallWatchdog.DefaultIdleSeconds, 15f,
            "окно короче нескольких секунд ловит обычную паузу TCP-ретрансмиссии "
            + "и рвёт живую загрузку на плохом Wi-Fi");
        Assert.LessOrEqual(DownloadStallWatchdog.DefaultIdleSeconds, 60f,
            "окно в минуты возвращает нас к тому, от чего уходили: пользователь "
            + "смотрит на замерший прогресс и закрывает приложение сам");
    }
}
