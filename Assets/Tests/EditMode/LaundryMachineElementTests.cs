using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;
using KitchenDesigner.Core.UI;

/// <summary>Стиральная и сушильная машина — ОДИН класс с двумя названиями
/// (<see cref="LaundryMachineKind"/>), тот же приём, что у ящика Movento. Здесь
/// спрашивается ровно то, что могло бы разъехаться: что реализация правда одна,
/// что размеры не заводские, а обычные редактируемые, что дверца открывается и
/// закрывается, и что круг «сохранить → открыть» не теряет ни вид, ни габарит,
/// ни состояние дверцы.
///
/// Пара ПРОТИВОПОЛОЖНЫХ входов держит два разных вопроса
/// (`agents/TEST-DESIGN.md`): «донесли ли значение» спрашивается НЕзаводскими
/// числами, «построился ли элемент вообще» — только заводскими, потому что
/// сеттер, получивший то, что уже лежит в поле, не трогает ничего.</summary>
public class LaundryMachineElementTests
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

        foreach (var e in Object.FindObjectsByType<KitchenElement>())
            if (e != null) Object.DestroyImmediate(e.gameObject);

        PartRegistry.Clear();
        GroupManager.Clear();
        CommandStack.Clear();
        ElementFactory.ClearPools();
        MaterialManager.ClearCache();
    }

    private LaundryMachineElement Make(LaundryMachineKind kind, Vector3Int dims, string name)
    {
        var go = ElementFactory.CreateLaundryMachine(kind, dims, name, Vector3.zero);
        _spawned.Add(go);
        var el = go.GetComponent<LaundryMachineElement>();
        Assert.IsNotNull(el, "фабрика обязана вернуть объект со стиральной машиной");
        return el!;
    }

    private LaundryMachineElement MakeDefault(LaundryMachineKind kind, string name) =>
        Make(kind, LaundryMachineBody.DefaultDimensionsMM, name);

    private static Transform DoorOf(LaundryMachineElement machine) =>
        machine.transform.GetChild(LaundryMachineBody.IdxHatchRim);

    [Test]
    public void TheWasherAndTheDryer_AreTheSameClass_WithDifferentNames()
    {
        var washer = MakeDefault(LaundryMachineKind.Washer, "Washer");
        var dryer = MakeDefault(LaundryMachineKind.Dryer, "Dryer");

        Assert.AreEqual(washer.GetType(), dryer.GetType(),
            "второй класс-копия — это ровно то, что было запрещено: реализация одна");
        Assert.AreNotEqual(washer.DisplayTypeName, dryer.DisplayTypeName,
            "названия обязаны различаться, иначе два вида неразличимы для человека");
        Assert.AreEqual(LaundryMachineBody.WasherName, washer.DisplayTypeName);
        Assert.AreEqual(LaundryMachineBody.DryerName, dryer.DisplayTypeName);
    }

    [Test]
    public void ChangingTheKind_RenamesIt_AndRebuildsOnlyTheHatch()
    {
        var machine = Make(LaundryMachineKind.Washer, new Vector3Int(607, 853, 563), "Kind");
        var dimsBefore = machine.DimensionsMM;
        var shellBefore = machine.transform.GetChild(LaundryMachineBody.IdxShell).localPosition;
        float hatchBefore = HatchWidthInScene(machine);

        machine.Kind = LaundryMachineKind.Dryer;

        Assert.AreEqual(LaundryMachineBody.DryerName, machine.DisplayTypeName);
        Assert.AreEqual(dimsBefore, machine.DimensionsMM,
            "смена вида меняет подпись, а не габарит — иначе пользователь потеряет размеры");
        Assert.AreEqual(shellBefore, machine.transform.GetChild(LaundryMachineBody.IdxShell)
            .localPosition, "корпус у двух машин общий и с места не двигается");
        Assert.Greater(HatchWidthInScene(machine), hatchBefore + 0.01f,
            "у сушильной люк шире, и это обязано доехать до СЦЕНЫ: неизменившийся обод "
            + "значит, что смена вида не пересобрала геометрию");
    }

    [Test]
    public void ChangingTheKindBack_ReturnsTheHatch_SoTheKindIsNotAOneWayDoor()
    {
        var machine = MakeDefault(LaundryMachineKind.Washer, "KindBack");
        float washer = HatchWidthInScene(machine);

        machine.Kind = LaundryMachineKind.Dryer;
        machine.Kind = LaundryMachineKind.Washer;

        Assert.AreEqual(washer, HatchWidthInScene(machine), 0.001f,
            "противоположный вход: обратная смена вида обязана вернуть прежний люк, "
            + "иначе отмена покажет не то, что было");
    }

    private static float HatchWidthInScene(LaundryMachineElement machine) =>
        machine.transform.GetChild(LaundryMachineBody.IdxHatchRim).localScale.x;

    [Test]
    public void TheKindProperty_IsUndoable_SoTheDropdownGetsUndoForFree()
    {
        var marked = UndoableProperties.For(typeof(LaundryMachineElement))
            .Select(p => p.Name).ToList();

        CollectionAssert.Contains(marked, nameof(LaundryMachineElement.Kind),
            "без [Undoable] смена вида не попадёт в стек отмены, а панель на это рассчитывает");
    }

    [Test]
    public void ANewMachine_AtItsDefaultSize_IsFullyBuilt()
    {
        foreach (var kind in new[] { LaundryMachineKind.Washer, LaundryMachineKind.Dryer })
        {
            var machine = MakeDefault(kind, "Built_" + kind);

            Assert.AreEqual(LaundryMachineBody.PartCount, machine.transform.childCount,
                kind + ": деталей меньше, чем должно быть — ApplyDimensions не звали ни разу, "
                + "и элемент вышел из фабрики пустым объектом");

            for (int i = 0; i < LaundryMachineBody.PartCount; i++)
            {
                var child = machine.transform.GetChild(i);
                var filter = child.GetComponent<MeshFilter>();
                Assert.IsNotNull(filter, kind + ": деталь " + child.name + " без MeshFilter");
                Assert.IsNotNull(filter!.sharedMesh, kind + ": деталь " + child.name + " без меша");
                Assert.IsNotNull(child.GetComponent<MeshRenderer>(),
                    kind + ": деталь " + child.name + " невидима — красить нечем");
                Assert.Greater(child.localScale.sqrMagnitude, 0f,
                    kind + ": деталь " + child.name + " нулевого размера");
            }

            var box = machine.GetComponent<BoxCollider>();
            Assert.IsNotNull(box, kind + ": нет коллайдера — элемент нельзя выделить мышью");
            Assert.Greater(box!.size.sqrMagnitude, 0f,
                kind + ": коллайдер нулевого размера мышью не поймать");

            Assert.AreEqual(LaundryMachineBody.DefaultDimensionsMM, machine.DimensionsMM,
                kind + ": размеры по умолчанию — 600 на 850 на 600 мм");
        }
    }

    /// <summary>Счётчик «в группе четыре пункта» у духовки и посудомойки вырос до шести — и
    /// это не подгонка числа, а ровно то, что здесь записано: машины дописаны В КОНЕЦ
    /// «Техники», поэтому места духовки (третье) и посудомойки (четвёртое) не сдвинулись.
    /// Утверждение о хвосте живёт тут, у своего предмета, а не в чужих файлах.</summary>
    [Test]
    public void TheCatalogue_PutsBothMachines_AtTheTailOfTheApplianceGroup()
    {
        var group = SidebarCatalog.Build().Find(g => g.title == "Техника");

        Assert.AreEqual(SidebarItemKind.Dishwasher, group.items[3].kind,
            "посудомойка обязана остаться четвёртой: машины дописываются ПОСЛЕ неё, "
            + "а не втискиваются в середину группы");

        var washer = group.items[4];
        var dryer = group.items[5];

        Assert.AreEqual(SidebarItemKind.WashingMachine, washer.kind);
        Assert.AreEqual(SidebarItemKind.Dryer, dryer.kind,
            "плитки сайдбара группируются ПО SidebarItemKind: общий вид склеил бы две "
            + "машины в одну плитку, и подпись у неё осталась бы от первой строки");
        Assert.AreEqual(LaundryMachineBody.WASHER_TYPE_ID, washer.preset.laundryKind);
        Assert.AreEqual(LaundryMachineBody.DRYER_TYPE_ID, dryer.preset.laundryKind,
            "второй пункт обязан нести ДРУГОЙ вид — одинаковый пресет дал бы две "
            + "кнопки, заводящие одну и ту же стиральную машину");
        Assert.AreEqual(LaundryMachineBody.DefaultDimensionsMM, washer.dims,
            "в каталоге стоят размеры по умолчанию: 600 на 850 на 600 мм");
        Assert.AreEqual(washer.dims, dryer.dims,
            "корпус у машин общий, значит и размеры в каталоге у них одни");
    }

    /// <summary>Дефект, который увидел пользователь: обе кнопки были подписаны «Стиральная».
    /// Причина не в подписях, а в том, что <c>SidebarTileBuilder</c> собирает плитку по
    /// <c>SidebarItemKind</c> — общий вид склеивал две строки в ОДНУ плитку, а подпись
    /// плитки берётся от первой из них, так что сушильной в интерфейсе не было вовсе,
    /// хотя пресеты уже были разные и объекты создавались верные. Поэтому вопрос задаётся
    /// не пресетам, а ПРОДУКТУ — тому самому набору плиток, который строит сайдбар, и
    /// именно той строке, которую он покажет на кнопке.</summary>
    [Test]
    public void TheTwoMachines_AreTwoTiles_WithDIFFERENTCaptions()
    {
        var group = SidebarCatalog.Build().Find(g => g.title == "Техника");
        var tiles = SidebarTileBuilder.BuildTiles(group.items);

        var machineTiles = tiles.FindAll(t => t.presets.Count > 0
            && (t.presets[0].kind == SidebarItemKind.WashingMachine
                || t.presets[0].kind == SidebarItemKind.Dryer));

        Assert.AreEqual(2, machineTiles.Count,
            "две машины — две плитки. Одна плитка значит, что они склеились и вторая "
            + "спряталась в переключатель пресетов под чужой подписью");

        var captions = machineTiles.ConvertAll(CaptionOf);
        Assert.AreNotEqual(captions[0], captions[1],
            "обе кнопки подписаны одинаково — ровно то, что увидел пользователь: "
            + "подписи «" + captions[0] + "» и «" + captions[1] + "»");
        CollectionAssert.Contains(captions, LaundryMachineBody.WasherName);
        CollectionAssert.Contains(captions, LaundryMachineBody.DryerName,
            "сушильной машины в каталоге не видно ни под каким именем");
    }

    /// <summary>Та же строка, что рисует <c>SidebarUI</c> на кнопке плитки: у плитки с
    /// одним пресетом это имя пресета, у плитки с несколькими — заголовок плитки.</summary>
    private static string CaptionOf(SidebarTileBuilder.Tile tile) =>
        tile.presets.Count > 1 ? tile.title : tile.presets[0].DisplayName;

    [Test]
    public void TheHatch_ReachesTheSceneAsADisc_NotAsACube()
    {
        var machine = MakeDefault(LaundryMachineKind.Washer, "Disc");

        var rim = machine.transform.GetChild(LaundryMachineBody.IdxHatchRim)
            .GetComponent<MeshFilter>();
        var panel = machine.transform.GetChild(LaundryMachineBody.IdxControlPanel)
            .GetComponent<MeshFilter>();

        Assert.IsNotNull(rim, "у обода люка нет MeshFilter");
        Assert.IsNotNull(panel, "у панели управления нет MeshFilter");
        Assert.Greater(rim!.sharedMesh.vertexCount, panel!.sharedMesh.vertexCount,
            "круглый люк не может быть кубом: у диска вершин заметно больше, чем у коробки. "
            + "Равное число значит, что диск не поставили и люк остался прямоугольным");
        Assert.AreNotSame(panel.sharedMesh, rim.sharedMesh,
            "обод и панель делят один меш — значит подмена меша не сработала");
    }

    [Test]
    public void TheShell_IsBoredForTheDrum_NotLeftAPlainCube()
    {
        var machine = MakeDefault(LaundryMachineKind.Washer, "Drum");

        var shell = machine.transform.GetChild(LaundryMachineBody.IdxShell)
            .GetComponent<MeshFilter>();
        Assert.IsNotNull(shell, "у корпуса нет MeshFilter");

        var mesh = shell!.sharedMesh;
        Assert.IsNotNull(mesh, "у корпуса нет меша");
        Assert.Greater(mesh!.vertexCount, 24,
            "у куба 24 вершины; корпус с расточкой под барабан заведомо богаче — "
            + "столько же значит, что дверцу открыли, а за ней плоская стенка");

        Assert.AreEqual(Vector3.one, machine.transform.GetChild(LaundryMachineBody.IdxShell).localScale,
            "корпус собран в настоящих миллиметрах, поэтому масштаб на нём обязан быть "
            + "единичным: иначе круглая расточка растянулась бы в овал");

        var size = mesh.bounds.size / AppConstants.MM_TO_UNITS;
        var expected = LaundryMachineBody.ShellSizeMM(LaundryMachineBody.DefaultDimensionsMM);
        Assert.AreEqual(expected.x, size.x, 0.5f, "меш корпуса шире или уже объявленного");
        Assert.AreEqual(expected.y, size.y, 0.5f, "меш корпуса выше или ниже объявленного");
        Assert.AreEqual(expected.z, size.z, 0.5f, "меш корпуса глубже или мельче объявленного");
    }

    [Test]
    public void EveryTriangleOfTheShell_IsWoundTheWayItsNormalPoints()
    {
        var machine = MakeDefault(LaundryMachineKind.Dryer, "Winding");
        var mesh = machine.transform.GetChild(LaundryMachineBody.IdxShell)
            .GetComponent<MeshFilter>().sharedMesh;

        var vertices = mesh.vertices;
        var normals = mesh.normals;
        var triangles = mesh.triangles;

        int wrong = 0;
        int degenerate = 0;
        for (int i = 0; i < triangles.Length; i += 3)
        {
            var a = vertices[triangles[i]];
            var cross = Vector3.Cross(vertices[triangles[i + 1]] - a, vertices[triangles[i + 2]] - a);
            if (cross.sqrMagnitude < 1e-12f) { degenerate++; continue; }

            var declared = normals[triangles[i]] + normals[triangles[i + 1]]
                + normals[triangles[i + 2]];
            if (Vector3.Dot(cross.normalized, declared.normalized) <= 0f) wrong++;
        }

        Assert.AreEqual(0, wrong,
            "треугольник намотан против собственной нормали: отсечение задних граней съест его, "
            + "и сквозь корпус будет видно насквозь. Ни коробка, ни счёт вершин этого не ловят. "
            + "Вывернутых треугольников: " + wrong);
        Assert.AreEqual(0, degenerate,
            "вырожденный треугольник нулевой площади: у кольца совпали два угла. Таких: "
            + degenerate);
    }

    [Test]
    public void TheRootKeepsUnitScale_SoTheChildrenAreNotScaledTwice()
    {
        var machine = Make(LaundryMachineKind.Washer, new Vector3Int(700, 900, 550), "Scale");

        Assert.AreEqual(Vector3.one, machine.transform.localScale,
            "детали заданы в абсолютных миллиметрах — масштаб на корне умножил бы их вторично");

        var box = machine.GetComponent<BoxCollider>();
        Assert.IsNotNull(box, "коллайдер обязан быть");
        Assert.AreEqual(0.7f, box!.size.x, 0.001f,
            "коллайдер обязан быть габаритом элемента, а не единичным кубом");
    }

    [Test]
    public void Resizing_RebuildsTheBody_OnEveryAxis()
    {
        var machine = MakeDefault(LaundryMachineKind.Washer, "Resize");
        var shell = machine.transform.GetChild(LaundryMachineBody.IdxShell)
            .GetComponent<MeshFilter>();

        machine.DimensionsMM = new Vector3Int(900, 1100, 700);

        var size = shell.sharedMesh.bounds.size / AppConstants.MM_TO_UNITS;
        Assert.AreEqual(900f, size.x, 0.5f, "корпус не поехал за шириной");
        Assert.AreEqual(1100f, size.y, 0.5f, "корпус не поехал за высотой");
        Assert.AreEqual(700f - LaundryMachineBody.FRONT_FACE_SETBACK_MM, size.z, 0.5f,
            "корпус не поехал за глубиной");
        Assert.AreEqual(new Vector3Int(900, 1100, 700), machine.DimensionsMM);
    }

    [Test]
    public void Resizing_MovesTheHinge_SoTheHatchStillTurnsAroundItself()
    {
        float narrow = OpenHatchReach(new Vector3Int(600, 850, 600), "HingeNarrow");
        float wide = OpenHatchReach(new Vector3Int(900, 1100, 700), "HingeWide");

        Assert.Greater(wide, narrow + 0.05f,
            "у широкой машины люк больше, значит и вылет открытого люка больше; "
            + "одинаковый вылет означал бы, что петля осталась на месте от прежнего размера");
    }

    private float OpenHatchReach(Vector3Int dims, string name)
    {
        var machine = Make(LaundryMachineKind.Washer, dims, name);
        var hinge = LaundryMachineBody.HingeLocalMM(LaundryMachineKind.Washer, dims)
            * AppConstants.MM_TO_UNITS;
        machine.SetOpen(true);
        machine.StepDoor(DropDoor.OPEN_SECONDS);
        return DoorOf(machine).localPosition.z - hinge.z;
    }

    [Test]
    public void TheDoor_SwingsForwardOnAVerticalAxis_AndComesBack()
    {
        var machine = MakeDefault(LaundryMachineKind.Washer, "Door");
        var door = DoorOf(machine);
        var closed = door.localPosition;

        Assert.IsFalse(machine.IsOpen, "новая машина стоит закрытой");
        Assert.AreEqual(0f, machine.DoorProgress, 0.001f);

        machine.SetOpen(true);
        machine.StepDoor(DropDoor.OPEN_SECONDS);

        Assert.IsTrue(machine.IsOpen);
        Assert.AreEqual(1f, machine.DoorProgress, 0.001f, "дверца обязана раскрыться до конца");
        Assert.Greater(door.localPosition.z, closed.z + 0.1f,
            "открытая дверца выходит ВПЕРЁД — иначе она уехала в корпус");
        Assert.AreEqual(DropDoor.OPEN_ANGLE_DEG,
            Quaternion.Angle(door.localRotation, Quaternion.identity), 0.5f,
            "дверца раскрывается на прямой угол");
        Assert.AreEqual(closed.y, door.localPosition.y, 0.001f,
            "ось вращения вертикальна: дверца не поднимается и не опускается");

        machine.SetOpen(false);
        machine.StepDoor(DropDoor.OPEN_SECONDS);

        Assert.IsFalse(machine.IsOpen);
        Assert.AreEqual(closed.x, door.localPosition.x, 0.001f,
            "закрытая дверца обязана вернуться ровно туда, откуда ушла");
        Assert.AreEqual(closed.z, door.localPosition.z, 0.001f,
            "закрытая дверца обязана вернуться ровно туда, откуда ушла");
    }

    [Test]
    public void ToggleAndForceClose_AreOppositeInputs()
    {
        var machine = MakeDefault(LaundryMachineKind.Dryer, "Toggle");

        machine.ToggleOpen();
        Assert.IsTrue(machine.IsOpen, "первое нажатие открывает");
        machine.StepDoor(DropDoor.OPEN_SECONDS);

        machine.ToggleOpen();
        Assert.IsFalse(machine.IsOpen, "второе нажатие закрывает");

        machine.SetOpen(true);
        machine.StepDoor(DropDoor.OPEN_SECONDS);
        machine.ForceClose();

        Assert.IsFalse(machine.IsOpen, "ForceClose гасит и флаг");
        Assert.AreEqual(0f, machine.DoorProgress, 0.001f,
            "ForceClose гасит и анимацию — иначе дверца замрёт открытой без флага");
    }

    [Test]
    public void TheOpenActionLabel_TellsTheTwoStatesApart()
    {
        var machine = MakeDefault(LaundryMachineKind.Washer, "Label");

        var closedLabel = machine.OpenActionLabel;
        machine.SetOpen(true);
        var openLabel = machine.OpenActionLabel;

        Assert.AreNotEqual(closedLabel, openLabel,
            "кнопка панели читает эту подпись; одинаковая в обоих состояниях она бесполезна");
    }

    [Test]
    public void TheSpecification_CountsItAsOnePiece_UnderItsOwnName()
    {
        foreach (var kind in new[] { LaundryMachineKind.Washer, LaundryMachineKind.Dryer })
        {
            var machine = MakeDefault(kind, "Spec_" + kind);
            var items = machine.GetSpecItems(new List<KitchenElement> { machine }).ToList();

            Assert.AreEqual(1, items.Count, kind + ": покупное изделие — ровно одна строка");
            StringAssert.Contains(LaundryMachineBody.NameOf(kind), items[0].name,
                kind + ": строка ведомости обязана называть вид машины");
            Assert.AreEqual(SpecUnit.Pieces, items[0].unit,
                kind + ": машина покупается штукой, а не листом и не метром");
            Assert.AreEqual(1f, items[0].qty, 0.001f, kind + ": одна машина — одна штука");
        }
    }

    [Test]
    public void TheWholeState_SurvivesSaveAndLoad()
    {
        var dims = new Vector3Int(607, 853, 563);
        var machine = Make(LaundryMachineKind.Dryer, dims, "Laundry_RT");
        machine.transform.position = new Vector3(1f, 2f, 3f);
        machine.SetOpen(true);

        var restored = RoundTrip();

        Assert.AreEqual(LaundryMachineKind.Dryer, restored.Kind,
            "вид не пережил круг — открытый проект показал бы стиральную вместо сушильной");
        Assert.AreEqual(dims, restored.DimensionsMM, "габарит не пережил круг");
        Assert.IsTrue(restored.IsOpen, "состояние дверцы не пережило круг");
        Assert.AreEqual("Laundry_RT", restored.PartName);
        Assert.AreEqual(1f, restored.transform.position.x, 0.001f);
    }

    [Test]
    public void AClosedWasher_AlsoSurvivesTheCircle()
    {
        var machine = Make(LaundryMachineKind.Washer, new Vector3Int(613, 857, 571), "Laundry_RT2");
        Assert.IsFalse(machine.IsOpen, "исходная машина закрыта");

        var restored = RoundTrip();

        Assert.AreEqual(LaundryMachineKind.Washer, restored.Kind,
            "противоположный вход: у сушильной обратное значение, и оба обязаны доехать");
        Assert.IsFalse(restored.IsOpen, "закрытая обязана открыться закрытой");
        Assert.AreEqual(new Vector3Int(613, 857, 571), restored.DimensionsMM);
    }

    private LaundryMachineElement RoundTrip()
    {
        var path = Path.Combine(Application.temporaryCachePath,
            "rt_laundry_" + System.Guid.NewGuid().ToString("N") + ".json");

        var elements = new List<KitchenElement>();
        foreach (var go in _spawned)
        {
            if (go == null) continue;
            var el = go.GetComponent<KitchenElement>();
            if (el != null) elements.Add(el);
        }

        SaveLoadManager.SaveToFile(path, SaveLoadManager.CaptureScene(elements));

        foreach (var go in _spawned)
            if (go != null) Object.DestroyImmediate(go);
        _spawned.Clear();
        foreach (var e in Object.FindObjectsByType<KitchenElement>())
            if (e != null) Object.DestroyImmediate(e.gameObject);
        PartRegistry.Clear();
        GroupManager.Clear();
        CommandStack.Clear();

        var loaded = SaveLoadManager.LoadFromFile(path);
        Assert.IsNotNull(loaded, "файл проекта не прочитался");
        SaveLoadManager.RestoreScene(loaded!);
        File.Delete(path);

        var machines = Object.FindObjectsByType<LaundryMachineElement>();
        Assert.AreEqual(1, machines.Length,
            "после загрузки в сцене обязана быть ровно одна машина — "
            + "ноль значит, что тип не восстанавливается вообще");
        return machines[0];
    }
}
