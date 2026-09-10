using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

/// <summary>Сторож дефекта T4: девять компонентов с Update() держали Unity-диспетчеризацию
/// живой каждый кадр даже в покое, хотя работа внутри — ранний выход. Тот же класс дефекта,
/// что чинил <see cref="FacadeElement.StepDoor"/> (коммит 60e1f9e2, тест
/// FacadeStepDoorPerfGuardTests): устоявшийся экземпляр обязан выключить свой Update
/// (<c>enabled = false</c>) и проснуться при ПЕРВОМ же изменении своего входа — открытии,
/// сдвиге собственного transform, движении хозяйского элемента/стены. Считаем не события
/// (у Facade был <c>TakeActiveStepDoorCalls</c>), а сам флаг <c>enabled</c> — конечный эффект
/// один и тот же: выключенный компонент не платит за диспетчеризацию Unity → C#.</summary>
public class IdleUpdateSleepPerfGuardTests : ElementTestBase
{
    private const int TopThicknessMM = 38;
    private const float ToU = AppConstants.MM_TO_UNITS;

    [SetUp]
    public void SetUp()
    {
        PartRegistry.Clear();
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
    }

    private T Spawn<T>(System.Func<GameObject> factory) where T : KitchenElement
    {
        var go = factory();
        _spawned.Add(go);
        return go.GetComponent<T>();
    }

    // ───────────────────────── OvenElement ─────────────────────────

    [Test]
    public void OvenElement_ClosedDoor_SettlesAndDisablesUpdate()
    {
        var oven = Spawn<OvenElement>(() => ElementFactory.CreateOven("Духовка", Vector3.zero));

        oven.Update();

        Assert.IsFalse(oven.enabled,
            "закрытая, никогда не открывавшаяся духовка обязана выключить свой Update");
    }

    [Test]
    public void OvenElement_SetOpen_WakesUpdate()
    {
        var oven = Spawn<OvenElement>(() => ElementFactory.CreateOven("Духовка", Vector3.zero));
        oven.Update();
        Assert.IsFalse(oven.enabled, "предусловие: духовка уснула");

        oven.SetOpen(true);

        Assert.IsTrue(oven.enabled, "открытие обязано разбудить Update");
        oven.Update();
        Assert.Greater(oven.DoorProgress, 0f, "дверца обязана была сдвинуться с места");
    }

    // ───────────────────────── DishwasherElement ─────────────────────────

    [Test]
    public void DishwasherElement_ClosedDoor_SettlesAndDisablesUpdate()
    {
        var dw = Spawn<DishwasherElement>(() =>
            ElementFactory.CreateDishwasher("Посудомойка", Vector3.zero));

        dw.Update();

        Assert.IsFalse(dw.enabled,
            "закрытая, никогда не открывавшаяся посудомойка обязана выключить свой Update");
    }

    [Test]
    public void DishwasherElement_SetOpen_WakesUpdate()
    {
        var dw = Spawn<DishwasherElement>(() =>
            ElementFactory.CreateDishwasher("Посудомойка", Vector3.zero));
        dw.Update();
        Assert.IsFalse(dw.enabled, "предусловие: посудомойка уснула");

        dw.SetOpen(true);

        Assert.IsTrue(dw.enabled, "открытие обязано разбудить Update");
    }

    // ───────────────────────── DrawerElement ─────────────────────────

    private DrawerElement SpawnDrawer() => Spawn<DrawerElement>(() =>
        ElementFactory.CreateDrawer(DrawerType.A, 350, DrawerColor.Anthracite, 400,
            "Ящик", Vector3.zero));

    [Test]
    public void DrawerElement_Closed_SettlesAndDisablesUpdate()
    {
        var drawer = SpawnDrawer();

        drawer.Update();

        Assert.IsFalse(drawer.enabled, "закрытый ящик обязан выключить свой Update");
    }

    [Test]
    public void DrawerElement_SetOpen_WakesUpdate()
    {
        var drawer = SpawnDrawer();
        drawer.Update();
        Assert.IsFalse(drawer.enabled, "предусловие: ящик уснул");

        drawer.SetOpen(true);

        Assert.IsTrue(drawer.enabled, "открытие обязано разбудить Update");
    }

    /// <summary>Верхний ящик пары синхронизирует высоту с нижним в LateUpdate
    /// (SyncToLower), а LateUpdate гасится тем же enabled, что и Update. Выключить его
    /// в покое — значит заморозить пару, если нижний ящик потом поменяет тип/длину.</summary>
    [Test]
    public void DrawerElement_UpperDrawer_NeverDisablesUpdate_SoPairSyncStaysAlive()
    {
        var drawer = SpawnDrawer();
        drawer.IsUpperDrawer = true;

        drawer.Update();

        Assert.IsTrue(drawer.enabled,
            "верхний ящик обязан оставаться включённым — иначе SyncToLower в LateUpdate " +
            "перестанет вызываться вместе с Update");
    }

    // ───────────────────────── WallOpeningElement (дверь) ─────────────────────────

    private DoorElement SpawnDoor() => Spawn<DoorElement>(() =>
        ElementFactory.CreateDoor(new Vector3Int(900, 2000, 100), "Дверь", Vector3.zero));

    [Test]
    public void DoorElement_Closed_SettlesAndDisablesUpdate()
    {
        var door = SpawnDoor();

        door.Update();

        Assert.IsFalse(door.enabled, "закрытая дверь обязана выключить свой Update");
    }

    [Test]
    public void DoorElement_SetOpen_WakesUpdate()
    {
        var door = SpawnDoor();
        door.Update();
        Assert.IsFalse(door.enabled, "предусловие: дверь уснула");

        door.SetOpen(true);

        Assert.IsTrue(door.enabled, "открытие обязано разбудить Update");
    }

    [Test]
    public void DoorElement_OwnTransformMoved_WakesUpdate()
    {
        var door = SpawnDoor();
        door.Update();
        Assert.IsFalse(door.enabled, "предусловие: дверь уснула");

        door.transform.position += new Vector3(0.1f, 0f, 0f);
        SceneChangeTracker.Poll();

        Assert.IsTrue(door.enabled,
            "сдвиг собственного transform обязан разбудить Update — иначе дверь никогда " +
            "не переснимется к ближайшей стене (SnapToWall)");
    }

    // ───────────────────────── CooktopElement ─────────────────────────

    private KitchenElement MakeCountertop(int widthMM, int depthMM)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        _spawned.Add(go);
        var el = go.AddComponent<KitchenElement>();
        el.PartName = "Столешница";
        el.transform.rotation = Quaternion.Euler(-90f, 0f, 0f);
        el.DimensionsMM = new Vector3Int(widthMM, depthMM, TopThicknessMM);
        PartRegistry.Register(el);
        return el;
    }

    [Test]
    public void CooktopElement_Steady_SettlesAndDisablesUpdate()
    {
        var cooktop = Spawn<CooktopElement>(() => ElementFactory.CreateCooktop("Плита", Vector3.zero));

        cooktop.Update();

        Assert.IsFalse(cooktop.enabled, "варочная без изменений обязана выключить свой Update");
    }

    [Test]
    public void CooktopElement_HostMoved_WakesUpdate()
    {
        var top = MakeCountertop(1200, 650);
        float topSurfaceY = top.transform.position.y + TopThicknessMM * 0.5f * ToU;
        var cooktop = Spawn<CooktopElement>(() =>
        {
            var go = new GameObject("Плита");
            go.transform.position = new Vector3(0f, topSurfaceY + 0.05f, 0f);
            var c = go.AddComponent<CooktopElement>();
            c.PartName = "Плита";
            c.DimensionsMM = new Vector3Int(
                CooktopElement.DEFAULT_WIDTH_MM, CooktopElement.DEFAULT_HEIGHT_MM,
                CooktopElement.DEFAULT_DEPTH_MM);
            PartRegistry.Register(c);
            return go;
        });
        cooktop.SnapToPart();
        Assert.IsTrue(cooktop.IsAttached, "предусловие: варочная села на столешницу");
        cooktop.Update();
        Assert.IsFalse(cooktop.enabled, "предусловие: варочная уснула после посадки");

        top.transform.position += new Vector3(0.3f, 0f, 0f);
        SceneChangeTracker.Poll();

        Assert.IsTrue(cooktop.enabled,
            "движение хозяйской столешницы обязано разбудить варочную — иначе Mount.PartMoved " +
            "никогда не будет перепроверен");
    }

    // ───────────────────────── SinkElement ─────────────────────────

    [Test]
    public void SinkElement_Steady_SettlesAndDisablesUpdate()
    {
        var sink = Spawn<SinkElement>(() => ElementFactory.CreateSink("Мойка", Vector3.zero));

        sink.Update();

        Assert.IsFalse(sink.enabled, "мойка без изменений обязана выключить свой Update");
    }

    [Test]
    public void SinkElement_OwnTransformMoved_WakesUpdate()
    {
        var sink = Spawn<SinkElement>(() => ElementFactory.CreateSink("Мойка", Vector3.zero));
        sink.Update();
        Assert.IsFalse(sink.enabled, "предусловие: мойка уснула");

        sink.transform.position += new Vector3(0.1f, 0f, 0f);
        SceneChangeTracker.Poll();

        Assert.IsTrue(sink.enabled,
            "сдвиг собственного transform обязан разбудить Update — иначе SnapToPart " +
            "никогда не перепроверит посадку");
    }

    // ───────────────────────── SocketElement ─────────────────────────

    [Test]
    public void SocketElement_Steady_SettlesAndDisablesUpdate()
    {
        var socket = Spawn<SocketElement>(() =>
            ElementFactory.CreateSocket(WallDeviceSpec.Default, "Розетка", Vector3.zero));

        socket.Update();

        Assert.IsFalse(socket.enabled, "розетка без изменений обязана выключить свой Update");
    }

    [Test]
    public void SocketElement_OwnTransformMoved_WakesUpdate()
    {
        var socket = Spawn<SocketElement>(() =>
            ElementFactory.CreateSocket(WallDeviceSpec.Default, "Розетка", Vector3.zero));
        socket.Update();
        Assert.IsFalse(socket.enabled, "предусловие: розетка уснула");

        socket.transform.position += new Vector3(0.1f, 0f, 0f);
        SceneChangeTracker.Poll();

        Assert.IsTrue(socket.enabled,
            "сдвиг собственного transform обязан разбудить Update — иначе розетка никогда " +
            "не переснимется к стене");
    }

    // ───────────────────────── LightSwitchElement ─────────────────────────

    [Test]
    public void LightSwitchElement_Steady_SettlesAndDisablesUpdate()
    {
        var sw = Spawn<LightSwitchElement>(() =>
            ElementFactory.CreateLightSwitch(WallDeviceSpec.Default, true, null,
                "Выключатель", Vector3.zero));

        sw.Update();

        Assert.IsFalse(sw.enabled, "выключатель без изменений обязан выключить свой Update");
    }

    /// <summary>Не только пробуждение, но и то, что оно НЕ тянет за собой полный
    /// LightSwitchNetwork.Refresh() — OnEnable гоняет его по всем деталям сцены, и если бы
    /// наше собственное усыпление/пробуждение проходило через тот же OnEnable без защиты,
    /// каждое пробуждение по несвязанной причине (сдвинули сам выключатель) стоило бы дороже,
    /// чем сэкономленная диспетчеризация.</summary>
    [Test]
    public void LightSwitchElement_OwnTransformMoved_WakesUpdate_WithoutSpuriousNetworkRefresh()
    {
        var sw = Spawn<LightSwitchElement>(() =>
            ElementFactory.CreateLightSwitch(WallDeviceSpec.Default, true, null,
                "Выключатель", Vector3.zero));
        sw.Update();
        Assert.IsFalse(sw.enabled, "предусловие: выключатель уснул");

        sw.transform.position += new Vector3(0.1f, 0f, 0f);
        SceneChangeTracker.Poll();

        Assert.IsTrue(sw.enabled,
            "сдвиг собственного transform обязан разбудить Update — иначе выключатель " +
            "никогда не переснимется к стене");
    }

    // ───────────────────────── WallHungToiletElement ─────────────────────────

    [Test]
    public void WallHungToiletElement_Steady_SettlesAndDisablesUpdate()
    {
        var toilet = Spawn<WallHungToiletElement>(() =>
            ElementFactory.CreateWallHungToilet(
                WallHungToiletElement.DefaultSeatHeightMM,
                WallHungToiletElement.DefaultFlushPlateHeightMM,
                "Унитаз", Vector3.zero));

        toilet.Update();

        Assert.IsFalse(toilet.enabled, "унитаз без изменений обязан выключить свой Update");
    }

    [Test]
    public void WallHungToiletElement_OwnTransformMoved_WakesUpdate()
    {
        var toilet = Spawn<WallHungToiletElement>(() =>
            ElementFactory.CreateWallHungToilet(
                WallHungToiletElement.DefaultSeatHeightMM,
                WallHungToiletElement.DefaultFlushPlateHeightMM,
                "Унитаз", Vector3.zero));
        toilet.Update();
        Assert.IsFalse(toilet.enabled, "предусловие: унитаз уснул");

        toilet.transform.position += new Vector3(0.1f, 0f, 0f);
        SceneChangeTracker.Poll();

        Assert.IsTrue(toilet.enabled,
            "сдвиг собственного transform обязан разбудить Update — иначе унитаз никогда " +
            "не переснимется к стене");
    }

    // ───────────────────────── FacadeElement, упёршийся в препятствие ─────────────────────────

    /// <summary>Случай, ради которого весь приём и делался, до сих пор не был закрыт: в
    /// StepDoor ранний выход «всё ещё упёрт в тот же предел» стоял РАНЬШЕ `enabled = false`,
    /// и условие истинно ровно для дверцы, стоящей у своего предела. Такая дверца оставалась
    /// в списке вызовов Unity навсегда, а в этом сьюте не было ни одного случая с фасадом.</summary>
    private FacadeElement MakeBlockedFacade()
    {
        var facade = MakePrimitiveFacade("Дверца", new Vector3Int(600, 716, 18),
            new Vector3(0f, 0.358f, 0f));
        MakePrimitiveElement("Препятствие", new Vector3Int(600, 600, 18),
            new Vector3(0f, 0.358f, 0.3f));

        facade.SetOpen(true);
        for (int i = 0; i < 20; i++) facade.StepDoor(0.05f);
        return facade;
    }

    [Test]
    public void FacadeElement_StuckAgainstObstacle_DisablesUpdate()
    {
        var facade = MakeBlockedFacade();

        Assert.Greater(facade.DoorProgress, 0f, "предусловие: дверца приоткрылась");
        Assert.Less(facade.DoorProgress, 1f, "предусловие: препятствие её остановило");
        Assert.IsFalse(facade.enabled,
            "дверца упёрлась и больше не сдвинется без изменения сцены — обязана выключить " +
            "свой Update, иначе единственный случай, ради которого всё делалось, тикает вечно");
    }

    [Test]
    public void FacadeElement_ModeChanged_WakesTheParkedDoor()
    {
        var facade = MakeBlockedFacade();
        Assert.IsFalse(facade.enabled, "предусловие: упёршаяся дверца уснула");

        facade.Mode = DoorMode.HingeFrontRight;

        Assert.IsTrue(facade.enabled,
            "смена режима навески сбрасывает кэш предела — сеттер обязан и разбудить Update, " +
            "иначе сброшенный кэш никто не пересчитает");
    }

    [Test]
    public void FacadeElement_ClosedPoseShifted_WakesTheParkedDoor()
    {
        var facade = MakeBlockedFacade();
        Assert.IsFalse(facade.enabled, "предусловие: упёршаяся дверца уснула");

        facade.ShiftClosedPose(new Vector3(0.5f, 0f, 0f));

        Assert.IsTrue(facade.enabled,
            "хозяин сдвинул закрытую позу — условия жеста изменились, кэш сброшен, " +
            "и компонент обязан проснуться");
    }

    // ───────────────────────── DrawerElement, упёршийся в препятствие ─────────────────────────

    [Test]
    public void DrawerElement_StuckAgainstObstacle_DisablesUpdate()
    {
        var drawer = SpawnDrawer();
        MakePrimitiveElement("Препятствие", new Vector3Int(400, 700, 18),
            new Vector3(0f, 0f, 0.2f));

        drawer.SetOpen(true);
        for (int i = 0; i < 30; i++) drawer.StepAnimation(0.05f);
        drawer.Update();

        Assert.Greater(drawer.AnimProgress, 0f, "предусловие: ящик выехал");
        Assert.Less(drawer.AnimProgress, 1f, "предусловие: препятствие его остановило");
        Assert.IsFalse(drawer.enabled,
            "упёршийся ящик болел тем же: цель никогда не достигнута, поэтому старое условие " +
            "выключения не срабатывало и Update тикал вечно");
    }
}
