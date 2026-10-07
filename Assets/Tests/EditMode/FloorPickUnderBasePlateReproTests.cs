using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

/// <summary>Баг: в режиме помещения клик по полю СВЕРХУ не выделял пол, а клик по торцу выделял.
///
/// Причина: BasePlate (подложка 18 мм, верх на y=0) остаётся с коллайдером, когда пол есть и
/// подложка не нарисована. Верх пола тоже на y=0, поэтому луч сверху сначала (или вничью) попадал в
/// подложку; та не элемент для выбора (HasElement=false в SceneClickPlan), и клик превращался в
/// «снять выделение». Торец пола ниже подложки, её коллайдер луч не пересекал — торец работал.
///
/// Правило: подложка выбора не принимает и не загораживает. Луч выбора проходит сквозь неё к
/// тому, что лежит дальше. Так же ведут себя ПКМ и пипетка, поэтому они тоже под защитой.
///
/// Корень устранён у источника: коллайдер подложки живёт только пока она нарисована (BasePlate.SetShown),
/// поэтому под полом сырой Physics.Raycast подложки не видит вовсе. Фильтр SelectionManager.IsBasePlateCollider
/// остался второй линией обороны и держит случай нарисованной подложки (пустая сцена без пола).</summary>
public class FloorPickUnderBasePlateReproTests
{
    private const int FloorSizeMm = 2000;
    private const int FloorThicknessMm = 100;

    private readonly List<GameObject> _spawned = new List<GameObject>();

    [SetUp]
    public void SetUp()
    {
        EditModeManager.Reset();
    }

    [TearDown]
    public void TearDown()
    {
        foreach (var go in _spawned) if (go != null) Object.DestroyImmediate(go);
        _spawned.Clear();
        EditModeManager.Reset();
    }

    private FloorElement MakeFloor(bool polygon)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.transform.position = new Vector3(0f, -0.05f, 0f);
        _spawned.Add(go);
        var floor = go.AddComponent<FloorElement>();
        floor.PartName = "Пол";
        floor.DimensionsMM = new Vector3Int(FloorSizeMm, FloorThicknessMm, FloorSizeMm);
        if (polygon)
        {
            int h = FloorSizeMm / 2;
            floor.SetPolygonLocalMm(new[]
            {
                new Vector2Int(-h, -h), new Vector2Int(h, -h), new Vector2Int(h, h), new Vector2Int(-h, h),
            });
        }
        return floor;
    }

    private BasePlate MakePlate(float liftMetres)
    {
        var plate = BasePlate.Create();
        _spawned.Add(plate.gameObject);
        plate.transform.position += new Vector3(0f, liftMetres, 0f);
        return plate;
    }

    private static Ray FromAbove => new Ray(new Vector3(0.3f, 2f, 0.2f), Vector3.down);

    private static Ray AtTheEdge => new Ray(new Vector3(3f, -0.05f, 0.2f), Vector3.left);

    private static KitchenElement? Pick(Ray ray, bool shift)
    {
        Physics.SyncTransforms();
        return SelectionManager.RaycastTransparentAware(ray, shift);
    }

    [TestCase(false, false)]
    [TestCase(true, false)]
    [TestCase(false, true)]
    [TestCase(true, true)]
    public void Floor_TopClick_PlateCoplanarWithFloorTop_SelectsTheFloorInRoomMode(bool polygon, bool shift)
    {
        EditModeManager.SetMode(EditMode.Room);
        var floor = MakeFloor(polygon);
        MakePlate(0f);

        var picked = Pick(FromAbove, shift);

        Assert.AreEqual(floor, picked,
            "верх пола и верх подложки лежат на одной плоскости y=0: луч сверху не должен "
            + "остановиться на подложке, иначе клик по полу снимает выделение, а торец выделяет");
        Assert.IsTrue(EditModeManager.IsInteractable(picked!), "в режиме помещения пол доступен");
    }

    [TestCase(false)]
    [TestCase(true)]
    public void Floor_TopClick_PlateAboveFloorTop_SelectsTheFloor(bool polygon)
    {
        EditModeManager.SetMode(EditMode.Room);
        var floor = MakeFloor(polygon);
        MakePlate(0.0005f);

        Assert.AreEqual(floor, Pick(FromAbove, shift: false),
            "подложка на 0,5 мм ВЫШЕ пола однозначно первая под лучом: без вничьей, без "
            + "случайного порядка коллайдеров — подложка всё равно не загораживает выбор");
    }

    [TestCase(false)]
    [TestCase(true)]
    public void Floor_SideClick_StillSelectsTheFloor(bool polygon)
    {
        EditModeManager.SetMode(EditMode.Room);
        var floor = MakeFloor(polygon);
        MakePlate(0f);

        Assert.AreEqual(floor, Pick(AtTheEdge, shift: false),
            "торец пола выделялся и до правки: парный контроль, чтобы правка не сломала его");
    }

    [Test]
    public void Floor_TopClick_NormalMode_StillResolvesToANotInteractableFloor()
    {
        var floor = MakeFloor(polygon: false);
        MakePlate(0f);

        var picked = Pick(FromAbove, shift: false);

        Assert.AreEqual(floor, picked, "луч находит пол и в обычном режиме");
        Assert.IsFalse(EditModeManager.IsInteractable(picked!),
            "в обычном режиме пол недоступен: клик по нему снимает выделение, как и раньше "
            + "(раньше его перехватывала подложка с тем же итогом)");
    }

    [TestCase(0f, false)]
    [TestCase(0.0005f, false)]
    [TestCase(0f, true)]
    [TestCase(0.0005f, true)]
    public void FloorPresent_PlateIsNotUnderTheRay_SoThePickNeedsNoFilter(float lift, bool polygon)
    {
        var floor = MakeFloor(polygon);
        var plate = MakePlate(lift);
        Physics.SyncTransforms();

        var hits = Physics.RaycastAll(FromAbove);
        bool floorHit = false, plateHit = false;
        var seen = new List<string>();
        foreach (var h in hits)
        {
            seen.Add(h.collider.name + "@" + h.distance.ToString("F4"));
            if (h.collider.GetComponentInParent<FloorElement>() == floor) floorHit = true;
            if (h.collider.GetComponentInParent<BasePlate>() == plate) plateHit = true;
        }

        Assert.IsTrue(floorHit, "посылка: пол под лучом. Нашли: " + string.Join(", ", seen));
        Assert.IsFalse(plateHit,
            "пока есть пол, плита не нарисована, и её коллайдер не должен стоять на пути ни у одного "
            + "луча — иначе каждый потребитель Physics.Raycast (замер, лампа, камера, выбор) обязан "
            + "помнить про фильтр. Нашли: " + string.Join(", ", seen));
        Assert.IsTrue(Physics.Raycast(FromAbove, out var first), "посылка: луч во что-то попадает");
        Assert.AreEqual(floor, first.collider.GetComponentInParent<FloorElement>(),
            "первое попадание сырого луча — пол");
    }

    [TestCase(0f)]
    [TestCase(0.0005f)]
    public void Floor_TopClick_EvenWithThePlateColliderForcedOn_TheFilterStillLetsThePickThrough(float lift)
    {
        EditModeManager.SetMode(EditMode.Room);
        var floor = MakeFloor(polygon: false);
        var plate = MakePlate(lift);
        plate.GetComponent<Collider>().enabled = true;
        Physics.SyncTransforms();

        var hits = new List<RaycastHit>(Physics.RaycastAll(FromAbove));
        Assert.IsTrue(hits.Exists(h => h.collider.GetComponentInParent<BasePlate>() == plate),
            "посылка: коллайдер плиты включён и стоит под лучом");
        Assert.AreEqual(floor, Pick(FromAbove, shift: false), "вторая линия обороны: фильтр SelectionManager");
        Assert.AreEqual(floor, Pick(FromAbove, shift: true));
        Assert.AreEqual(LevelRegistry.CurrentId, LevelRegistry.LevelOf(plate.Element).id,
            "подложка на текущем уровне — иначе без фильтра выбор отдавал бы null, а не подложку");
    }

    [Test]
    public void BasePlate_ClickedAlone_PicksNothing()
    {
        MakePlate(0f);

        Assert.IsNull(Pick(FromAbove, shift: false),
            "подложка не цель выбора: клик по ней — клик в пустоту, то есть снятие выделения");
        Assert.IsNull(Pick(FromAbove, shift: true));
    }

    [Test]
    public void Board_StandingOnFloorAbovePlate_IsStillPickedFirst()
    {
        MakeFloor(polygon: false);
        MakePlate(0f);
        var boardGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
        _spawned.Add(boardGo);
        var board = boardGo.AddComponent<KitchenElement>();
        board.PartName = "Доска";
        board.DimensionsMM = new Vector3Int(600, 400, 600);
        boardGo.transform.position = new Vector3(0.3f, 0.2f, 0.2f);

        Assert.AreEqual(board, Pick(FromAbove, shift: false),
            "обычный случай не меняется: предмет над полом остаётся первой целью под лучом");
    }

    [Test]
    public void RightClickPick_FloorTop_SeesTheFloorThroughThePlate()
    {
        EditModeManager.SetMode(EditMode.Room);
        var floor = MakeFloor(polygon: false);
        MakePlate(0.0005f);
        Physics.SyncTransforms();

        Assert.AreEqual(floor, SelectionManager.RaycastElementThroughGizmos(FromAbove, shiftHeld: false),
            "ПКМ ходит другим путём (RaycastElementThroughGizmos), и подложка загораживала и его");
    }
}
