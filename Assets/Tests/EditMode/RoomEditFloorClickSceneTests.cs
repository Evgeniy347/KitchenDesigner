using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

/// <summary>Клик по полу в режиме помещения на ЗАМОРОЖЕННОЙ копии проекта пользователя
/// (<c>Fixtures/distance-guides-scene.save.json</c> — его <c>docs/example.save.json</c> от 2026-10-04
/// с полом Pol_1 и подложкой 3170×7240; живой файл не трогается).
///
/// Путь клика тот же, что в приложении: режим включает <c>EditModeManager.SetMode(EditMode.Room)</c>
/// (его зовёт кнопка тулбара), видимость применяет <c>SceneVisibilityManager.Apply</c>, луч идёт из
/// камеры, цель берёт <c>SelectionManager.RaycastTransparentAware</c> (её зовёт <c>Update</c>), решение
/// о клике — <c>SelectionManager.HandleClickOnElement</c> (её зовёт <c>Update</c> же). Своего пикинга у
/// режима помещения нет: только <c>EditModeManager.IsInteractable</c> по категории элемента.
///
/// Лучи берутся только там, где над верхом пола нет НИЧЕГО, кроме самого пола и подложки: клик по
/// мебели в режиме помещения снимает выделение по задумке (RoomMode_ClickRegularBoard_ShouldDeselect),
/// и к этому багу отношения не имеет.</summary>
public class RoomEditFloorClickSceneTests
{
    private const string FixtureName = "Fixtures/distance-guides-scene.save.json";
    private const float GridStepMetres = 0.25f;
    private const float PlateHalfX = 1.5f;
    private const float PlateHalfZ = 3.5f;

    private ProjectLoadStateGuard? _guard;
    private FloorElement _floor = null!;
    private BasePlate _plate = null!;
    private readonly List<GameObject> _spawned = new List<GameObject>();

    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        var path = Path.Combine(Application.dataPath, "Tests/EditMode", FixtureName);
        Assert.IsTrue(File.Exists(path), $"фикстура не найдена: {path}");

        _guard = ProjectLoadStateGuard.Capture();
        KitchenSettings.Instance.AutoSave = false;
        ClearScene();

        var data = SaveLoadManager.Deserialize(File.ReadAllText(path));
        Assert.IsNotNull(data, "фикстура не разобралась");
        SaveLoadManager.RestoreScene(data!);

        var floors = Object.FindObjectsByType<FloorElement>();
        Assert.AreEqual(1, floors.Length, "посылка: в фрозене ровно один пол — Pol_1");
        _floor = floors[0];
        var plate = Object.FindAnyObjectByType<BasePlate>();
        Assert.IsNotNull(plate, "посылка: подложка восстановлена вместе со сценой");
        _plate = plate!;
        Physics.SyncTransforms();
    }

    [OneTimeTearDown]
    public void OneTimeTearDown()
    {
        ClearScene();
        EditModeManager.Reset();
        _guard?.Restore();
    }

    [SetUp]
    public void SetUp() => EditModeManager.Reset();

    [TearDown]
    public void TearDown()
    {
        foreach (var go in _spawned) if (go != null) Object.DestroyImmediate(go);
        _spawned.Clear();
        EditModeManager.Reset();
    }

    private static void ClearScene()
    {
        foreach (var e in Object.FindObjectsByType<KitchenElement>())
            if (e != null) Object.DestroyImmediate(e.gameObject);
        PartRegistry.Clear();
        GroupManager.Clear();
        CommandStack.Clear();
        ElementFactory.ClearPools();
        MaterialManager.ClearCache();
        ProjectInstructions.Reset();
        ProjectRooms.Reset();
        ProjectFloorplans.Reset();
    }

    private void EnterRoomMode()
    {
        EditModeManager.SetMode(EditMode.Room);
        SceneVisibilityManager.Apply();
        Physics.SyncTransforms();
    }

    private Camera MakeCamera()
    {
        var go = new GameObject("RoomEditClickCamera");
        go.tag = "MainCamera";
        _spawned.Add(go);
        return go.AddComponent<Camera>();
    }

    private static Ray RayFrom(Camera cam, Vector3 position, Vector3 target)
    {
        cam.transform.position = position;
        if ((target - position).normalized == Vector3.down) cam.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        else cam.transform.LookAt(target);
        return cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
    }

    private IEnumerable<Vector3> GridOnTheFloorTop()
    {
        float y = _floor.transform.position.y + _floor.transform.localScale.y * 0.5f;
        for (float x = -PlateHalfX; x <= PlateHalfX; x += GridStepMetres)
            for (float z = -PlateHalfZ; z <= PlateHalfZ; z += GridStepMetres)
                yield return new Vector3(x, y, z);
    }

    private bool OnlyFloorAndPlateUnder(Ray ray, out bool plateAmongThem)
    {
        plateAmongThem = false;
        bool floorSeen = false;
        foreach (var h in Physics.RaycastAll(ray, 200f))
        {
            if (h.collider.GetComponentInParent<FloorElement>() == _floor) { floorSeen = true; continue; }
            if (h.collider.GetComponentInParent<BasePlate>() == _plate) { plateAmongThem = true; continue; }
            return false;
        }
        return floorSeen;
    }

    private static string Describe(Ray ray)
    {
        var hits = Physics.RaycastAll(ray, 200f).OrderBy(h => h.distance)
            .Select(h => $"{h.collider.name}[{h.collider.GetType().Name}, enabled={h.collider.enabled}]@{h.distance:F4}");
        return string.Join(" -> ", hits);
    }

    private sealed class Sweep
    {
        public int Candidates;
        public int WithPlateUnder;
        public readonly List<string> Misses = new List<string>();
        public Ray FirstFreeRay;
        public bool HasFreeRay;
    }

    private Sweep SweepRays(System.Func<Vector3, (Vector3 origin, Vector3 target)> rayFor, bool shift = false)
    {
        var cam = MakeCamera();
        var sweep = new Sweep();
        foreach (var point in GridOnTheFloorTop())
        {
            var (origin, target) = rayFor(point);
            var ray = RayFrom(cam, origin, target);
            Physics.SyncTransforms();
            if (!OnlyFloorAndPlateUnder(ray, out bool plate)) continue;

            sweep.Candidates++;
            if (plate) sweep.WithPlateUnder++;
            if (!sweep.HasFreeRay) { sweep.FirstFreeRay = ray; sweep.HasFreeRay = true; }

            var picked = SelectionManager.RaycastTransparentAware(ray, shift);
            if (picked != _floor)
                sweep.Misses.Add($"({point.x:F2}; {point.z:F2}) выбрано {(picked == null ? "ничего" : picked.PartName)} | {Describe(ray)}");
        }
        return sweep;
    }

    private static void AssertSweepIsMeaningful(Sweep sweep)
    {
        Assert.Greater(sweep.Candidates, 0,
            "посылка: над полом в фрозене есть свободные от мебели точки — иначе проверять нечего");
        Assert.Greater(sweep.WithPlateUnder, 0,
            "посылка: хотя бы часть свободных точек лежит над подложкой — именно там верх пола с ней в одной плоскости");
    }

    private static string Report(Sweep sweep) =>
        $"промахов {sweep.Misses.Count} из {sweep.Candidates} (над подложкой {sweep.WithPlateUnder}); первые: "
        + string.Join(" || ", sweep.Misses.Take(3));

    [TestCase(false)]
    [TestCase(true)]
    public void RoomMode_RayStraightDownOntoFloorTop_PicksTheFloor(bool shift)
    {
        EnterRoomMode();

        var sweep = SweepRays(p => (p + Vector3.up * 20f, p), shift);

        AssertSweepIsMeaningful(sweep);
        Assert.IsEmpty(sweep.Misses,
            "в режиме помещения луч сверху в верх пола обязан дать пол. " + Report(sweep));
    }

    [Test]
    public void RoomMode_ObliqueRayOntoFloorTop_PicksTheFloor()
    {
        EnterRoomMode();

        var sweep = SweepRays(p => (p + new Vector3(3f, 6f, -3f), p));

        AssertSweepIsMeaningful(sweep);
        Assert.IsEmpty(sweep.Misses,
            "камера режима помещения смотрит на пол под углом: наклонный луч в верх пола тоже даёт пол. "
            + Report(sweep));
    }

    [Test]
    public void RoomMode_ClickOnFloorTop_SelectsTheFloor()
    {
        EnterRoomMode();
        var sweep = SweepRays(p => (p + Vector3.up * 20f, p));
        AssertSweepIsMeaningful(sweep);
        var sm = MakeSelectionManager();

        var picked = SelectionManager.RaycastTransparentAware(sweep.FirstFreeRay, false);
        sm.HandleClickOnElement(picked, ctrlHeld: false);

        Assert.AreEqual(_floor, sm.Selected,
            "клик по верху пола в режиме помещения выделяет пол (цель луча: "
            + (picked == null ? "ничего" : picked.PartName) + "). " + Describe(sweep.FirstFreeRay));
    }

    [Test]
    public void RoomMode_RayOntoFloorSide_PicksTheFloor()
    {
        EnterRoomMode();
        var cam = MakeCamera();
        float y = _floor.transform.position.y;
        var ray = RayFrom(cam, new Vector3(-6f, y, 0f), new Vector3(0f, y, 0f));
        Physics.SyncTransforms();

        Assert.AreEqual(_floor, SelectionManager.RaycastTransparentAware(ray, false),
            "парный контроль: торец пола выделялся всегда. " + Describe(ray));
    }

    [Test]
    public void NormalMode_ClickOnFloorTop_DeselectsAsBefore()
    {
        var sweep = SweepRays(p => (p + Vector3.up * 20f, p));
        AssertSweepIsMeaningful(sweep);
        var sm = MakeSelectionManager();
        var board = Object.FindObjectsByType<KitchenElement>()
            .First(e => e != _floor && e.GetComponent<Wall>() == null && e.GetComponent<BasePlate>() == null
                && EditModeManager.IsInteractable(e));
        sm.Select(board);
        Assert.AreEqual(board, sm.Selected, "посылка: деталь выделена");

        var picked = SelectionManager.RaycastTransparentAware(sweep.FirstFreeRay, false);
        sm.HandleClickOnElement(picked, ctrlHeld: false);

        Assert.IsNull(sm.Selected,
            "в обычном режиме пол недоступен: клик по его верху, как и раньше, снимает выделение");
    }

    private SelectionManager MakeSelectionManager()
    {
        var go = new GameObject("RoomEditClickSelection");
        _spawned.Add(go);
        return go.AddComponent<SelectionManager>();
    }
}
