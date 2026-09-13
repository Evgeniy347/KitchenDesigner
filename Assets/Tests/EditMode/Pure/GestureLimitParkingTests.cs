using NUnit.Framework;
using KitchenDesigner.Core;

/// <summary>Парковка жеста: «я выключил свой кадр, потому что упёрся». Шесть носителей
/// (дверца, ящик, посудомойка, духовка, стиральная машина, оконный проём) держали эту
/// голову жеста копией, и копия уже разъезжалась однажды — по памяти скана. Здесь она
/// одна.
///
/// Главное различие, ради которого парковка — ОТДЕЛЬНЫЙ тип, а не часть
/// <c>OpeningScanMemo</c>: флаг обязан пережить смену ревизии сцены. Признак «упёрт»
/// когда-то вычислялся как «кэш предела свеж на ТЕКУЩЕЙ ревизии», и
/// <c>PartRegistry.Unregister</c> бампал ревизию сам, до опроса — к обходу будильника
/// признак был уже ложен, будить было некого, и дверца оставалась приоткрытой навсегда.
/// Сторожит это <c>ParkedGestureWakeTests.ParkedFacade_StaysMarkedParked_AcrossARevisionBump</c>.
///
/// Отсюда и форма: парковка ничего не знает про сцену и ревизию, ей отдают два уже
/// посчитанных ответа. Порядок между ними — правило, а не деталь: доехавший жест НЕ
/// припаркован, даже если на его месте стоит препятствие, иначе будильник поднимал бы
/// его каждый раз, когда сцену трогают.</summary>
public class GestureLimitParkingTests
{
    [Test]
    public void AFreshGesture_RunsAndIsNotParked()
    {
        var parking = default(GestureLimitParking);

        Assert.IsTrue(parking.RunsThisFrame(reachedTarget: false, stillBlocked: false),
            "путь свободен и цель не достигнута — кадр обязан отработать");
        Assert.IsFalse(parking.IsParked);
    }

    [Test]
    public void AGestureThatReachedItsTarget_DoesNotRun_AndIsNotParked()
    {
        var parking = default(GestureLimitParking);

        Assert.IsFalse(parking.RunsThisFrame(reachedTarget: true, stillBlocked: false),
            "дверца доехала — двигать больше нечего");
        Assert.IsFalse(parking.IsParked,
            "и она НЕ припаркована: иначе будильник дёргал бы её на каждое изменение сцены "
            + "до конца сеанса");
    }

    [Test]
    public void AGestureStoppedByAnObstacle_DoesNotRun_AndIsParked()
    {
        var parking = default(GestureLimitParking);

        Assert.IsFalse(parking.RunsThisFrame(reachedTarget: false, stillBlocked: true),
            "упёрлась — считать и двигать нечего");
        Assert.IsTrue(parking.IsParked,
            "но она обязана быть отмечена: её разбудит только общий будильник");
    }

    /// <summary>Порядок двух признаков — это правило. Доехать И упереться одновременно
    /// можно: дверца, доехавшая до конца, стоит вплотную к тому, во что упиралась бы
    /// дальше. Побеждает «доехала».</summary>
    [Test]
    public void ReachingTheTarget_OutranksBeingBlocked()
    {
        var parking = default(GestureLimitParking);

        Assert.IsFalse(parking.RunsThisFrame(reachedTarget: true, stillBlocked: true));
        Assert.IsFalse(parking.IsParked,
            "доехавший жест не паркуется, даже если предел совпал с его целью");
    }

    /// <summary>ОБРАТНЫЙ ВХОД на уровне типа: препятствие ушло — отметка обязана сняться
    /// сама, тем же кадром, иначе дверца осталась бы «упёршейся» после того, как ехать
    /// уже можно.</summary>
    [Test]
    public void WhenTheObstacleIsGone_TheParkingIsReleasedByTheVeryNextFrame()
    {
        var parking = default(GestureLimitParking);
        parking.RunsThisFrame(reachedTarget: false, stillBlocked: true);
        Assert.IsTrue(parking.IsParked, "предусловие: она была припаркована");

        Assert.IsTrue(parking.RunsThisFrame(reachedTarget: false, stillBlocked: false),
            "путь освободился — кадр обязан отработать");
        Assert.IsFalse(parking.IsParked);
    }

    /// <summary>Явное снятие нужно там, где жест обрывают не пределом, а снаружи:
    /// ForceClose, SetOpen, смена размеров, возврат в пул. Без него деталь уехала бы
    /// в пул с поднятым флагом и следующий её хозяин начал бы жизнь «упёршимся».</summary>
    [Test]
    public void Release_ClearsTheMark_WhenTheGestureIsCutShortFromOutside()
    {
        var parking = default(GestureLimitParking);
        parking.RunsThisFrame(reachedTarget: false, stillBlocked: true);

        parking.Release();

        Assert.IsFalse(parking.IsParked);
    }
}
