using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;
using KitchenDesigner.Core.Measure;

/// <summary>Направляющие расстояний и равный зазор на ЗАМОРОЖЕННОЙ копии проекта пользователя
/// (<c>Fixtures/distance-guides-scene.save.json</c> — <c>docs/example.save.json</c> от
/// 2026-10-04 без истории отмены; живой файл не трогается).
///
/// Сцена: четыре доски 600×2700×16 на x = 3,176 м — Polka_5 (z −0,647, вплотную к стене
/// P_BATH_N), Polka_29 (−1,245), Polka_30 (−1,843), Polka_22 (−3,612, вплотную к W150_05).
/// Шаг центров 598 мм, зазор от грани до грани 582 мм. Пользователь назвал 598 и «около
/// −1843»: 598 — это центры, −1843 — центр Polka_30 в позиции равного зазора, и он там уже
/// стоит. Доски одной толщины, поэтому равенство центров и равенство зазоров здесь совпадают;
/// механика меряет зазор (EqualGapMatrixTests ловит разницу на соседях разной толщины).
///
/// Сцена грузится один раз на класс; каждый тест отменяет свой жест.</summary>
public class DistanceGuidesSceneTests
{
    private const string FixtureName = "Fixtures/distance-guides-scene.save.json";
    private const float Mm = AppConstants.MM_TO_UNITS;
    private const float PolkaEqualGapZ = -1.843f;
    private const float SurfaceGapMm = 582f;

    private ProjectLoadStateGuard? _guard;
    private List<KitchenElement> _scene = new List<KitchenElement>();
    private GameObject? _host;
    private ElementMover? _mover;
    private ResizeHandleManager? _resizer;
    private KitchenElement _polka30 = null!;
    private Vector3 _polka30Start;
    private bool _snapBefore;

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
        _scene = SaveLoadManager.RestoreScene(data!)
            .Select(g => g.GetComponent<KitchenElement>()).Where(e => e != null).ToList()!;

        _polka30 = Named("Polka_30");
        _polka30Start = _polka30.transform.position;
        Assert.AreEqual(PolkaEqualGapZ, _polka30Start.z, 0.0001f,
            "посылка: Polka_30 во фрозене стоит на z = −1843 мм");

        _host = new GameObject("DistanceGuidesHost");
        _mover = _host.AddComponent<ElementMover>();
        _resizer = _host.AddComponent<ResizeHandleManager>();
    }

    [OneTimeTearDown]
    public void OneTimeTearDown()
    {
        if (_host != null) Object.DestroyImmediate(_host);
        ClearScene();
        _guard?.Restore();
        DistanceGuideStore.Clear();
    }

    [SetUp]
    public void SetUp()
    {
        _snapBefore = KitchenSettings.Instance.SnapEnabled;
        KitchenSettings.Instance.SnapEnabled = true;
        KitchenSettings.Instance.BlockOnViolation = false;
        Assert.AreEqual(50f, KitchenSettings.Instance.SnapThreshold, 0.001f,
            "посылка: порог прилипания из проекта — 50 мм");
    }

    [TearDown]
    public void TearDown()
    {
        if (ElementMover.IsDragging) _mover!.TryCancelDragFromEscape();
        KitchenSettings.Instance.SnapEnabled = _snapBefore;
        Assert.AreEqual(0f, (_polka30.transform.position - _polka30Start).magnitude, 0.00001f,
            "тест обязан вернуть Polka_30 на место — сцена общая на класс");
    }

    [Test]
    public void Drag_Polka30TwentyMillimetresTowardPolka29_SettlesAtMinus1843WithTheSecondLine()
    {
        _mover!.BeginDragOn(_polka30);
        _mover.DragFrameOn(_polka30Start + new Vector3(0f, 0f, 0.020f));

        Assert.AreEqual(PolkaEqualGapZ, _polka30.transform.position.z, 0.0001f,
            "кандидат на 20 мм ближе к Polka_29 обязан вернуться к равному зазору: центр −1843, "
            + "грань −1835, зазор до Polka_29 582 = зазору Polka_29↔Polka_5");
        var plusZ = LinesAlong(Vector3.forward);
        Assert.AreEqual(2, plusZ.Count,
            "по +Z две линии: Polka_30↔Polka_29 и её продолжение Polka_29↔Polka_5");
        Assert.AreEqual(SurfaceGapMm, plusZ[0].Length / Mm, 0.5f, "текущий зазор — 582 мм");
        Assert.AreEqual(SurfaceGapMm, plusZ[1].Length / Mm, 0.5f, "существующий зазор — 582 мм");
        Assert.IsTrue(plusZ[0].EqualGap && plusZ[1].EqualGap, "обе отмечены равными");
        Assert.AreEqual(-1.237f, plusZ[1].A.z, 0.0001f,
            "вторая линия начинается от дальней грани Polka_29");
    }

    [Test]
    public void Drag_Polka30EightyMillimetres_ReleasesTheSnapAndDropsTheSecondLine()
    {
        _mover!.BeginDragOn(_polka30);
        _mover.DragFrameOn(_polka30Start + new Vector3(0f, 0f, 0.020f));
        Assume.That(LinesAlong(Vector3.forward).Count, Is.EqualTo(2));

        _mover.DragFrameOn(_polka30Start + new Vector3(0f, 0f, 0.080f));

        Assert.AreEqual(PolkaEqualGapZ + 0.080f, _polka30.transform.position.z, 0.0001f,
            "80 мм разницы зазоров — за порогом 50 мм: деталь стоит там, куда её тянут");
        var plusZ = LinesAlong(Vector3.forward);
        Assert.AreEqual(1, plusZ.Count, "прилипание отпустило — вторая линия обязана исчезнуть");
        Assert.AreEqual(SurfaceGapMm - 80f, plusZ[0].Length / Mm, 0.5f, "до Polka_29 осталось 502 мм");
    }

    [Test]
    public void Drag_SnapOffInTheProject_GuidesStayButNothingPulls()
    {
        KitchenSettings.Instance.SnapEnabled = false;
        _mover!.BeginDragOn(_polka30);
        _mover.DragFrameOn(_polka30Start + new Vector3(0f, 0f, 0.020f));

        Assert.AreEqual(PolkaEqualGapZ + 0.020f, _polka30.transform.position.z, 0.0001f,
            "snapEnabled = false в проекте выключает и равный зазор");
        Assert.AreEqual(SurfaceGapMm - 20f, LinesAlong(Vector3.forward)[0].Length / Mm, 0.5f,
            "а направляющие расстояний остаются: 562 мм");
    }

    [Test]
    public void Drag_GuidesSettingOff_DrawsNothingButTheEqualGapStillSnaps()
    {
        KitchenSettings.Instance.DistanceGuides = false;
        try
        {
            _mover!.BeginDragOn(_polka30);
            _mover.DragFrameOn(_polka30Start + new Vector3(0f, 0f, 0.020f));

            Assert.IsFalse(DistanceGuideStore.Showing,
                "«Направляющие расстояний» выключены — линий и подписей нет");
            Assert.AreEqual(PolkaEqualGapZ, _polka30.transform.position.z, 0.0001f,
                "равный зазор — часть привязки, его включает snapEnabled, а не этот переключатель");
        }
        finally
        {
            KitchenSettings.Instance.DistanceGuides = true;
        }
    }

    [Test]
    public void Drag_Polka30_MeasuresEveryDirectionFaceToFace()
    {
        _mover!.BeginDragOn(_polka30);
        _mover.DragFrameOn(_polka30Start + new Vector3(0f, 0f, 0.080f));

        Assert.AreEqual(1753f + 80f, LinesAlong(Vector3.back)[0].Length / Mm, 0.5f,
            "по −Z — до Polka_22: −1,771 − (−3,604) = 1833 мм от грани до грани");
        Assert.AreEqual(1041f, LinesAlong(Vector3.left)[0].Length / Mm, 0.5f,
            "по −X — стена W250_01 в 1041 мм от грани доски");
        Assert.AreEqual(0, LinesAlong(Vector3.right).Count,
            "по +X доска стоит вплотную к стене W125_13: нулевой линии не рисуем");
        Assert.AreEqual(0, LinesAlong(Vector3.down).Count, "доска стоит на полу — вниз ноль");
    }

    [Test]
    public void ReleaseAndCancel_ClearTheGuides()
    {
        _mover!.BeginDragOn(_polka30);
        _mover.DragFrameOn(_polka30Start + new Vector3(0f, 0f, 0.080f));
        Assume.That(DistanceGuideStore.Showing, Is.True);

        _mover.TryCancelDragFromEscape();

        Assert.IsFalse(DistanceGuideStore.Showing, "Esc отменяет жест — направляющие уходят");

        _mover.BeginDragOn(_polka30);
        _mover.DragFrameOn(_polka30Start + new Vector3(0f, 0f, 0.080f));
        _mover.FinishDragNow();
        Assert.IsFalse(DistanceGuideStore.Showing, "отпускание кнопки — тоже");
        CommandStack.Undo();
    }

    [Test]
    public void Resize_Polka30PlusZFace_GrowsUntilItsGapEqualsTheNextOne()
    {
        _polka30.transform.position = _polka30Start + new Vector3(0f, 0f, -0.100f);
        int plusZ = FaceIndexFacing(_polka30, Vector3.forward);
        _resizer!.BeginDragOn(_polka30, plusZ);
        _resizer.ResizeFrameBy(0.120f);

        Assert.AreEqual(116, _polka30.DimensionsMM.z,
            "Polka_30 отодвинута на 100 мм (зазор до Polka_29 682 мм); грань +Z, утянутая на 120 мм, "
            + "прилипает туда, где зазор снова 582 мм, — толщина 16 + 100 = 116 мм. Без равного "
            + "зазора было бы 136");
        Assert.IsTrue(LinesAlong(Vector3.forward).Any(l => l.EqualGap),
            "и вторая линия Polka_29↔Polka_5 показана");

        _resizer.FinishDragNow();
        Assert.IsFalse(DistanceGuideStore.Showing, "конец ресайза убирает направляющие");
        _polka30.DimensionsMM = new Vector3Int(600, 2700, 16);
        _polka30.transform.position = _polka30Start;
    }

    [Test]
    public void Drag_TenFrames_BuildOneIndexAndNeverScanTheScenePerRay()
    {
        int built = DistanceGuideSession.IndexesBuilt;
        _mover!.BeginDragOn(_polka30);
        var index = DragIndex();
        int before = index.BoxTests;

        for (int i = 1; i <= 10; i++)
            _mover.DragFrameOn(_polka30Start + new Vector3(0f, 0f, 0.060f + i * 0.003f));

        int perFrame = (index.BoxTests - before) / 10;
        Assert.AreEqual(1, DistanceGuideSession.IndexesBuilt - built,
            "индекс строится один раз на жест, а не на кадр");
        Assert.Greater(perFrame, 0, "кадр обязан спрашивать индекс — иначе ниже нечего сравнивать");
        Assert.Less(perFrame, _scene.Count,
            $"кадр стоил {perFrame} проверок коробок при {_scene.Count} деталях: шесть лучей и "
            + "равный зазор не вправе перебирать сцену — каждый спрашивает одну клетку");
    }

    private AxisGuideIndex DragIndex()
    {
        Assert.IsNotNull(_mover!.GuideIndex, "жест начат — индекс обязан быть построен");
        return _mover.GuideIndex!;
    }

    private static List<GuideLine> LinesAlong(Vector3 direction) =>
        DistanceGuideStore.Lines
            .Where(l => Vector3.Dot((l.B - l.A).normalized, direction) > Tolerance.ParallelDot)
            .OrderBy(l => Vector3.Dot(l.A, direction))
            .ToList();

    private static int FaceIndexFacing(KitchenElement element, Vector3 direction)
    {
        var faces = element.GetFaces();
        for (int i = 0; i < faces.Length; i++)
            if (Vector3.Dot(faces[i].normal.normalized, direction) > Tolerance.ParallelDot) return i;
        Assert.Fail($"у {element.PartName} нет грани по {direction}");
        return -1;
    }

    private KitchenElement Named(string name)
    {
        var found = _scene.FirstOrDefault(e => e.PartName == name);
        Assert.IsNotNull(found, $"в фикстуре нет {name}");
        return found!;
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
}
