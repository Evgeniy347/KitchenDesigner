using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

public class OpeningCollisionTests
{
    private readonly List<GameObject> _spawned = new List<GameObject>();

    [SetUp]
    public void Setup()
    {
        PartRegistry.Clear();
    }

    [TearDown]
    public void Teardown()
    {
        foreach (var go in _spawned)
            if (go != null) Object.DestroyImmediate(go);
        _spawned.Clear();
        PartRegistry.Clear();
    }

    private GameObject Spawn(GameObject go)
    {
        _spawned.Add(go);
        return go;
    }

    private KitchenElement MakePart(string name, Vector3 pos, int w = 600, int h = 600, int d = 18)
    {
        var go = Spawn(ElementFactory.CreatePart(new Vector3Int(w, h, d), name, pos));
        return go.GetComponent<KitchenElement>();
    }

    private FacadeElement MakeFacade(string name, Vector3 pos, int w = 600, int h = 716, int d = 18)
    {
        var go = Spawn(ElementFactory.CreateFacade(new Vector3Int(w, h, d), name, pos, 2, 2, 2, 2));
        return go.GetComponent<FacadeElement>();
    }

    private DrawerElement MakeDrawer(string name, Vector3 pos, int width = 400)
    {
        var go = Spawn(ElementFactory.CreateDrawer(DrawerType.A, 350, DrawerColor.Anthracite, width, name, pos));
        return go.GetComponent<DrawerElement>();
    }

    private WindowElement MakeWindow(string name, Vector3 pos, int w = 600, int h = 800, int d = 120)
    {
        var go = Spawn(ElementFactory.CreateWindow(new Vector3Int(w, h, d), name, pos));
        return go.GetComponent<WindowElement>();
    }

    private DoorElement MakeDoor(string name, Vector3 pos, int w = 600, int h = 800, int d = 120)
    {
        var go = Spawn(ElementFactory.CreateDoor(new Vector3Int(w, h, d), name, pos));
        return go.GetComponent<DoorElement>();
    }

    // ── Фасад (hinge) ────────────────────────────────────────────────────

    [Test]
    public void FacadeHinge_HitsObstacle_StopsBeforeFullOpen()
    {
        var facade = MakeFacade("F", new Vector3(0f, 0.358f, 0f));
        // Препятствие 600×600 перед фасадом на расстоянии 30 см
        var obstacle = MakePart("Obs", new Vector3(0f, 0.358f, 0.3f), 600, 600, 18);

        facade.SetOpen(true);
        facade.StepDoor(1f);

        Assert.Less(facade.DoorProgress, 1f, "фасад не открылся полностью — упёрся в препятствие");
        Assert.Greater(facade.DoorProgress, 0f, "фасад всё же приоткрылся");
        AssertNoOverlap(facade, obstacle, "открытый фасад не пересекается с препятствием");
    }

    [Test]
    public void FacadeHinge_NoObstacle_OpensFully()
    {
        var facade = MakeFacade("F", new Vector3(0f, 0.358f, 0f));

        facade.SetOpen(true);
        facade.StepDoor(1f);

        Assert.AreEqual(1f, facade.DoorProgress, 1e-4f, "без препятствия открывается полностью");
    }

    [Test]
    public void FacadeHinge_ObstacleFarAway_OpensFully()
    {
        var facade = MakeFacade("F", new Vector3(0f, 0.358f, 0f));
        var obstacle = MakePart("Obs", new Vector3(0f, 0.358f, 2f), 600, 600, 18);

        facade.SetOpen(true);
        facade.StepDoor(1f);

        Assert.AreEqual(1f, facade.DoorProgress, 1e-4f, "препятствие далеко — открывается полностью");
    }

    // ── Фасад (drawer) ────────────────────────────────────────────────────

    [Test]
    public void FacadeDrawer_HitsObstacle_StopsBeforeFullOpen()
    {
        var facade = MakeFacade("F", new Vector3(0f, 0.358f, 0f));
        facade.Mode = DoorMode.DrawerOut;
        // Препятствие в 20 см спереди (ход ящика 40 см)
        var obstacle = MakePart("Obs", new Vector3(0f, 0.358f, 0.2f), 600, 600, 18);

        facade.SetOpen(true);
        facade.StepDoor(1f);

        Assert.Less(facade.DoorProgress, 1f, "ящик не выдвинулся полностью — упёрся в препятствие");
        Assert.Greater(facade.DoorProgress, 0f);
        AssertNoOverlap(facade, obstacle, "выдвинутый фасад не пересекается с препятствием");
    }

    [Test]
    public void FacadeDrawer_NoObstacle_OpensFully()
    {
        var facade = MakeFacade("F", new Vector3(0f, 0.358f, 0f));
        facade.Mode = DoorMode.DrawerOut;

        facade.SetOpen(true);
        facade.StepDoor(1f);

        Assert.AreEqual(1f, facade.DoorProgress, 1e-4f);
    }

    // ── Ящик ──────────────────────────────────────────────────────────────

    [Test]
    public void Drawer_HitsObstacle_StopsBeforeFullOpen()
    {
        var drawer = MakeDrawer("D", new Vector3(0f, 0.043f, 0f));
        // Препятствие в 20 см спереди (ход ящика 40 см)
        var obstacle = MakePart("Obs", new Vector3(0f, 0.043f, 0.2f), 400, 86, 18);

        drawer.SetOpen(true);
        drawer.StepAnimation(1f);

        Assert.Less(drawer.AnimProgress, 1f, "ящик не выдвинулся полностью");
        Assert.Greater(drawer.AnimProgress, 0f);
        AssertNoOverlap(drawer, obstacle, "ящик не пересекается с препятствием");
    }

    [Test]
    public void Drawer_NoObstacle_OpensFully()
    {
        var drawer = MakeDrawer("D", new Vector3(0f, 0.043f, 0f));

        drawer.SetOpen(true);
        drawer.StepAnimation(1f);

        Assert.AreEqual(1f, drawer.AnimProgress, 1e-4f);
    }

    // ── Окно ──────────────────────────────────────────────────────────────

    [Test]
    public void Window_HitsObstacle_StopsBeforeFullOpen()
    {
        var window = MakeWindow("W", new Vector3(0f, 0.4f, 0f));
        // Препятствие перед окном в 10 см — створка упрётся
        var obstacle = MakePart("Obs", new Vector3(0f, 0.4f, 0.1f), 600, 600, 18);

        window.SetOpen(true);
        window.StepDoor(1f);

        Assert.Less(window.DoorProgress, 1f, "створка не открылась полностью");
        Assert.Greater(window.DoorProgress, 0f, "створка приоткрылась");
    }

    [Test]
    public void Window_NoObstacle_OpensFully()
    {
        var window = MakeWindow("W", new Vector3(0f, 0.4f, 0f));

        window.SetOpen(true);
        window.StepDoor(1f);

        Assert.AreEqual(1f, window.DoorProgress, 1e-4f);
    }

    // ── Дверь ─────────────────────────────────────────────────────────────

    [Test]
    public void Door_HitsObstacle_StopsBeforeFullOpen()
    {
        var door = MakeDoor("D", new Vector3(0f, 0.4f, 0f));
        var obstacle = MakePart("Obs", new Vector3(0f, 0.4f, 0.15f), 600, 600, 18);

        door.SetOpen(true);
        door.StepDoor(1f);

        Assert.Less(door.DoorProgress, 1f, "дверь не открылась полностью");
        Assert.Greater(door.DoorProgress, 0f);
    }

    [Test]
    public void Door_NoObstacle_OpensFully()
    {
        var door = MakeDoor("D", new Vector3(0f, 0.4f, 0f));

        door.SetOpen(true);
        door.StepDoor(1f);

        Assert.AreEqual(1f, door.DoorProgress, 1e-4f);
    }

    // ── Фасад + ящик скреплены ────────────────────────────────────────────

    [Test]
    public void DrawerWithFacade_FacadeHitsObstacle_BothStop()
    {
        var drawer = MakeDrawer("Drawer", new Vector3(0f, 0.043f, 0f));
        var facade = MakeFacade("Facade", new Vector3(0f, 0.043f, 0.184f), 400, 86, 18);

        drawer.AttachedFacadeName = facade.PartName;

        // Препятствие перед фасадом — так, чтобы фасад упёрся
        var obstacle = MakePart("Obs", new Vector3(0f, 0.043f, 0.35f), 400, 86, 18);

        drawer.SetOpen(true);
        // Анимируем оба — ящик и прикреплённый фасад
        drawer.StepAnimation(1f);
        facade.StepDoor(1f);

        Assert.Less(drawer.AnimProgress, 1f, "ящик остановился");
        Assert.Less(facade.DoorProgress, 1f, "фасад остановился");
        Assert.Greater(facade.DoorProgress, 0f);
    }

    [Test]
    public void DrawerWithFacade_NoObstacle_BothFullyOpen()
    {
        var drawer = MakeDrawer("Drawer", new Vector3(0f, 0.043f, 0f));
        var facade = MakeFacade("Facade", new Vector3(0f, 0.043f, 0.184f), 400, 86, 18);

        drawer.AttachedFacadeName = facade.PartName;

        drawer.SetOpen(true);
        drawer.StepAnimation(1f);
        facade.StepDoor(1f);

        Assert.AreEqual(1f, drawer.AnimProgress, 1e-4f);
        Assert.AreEqual(1f, facade.DoorProgress, 1e-4f, "фасад тоже полностью открылся");
    }

    // ── Фасад не должен блокироваться ящиком, к которому прикреплён ──────

    [Test]
    public void DrawerWithFacade_FacadeOverlapsDrawer_BothFullyOpen()
    {
        // Фасад — фронт ящика, геометрически накладывается на корпус.
        // Без exclude-списка в StepDoor фасад остановится, потому что
        // ящик уже открыт в той же позиции, куда едет фасад.
        var drawer = MakeDrawer("Drawer", new Vector3(0f, 0.043f, 0f));
        // Фасад на 4 мм «внутри» корпуса (задняя грань на z=0.171, фронт ящика на z=0.175)
        var facade = MakeFacade("Facade", new Vector3(0f, 0.043f, 0.18f), 400, 86, 18);

        drawer.AttachedFacadeName = facade.PartName;

        drawer.SetOpen(true);
        drawer.StepAnimation(1f);
        facade.StepDoor(1f);

        Assert.AreEqual(1f, drawer.AnimProgress, 1e-4f, "ящик открыт полностью");
        Assert.AreEqual(1f, facade.DoorProgress, 1e-4f,
            "прикреплённый фасад тоже открыт полностью — ящик не должен его блокировать");
    }

    // ── Закрытие не проверяет коллизии ────────────────────────────────────

    [Test]
    public void Facade_ClosePastObstacle_ClosesFully()
    {
        var facade = MakeFacade("F", new Vector3(0f, 0.358f, 0f));
        var obstacle = MakePart("Obs", new Vector3(0f, 0.358f, 0.01f), 600, 600, 18);

        // Открываем (упрется в близкое препятствие почти сразу)
        facade.SetOpen(true);
        facade.StepDoor(1f);
        float openProgress = facade.DoorProgress;

        // Закрываем — должно закрыться полностью, игнорируя коллизию
        facade.SetOpen(false);
        facade.StepDoor(1f);

        Assert.IsTrue(facade.IsDoorClosed, "закрытие не блокируется коллизией");
        Assert.LessOrEqual(facade.DoorProgress, 0.001f);
    }

    // ── Вспомогательные методы ────────────────────────────────────────────

    private void AssertNoOverlap(KitchenElement a, KitchenElement b, string message)
    {
        Assert.IsFalse(SnapSystem.ElementsIntersect(a, b), message);
    }
}
