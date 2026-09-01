using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

public class OpeningCollisionScanTests
{
    private readonly List<GameObject> _spawned = new List<GameObject>();

    [SetUp]
    public void SetUp() => PartRegistry.Clear();

    [TearDown]
    public void TearDown()
    {
        foreach (var go in _spawned)
            if (go != null) Object.DestroyImmediate(go);
        _spawned.Clear();
        PartRegistry.Clear();
    }

    private KitchenElement MakePart(string name, Vector3 pos, int w, int h, int d)
    {
        var go = ElementFactory.CreatePart(new Vector3Int(w, h, d), name, pos);
        _spawned.Add(go);
        return go.GetComponent<KitchenElement>()!;
    }

    private DrawerElement MakeDrawer(string name, Vector3 pos)
    {
        var go = ElementFactory.CreateDrawer(DrawerType.A, 350, DrawerColor.Anthracite, 400, name, pos);
        _spawned.Add(go);
        return go.GetComponent<DrawerElement>()!;
    }

    [Test]
    public void ScanStep_IsFinerThanTheThinnestBoard_SoNothingIsFlownThrough()
    {
        float travelMm = DrawerConstants.DRAWER_SLIDE_METERS / AppConstants.MM_TO_UNITS;
        float stepMm = travelMm / OpeningCollision.ScanSteps;

        Assert.Less(stepMm, (float)DrawerConstants.MOVENTO_BOARD_THICKNESS,
            "пересечение НЕмонотонно по прогрессу: тонкое препятствие можно «пролететь "
            + "насквозь», если конечная поза уже за ним. Путь поэтому сканируется, и "
            + "шаг обязан быть мельче самой тонкой детали (плита 16 мм), иначе "
            + "сканирование её перешагнёт");
    }

    [Test]
    public void Drawer_TouchingItsCarcassWhenClosed_StillOpensFully()
    {
        var drawer = MakeDrawer("D", new Vector3(0f, 0.043f, 0f));
        float frontZ = drawer.ToGeometry().Max.z;
        float boardHalf = 9f * AppConstants.MM_TO_UNITS;
        var container = MakePart("Front", new Vector3(0f, 0.043f, frontZ + 0.002f + boardHalf),
            400, 86, 18);

        drawer.SetOpen(true);
        drawer.StepAnimation(1f);

        Assert.AreEqual(1f, drawer.AnimProgress, 1e-4f,
            "деталь, которой ящик КАСАЕТСЯ уже в закрытом состоянии, — это его "
            + "контейнер (корпус, рама, фасад), а не препятствие. Не исключив такие, "
            + "ящик не выехал бы из корпуса, а дверца — из рамы");
        Assert.IsNotNull(container, "контейнер остаётся в сцене — его именно исключили, а не удалили");
    }

    [Test]
    public void Drawer_ObstacleBeyondTheTouchGap_StopsIt()
    {
        var drawer = MakeDrawer("D", new Vector3(0f, 0.043f, 0f));
        float frontZ = drawer.ToGeometry().Max.z;
        float boardHalf = 9f * AppConstants.MM_TO_UNITS;
        float beyondTouch = (OpeningCollision.TouchGapMm + 20f) * AppConstants.MM_TO_UNITS;
        MakePart("Obs", new Vector3(0f, 0.043f, frontZ + beyondTouch + boardHalf), 400, 86, 18);

        drawer.SetOpen(true);
        drawer.StepAnimation(1f);

        Assert.Less(drawer.AnimProgress, 1f,
            "контроль к предыдущему: та же деталь, отодвинутая за порог касания, снова "
            + "препятствие. Без него «контейнер исключается» было бы неотличимо от "
            + "«исключается вообще всё»");
    }
}
