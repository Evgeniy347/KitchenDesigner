using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

/// <summary>Сторож дефекта «FacadeElement.StepDoor съедает 95% кадра, даже когда ни одна
/// дверь не открывается»: 63,74мс из 66,7мс кадра и 221,6КБ мусора на ровном месте
/// (F9-профиль пользователя, ни одна деталь на сцене не анимировалась). Причина — дверь,
/// упёршаяся в препятствие, никогда не считает `_doorProgress == target` и потому каждый
/// кадр заново гоняет полный `OpeningCollision.FindMaxProgress` (обход PartRegistry,
/// `GetVertices()` на каждый элемент — свежий массив на каждый вызов) НАВСЕГДА, даже когда
/// в сцене ничего не сдвинулось. Считать миллисекунды в этом сьюте бессмысленно — они
/// плавают от машины к машине, — поэтому сторож считает СОБЫТИЯ:
/// <see cref="FacadeElement.TakeActiveStepDoorCalls"/> — сколько раз StepDoor реально
/// что-то посчитал (а не взял путь нулевой стоимости) с прошлого опроса.</summary>
public class FacadeStepDoorPerfGuardTests : ElementTestBase
{
    [SetUp]
    public void SetUp()
    {
        PartRegistry.Clear();
        FacadeElement.TakeActiveStepDoorCalls();
    }

    [TearDown]
    public void TearDown()
    {
        foreach (var go in _spawned)
            if (go != null) Object.DestroyImmediate(go);
        _spawned.Clear();
        foreach (var el in PartRegistry.GetAll())
            if (el != null) Object.DestroyImmediate(el.gameObject);
        PartRegistry.Clear();
        FacadeElement.TakeActiveStepDoorCalls();
    }

    /// <summary>Отрицательный контроль: фасад, который никуда не едет, обязан стоить
    /// ноль. До фикса тот же вызов увеличивал счётчик на каждом кадре покоя — StepDoor
    /// пересчитывал `Mathf.Approximately` и ставил transform заново независимо от того,
    /// изменилось ли что-то.</summary>
    [Test]
    public void ClosedFacade_RepeatedStepDoor_NeverCountsAsActive()
    {
        var facade = MakePrimitiveFacade("F", new Vector3Int(400, 700, 18), Vector3.zero);

        for (int i = 0; i < 5; i++) facade.StepDoor(0.016f);

        Assert.AreEqual(0, FacadeElement.TakeActiveStepDoorCalls(),
            "закрытая, никогда не открывавшаяся дверца не должна была сделать ни одного " +
            "реального шага анимации");
        Assert.IsFalse(facade.enabled,
            "устоявшийся фасад обязан выключить свой Update — иначе он тикает вечно бесплатно " +
            "только по названию, а на самом деле каждый вызов стоит диспетчеризации компонента");
    }

    /// <summary>Положительный контроль к тесту выше: без него «ноль» было бы зелёным просто
    /// потому, что счётчик не работает вообще. Открывающаяся дверца ОБЯЗАНА засчитаться.</summary>
    [Test]
    public void OpeningFacade_StepDoor_CountsAsActive()
    {
        var facade = MakePrimitiveFacade("F", new Vector3Int(400, 700, 18), Vector3.zero);

        facade.SetOpen(true);
        facade.StepDoor(0.016f);

        Assert.Greater(FacadeElement.TakeActiveStepDoorCalls(), 0,
            "дверца двинулась — StepDoor обязан был это заметить и посчитать");
        Assert.IsTrue(facade.enabled, "мид-анимация обязана держать компонент включённым");
    }

    /// <summary>Главный репродукт. Препятствие (тот же приём, что и в
    /// OpeningCollisionTests.FacadeHinge_HitsObstacle_StopsBeforeFullOpen) останавливает
    /// дверцу на середине хода. Она НЕ закрыта (не первая ветка раннего выхода) и её
    /// прогресс НЕ равен цели (1) — то самое состояние, в котором старый код гонял
    /// FindMaxProgress каждый кадр бесконечно.</summary>
    [Test]
    public void FacadeStuckAgainstObstacle_StepDoorAfterSettling_StopsDoingWork()
    {
        var facade = MakePrimitiveFacade("F", new Vector3Int(600, 716, 18),
            new Vector3(0f, 0.358f, 0f));
        MakePrimitiveElement("Obs", new Vector3Int(600, 600, 18), new Vector3(0f, 0.358f, 0.3f));

        facade.SetOpen(true);
        for (int i = 0; i < 20; i++) facade.StepDoor(0.05f);

        float stuckProgress = facade.DoorProgress;
        Assert.Greater(stuckProgress, 0f, "предусловие: дверца приоткрылась");
        Assert.Less(stuckProgress, 1f, "предусловие: препятствие остановило её, не дав открыться");
        Assert.IsFalse(facade.IsDoorClosed, "предусловие: это не первая ветка раннего выхода");

        FacadeElement.TakeActiveStepDoorCalls();

        for (int i = 0; i < 10; i++) facade.StepDoor(0.05f);

        Assert.AreEqual(0, FacadeElement.TakeActiveStepDoorCalls(),
            "дверца уже упёрлась в препятствие и сцена не менялась — до фикса именно тут " +
            "каждый кадр заново гонялся полный OpeningCollision.FindMaxProgress");
        Assert.AreEqual(stuckProgress, facade.DoorProgress, 1e-6f,
            "поведение анимации не изменилось: дверца осталась ровно там же, где упёрлась");
    }

    /// <summary>Пассажир (фасад, приклеенный к ящику/посудомойке) никогда не анимирует
    /// себя сам — его позу ставит хост. Собственный тик не должен стоить ничего.</summary>
    [Test]
    public void PassengerFacade_StepDoor_NeverCountsAsActive()
    {
        var facade = MakePrimitiveFacade("F", new Vector3Int(400, 700, 18), Vector3.zero);
        facade.IsPassenger = true;

        for (int i = 0; i < 5; i++) facade.StepDoor(0.016f);

        Assert.AreEqual(0, FacadeElement.TakeActiveStepDoorCalls(),
            "пассажир не анимирует себя — тик не должен ничего считать");
        Assert.IsFalse(facade.enabled, "пассажир обязан выключить собственный Update");
    }
}
