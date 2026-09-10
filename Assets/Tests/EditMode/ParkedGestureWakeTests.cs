using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

/// <summary>Вторая половина дефекта D7. Дверца (и ящик), упёршаяся в препятствие, теперь
/// выключает свой Update — а выключенный компонент не может сам заметить, что сцену изменили.
/// Будильник один и общий: `SceneChangeTracker.SettleDerivedLinks` перед подъёмом ревизии
/// поднимает всех, кто стоит у своего предела (<see cref="IParksAtAGestureLimit"/>). Он звонит
/// ДО `SceneRevision.Bump`: после бампа признак «упёрт на этой ревизии» уже ложен, и будить
/// стало бы некого — именно этот порядок и проверяют тесты ниже.
///
/// Каждая фикстура сначала ДОСАЖИВАЕТ сцену: последний кадр анимации оставляет `hasChanged`
/// поднятым, и первый же следующий опрос честно поднимает ревизию — этот бамп будит дверцу
/// сам, без всякого препятствия. Без досадки тесты зеленели бы на нём, а не на том, что
/// проверяют.</summary>
public class ParkedGestureWakeTests : ElementTestBase
{
    [SetUp]
    public void SetUp()
    {
        PartRegistry.Clear();
        SceneRevision.Reset();
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
        SceneRevision.Reset();
    }

    private FacadeElement MakeParkedFacade(out KitchenElement obstacle)
    {
        var facade = MakePrimitiveFacade("Дверца", new Vector3Int(600, 716, 18),
            new Vector3(0f, 0.358f, 0f));
        obstacle = MakePrimitiveElement("Препятствие", new Vector3Int(600, 600, 18),
            new Vector3(0f, 0.358f, 0.3f));

        facade.SetOpen(true);
        for (int i = 0; i < 20; i++) facade.StepDoor(0.05f);

        SceneChangeTracker.Poll();
        SceneChangeTracker.Poll();
        facade.StepDoor(0.05f);
        facade.StepDoor(0.05f);
        SceneChangeTracker.Poll();

        Assert.Less(facade.DoorProgress, 1f, "предусловие: препятствие остановило дверцу");
        Assert.IsTrue(facade.IsParkedAtALimit, "предусловие: она стоит ровно у своего предела");
        Assert.IsFalse(facade.enabled, "предусловие: упёршаяся дверца уснула");
        return facade;
    }

    [Test]
    public void ParkedFacade_IsWoken_WhenTheObstacleIsRemoved()
    {
        var facade = MakeParkedFacade(out var obstacle);

        PartRegistry.Unregister(obstacle);
        Object.DestroyImmediate(obstacle.gameObject);
        SceneChangeTracker.Poll();

        Assert.IsTrue(facade.enabled,
            "препятствие убрали — сцена изменилась, и спящая дверца обязана проснуться, "
            + "иначе она навсегда стоит приоткрытой и не доедет до конца");
    }

    [Test]
    public void ParkedFacade_IsWoken_WhenTheObstacleMovesAway()
    {
        var facade = MakeParkedFacade(out var obstacle);

        obstacle.transform.position += Vector3.right * 2f;
        SceneChangeTracker.Poll();

        Assert.IsTrue(facade.enabled,
            "соседа отодвинули: изменение пришло через ЕГО hasChanged, а не через реестр — "
            + "будильник обязан сработать и в этом случае");
    }

    /// <summary>Отрицательный контроль: покой не имеет права никого будить. Без него зелёный
    /// в двух тестах выше значил бы «включено всегда», а не «включено по делу».</summary>
    [Test]
    public void ParkedFacade_StaysAsleep_WhenNothingInTheSceneChanged()
    {
        var facade = MakeParkedFacade(out _);

        for (int i = 0; i < 5; i++) SceneChangeTracker.Poll();

        Assert.IsFalse(facade.enabled,
            "сцена не менялась — дверца обязана остаться выключенной, иначе будильник звонит "
            + "каждый кадр и весь приём сводится к нулю");
    }

    /// <summary>Ящик держит тот же контракт через тот же интерфейс, поэтому у будильника один
    /// список, а не ветка на каждый тип элемента.</summary>
    [Test]
    public void ParkedDrawer_IsWoken_WhenTheObstacleIsRemoved()
    {
        var go = ElementFactory.CreateDrawer(DrawerType.A, 350, DrawerColor.Anthracite, 400,
            "Ящик", Vector3.zero);
        _spawned.Add(go);
        var drawer = go.GetComponent<DrawerElement>()!;
        var obstacle = MakePrimitiveElement("Препятствие", new Vector3Int(400, 700, 18),
            new Vector3(0f, 0f, 0.2f));

        drawer.SetOpen(true);
        for (int i = 0; i < 30; i++) drawer.StepAnimation(0.05f);
        SceneChangeTracker.Poll();
        SceneChangeTracker.Poll();
        drawer.StepAnimation(0.05f);
        drawer.Update();
        SceneChangeTracker.Poll();

        Assert.Less(drawer.AnimProgress, 1f, "предусловие: препятствие остановило ящик");
        Assert.IsFalse(drawer.enabled, "предусловие: упёршийся ящик уснул");

        PartRegistry.Unregister(obstacle);
        Object.DestroyImmediate(obstacle.gameObject);
        SceneChangeTracker.Poll();

        Assert.IsTrue(drawer.enabled, "препятствие убрали — ящик обязан проснуться и доехать");
    }
}
