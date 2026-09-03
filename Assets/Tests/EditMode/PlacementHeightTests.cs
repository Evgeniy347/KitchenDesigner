using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

/// <summary>Высота, на которой рождается настенный прибор, обязана дожить
/// до сцены.
///
/// Смеситель для ванны появлялся на полу, хотя ElementSpawner заводил его на
/// 700 мм. Ронял его PlacementController: пока объект висит на курсоре, он
/// КАЖДЫЙ кадр перебивает Y на половину высоты габарита — то есть ставит
/// предмет на пол. Исключения были перечислены типами прямо в условии, и
/// новый настенный тип в этот список, разумеется, не попадал.
///
/// Розетка и выключатель падали ровно так же и с тем же кодом. Их этого
/// никто не заметил: розетка высотой 80 мм, поставленная на пол вместо 850,
/// выглядит просто «низковато», а не сломанно. Поэтому проверка тут не одна
/// на смеситель, а на все четыре типа сразу — симптом у них общий, и чинится
/// он в одном месте.
///
/// Второй тест — обратный, и без него первый бесполезен: обычная доска
/// ОБЯЗАНА лечь на пол. Разреши сохранять высоту всем — и первые четыре
/// проверки останутся зелёными, а вся мебель начнёт висеть в воздухе там,
/// где её отпустили.</summary>
public class PlacementHeightTests
{
    private const float Tol = 1e-3f;

    private readonly List<GameObject> _spawned = new List<GameObject>();
    private PlacementController _placement = null!;

    [SetUp]
    public void SetUp()
    {
        PartRegistry.Clear();

        var camGo = new GameObject("Main Camera");
        camGo.tag = "MainCamera";
        camGo.AddComponent<Camera>();
        camGo.transform.position = new Vector3(0f, 3f, -5f);
        camGo.transform.LookAt(Vector3.zero);
        _spawned.Add(camGo);

        var host = new GameObject("Placement");
        _spawned.Add(host);
        _placement = host.AddComponent<PlacementController>();
    }

    [TearDown]
    public void TearDown()
    {
        foreach (var go in _spawned) if (go != null) Object.DestroyImmediate(go);
        _spawned.Clear();
        foreach (var el in PartRegistry.GetAll())
            if (el != null) Object.DestroyImmediate(el.gameObject);
        PartRegistry.Clear();
    }

    [Test]
    public void ASocketOnTheCursor_StaysAtSwitchboardHeight()
    {
        AssertKeepsHeight(
            go => ElementFactory.CreateSocket(WallDeviceSpec.Default, "Розетка", go),
            WallDeviceLayout.SocketCentreAboveFloorMM,
            "розетка на полу — это не «низковато», это розетка на полу");
    }

    [Test]
    public void ALightSwitchOnTheCursor_StaysAtSwitchboardHeight()
    {
        AssertKeepsHeight(
            go => ElementFactory.CreateLightSwitch(WallDeviceSpec.Default, true, null,
                "Выключатель", go),
            WallDeviceLayout.SwitchCentreAboveFloorMM,
            "выключатель ставят под руку, а не под ногу");
    }

    [Test]
    public void ABathMixerOnTheCursor_StaysAtTapHeight()
    {
        AssertKeepsHeight(
            go => ElementFactory.CreateBathMixer(BathMixerSpec.Default, "Смеситель", go),
            BathMixerLayout.CentreAboveFloorMM(BathMixerSpec.Default),
            "с этого и начали: смеситель падал на пол, и пользователь поднимал его руками "
            + "после каждого создания");
    }

    [Test]
    public void AShowerColumnOnTheCursor_StaysAtDiverterHeight()
    {
        AssertKeepsHeight(
            go => ElementFactory.CreateShowerColumn(ShowerColumnSpec.Default, "Стойка", go),
            ShowerColumnLayout.CentreAboveFloorMM(ShowerColumnSpec.Default),
            "стойка длиннее человека, и уроненная на пол она уходит лейкой под потолок");
    }

    [Test]
    public void APlainBoardOnTheCursor_DropsToTheFloor()
    {
        var dims = new Vector3Int(600, 18, 400);
        var go = ElementFactory.CreatePart(dims, "Доска",
            new Vector3(0f, 2f, 0f));
        _spawned.Add(go);
        var board = go.GetComponent<KitchenElement>();

        _placement.Begin(board);

        Assert.AreEqual(dims.y * 0.5f * AppConstants.MM_TO_UNITS, go.transform.position.y, Tol,
            "мебель на курсоре ЛОЖИТСЯ на пол, и это не мелочь рядом с проверками выше: "
            + "разреши сохранять высоту всем — они останутся зелёными, а доски начнут "
            + "висеть в воздухе там, где их отпустили");
    }

    private void AssertKeepsHeight(System.Func<Vector3, GameObject> create, float centreMM,
        string why)
    {
        float centreUnits = centreMM * AppConstants.MM_TO_UNITS;
        var go = create(new Vector3(0f, centreUnits, 0f));
        _spawned.Add(go);
        var element = go.GetComponent<KitchenElement>();
        Assert.IsNotNull(element, "фабрика обязана вернуть элемент");
        Assert.IsInstanceOf<IKeepsPlacementHeight>(element,
            "тип, который заводят на высоте, обязан нести IKeepsPlacementHeight — иначе "
            + "PlacementController уронит его на пол в первом же кадре на курсоре");

        _placement.Begin(element!);

        Assert.AreEqual(centreUnits, go.transform.position.y, Tol, why);
        Assert.Greater(go.transform.position.y,
            element!.DimensionsMM.y * 0.5f * AppConstants.MM_TO_UNITS,
            "и высота эта именно ЗАДАННАЯ, а не половина габарита: совпади они — "
            + "проверка выше прошла бы и на сломанном коде");
    }
}
