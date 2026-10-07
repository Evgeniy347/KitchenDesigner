using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

/// <summary>Плита BasePlate под пользовательским полом не рисуется и не твёрдая (BasePlateTests), но её
/// грани оставались кандидатами привязки: деталь у края комнаты прилипала к невидимой плите, которая
/// торчит из-под пола на 0,5 м, а верх плиты притягивал к y = 0 даже там, где верх пола стоит выше.
/// Пока своих полов нет, плита - нарисованная земля пустой сцены, и привязка к ней остаётся: это
/// единственное, что опускает поднятую деталь на землю.
///
/// Три входа к одному правилу: перемещение и изменение размера собирают кандидатов через
/// SnapSceneGeometry.For, диагностика snap_diagnose строит свой список сама, и все три обязаны
/// сходиться (AGENTS: правило отбора кандидатов доезжает до Diagnose тем же коммитом).</summary>
public class BasePlateSnapCandidateTests
{
    private readonly List<GameObject> _spawned = new List<GameObject>();
    private KitchenSettingsData? _before;

    [SetUp]
    public void SetUp()
    {
        _before = KitchenSettings.Instance.ToData();
        var s = KitchenSettings.Instance;
        s.SnapEnabled = true;
        s.SnapThreshold = 50f;
        s.GridEnabled = false;
    }

    [TearDown]
    public void TearDown()
    {
        foreach (var go in _spawned) if (go != null) Object.DestroyImmediate(go);
        _spawned.Clear();
        if (_before != null) KitchenSettings.Instance.ApplyFrom(_before);
    }

    private FloorElement MakeUserFloor()
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.transform.position = new Vector3(0f, -0.05f, 0f);
        _spawned.Add(go);
        var floor = go.AddComponent<FloorElement>();
        floor.PartName = "Пол";
        floor.DimensionsMM = new Vector3Int(2000, 100, 2000);
        return floor;
    }

    private BasePlate MakePlate()
    {
        var plate = BasePlate.Create();
        _spawned.Add(plate.gameObject);
        return plate;
    }

    private static readonly Vector3 BeyondTheFloorEdgeAbovePlateTop = new Vector3(1.3f, 0.215f, 0f);

    private KitchenElement MakeBoardBeyondTheFloorEdge()
    {
        var go = ElementFactory.CreatePart(new Vector3Int(400, 400, 400), "Доска", BeyondTheFloorEdgeAbovePlateTop);
        _spawned.Add(go);
        return go.GetComponent<KitchenElement>();
    }

    [Test]
    public void Snap_IgnoresBasePlateFaces_WhileAUserFloorCoversIt()
    {
        var floor = MakeUserFloor();
        var plate = MakePlate();
        var board = MakeBoardBeyondTheFloorEdge();

        var snap = SnapSystem.TrySnap(board, new List<KitchenElement> { floor, plate.Element },
            BeyondTheFloorEdgeAbovePlateTop);

        Assert.IsTrue(plate.CoveredByUserFloor, "посылка: пол есть, плита им накрыта");
        Assert.IsFalse(snap.snapped,
            "доска в 15 мм над верхом невидимой плиты, за краем пола (пол кончается на 1,0 м, плита на 1,5 м): "
            + "прилипать ей не к чему. Прилипла к " + snap.targetName);
    }

    [Test]
    public void Snap_ToTheDrawnBasePlate_StillSeatsAPartOnTheGround()
    {
        var plate = MakePlate();
        var board = MakeBoardBeyondTheFloorEdge();

        var snap = SnapSystem.TrySnap(board, new List<KitchenElement> { plate.Element },
            BeyondTheFloorEdgeAbovePlateTop);

        Assert.IsFalse(plate.CoveredByUserFloor, "посылка: полов нет, плита - нарисованная земля");
        Assert.IsTrue(snap.snapped, "без своего пола плита - единственная земля, и деталь над ней прилипает");
        Assert.AreEqual(0.2f, snap.position.y, 0.001f, "низ доски встал на верх плиты (y = 0)");
    }

    [Test]
    public void Diagnose_DoesNotListACoveredBasePlate_AndAgreesWithTheSnap()
    {
        var floor = MakeUserFloor();
        var plate = MakePlate();
        var board = MakeBoardBeyondTheFloorEdge();
        var scene = new List<KitchenElement> { floor, plate.Element };

        var report = SnapSystem.Diagnose(board, scene, BeyondTheFloorEdgeAbovePlateTop);

        Assert.IsFalse(report.neighbors.Exists(n => n.name == plate.Element.PartName),
            "snap_diagnose не может рассказывать про соседа, которого привязка не рассматривает");
        Assert.IsFalse(report.wouldSnap, "и обещать привязку к нему");
    }

    [Test]
    public void SnapGeometry_SkipsOnlyACoveredBasePlate()
    {
        var floor = MakeUserFloor();
        var plate = MakePlate();
        var board = MakeBoardBeyondTheFloorEdge();
        var scene = new List<KitchenElement> { floor, plate.Element, board };

        var covered = SnapSceneGeometry.For(scene, board);
        FloorElement.RefreshBasePlateVisibility(except: floor);
        var uncovered = SnapSceneGeometry.For(scene, board);

        Assert.AreEqual(2, covered.Count, "пол и доска; плита под полом не кандидат (ни при перемещении, ни при растяжении)");
        Assert.AreEqual(3, uncovered.Count, "убрали пол - плита снова земля и снова кандидат");
    }
}
