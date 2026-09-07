using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

public class ConstraintValidatorTests
{
    private KitchenElement CreateElement(string name, Vector3Int dims, Vector3 position)
    {
        var go = new GameObject(name);
        go.transform.position = position;
        var element = go.AddComponent<KitchenElement>();
        element.PartName = name;
        element.DimensionsMM = dims;
        return element;
    }

    [Test]
    public void Validate_TwoBoardsFaceToFace_IsValid()
    {
        var a = CreateElement("A", new Vector3Int(800, 400, 18), Vector3.zero);
        var b = CreateElement("B", new Vector3Int(800, 400, 18), new Vector3(0.8f, 0, 0));

        var result = ConstraintValidator.Validate(new List<KitchenElement> { a, b });

        Assert.IsTrue(result.isValid);
        Assert.AreEqual(1, result.contacts.Count);
        Assert.IsTrue(result.contacts[0].isFaceToFace);

        Object.DestroyImmediate(a.gameObject);
        Object.DestroyImmediate(b.gameObject);
    }

    [Test]
    public void Validate_SingleBoardInAir_Violation()
    {
        var a = CreateElement("A", new Vector3Int(800, 400, 18), Vector3.zero);

        var result = ConstraintValidator.Validate(new List<KitchenElement> { a });

        Assert.IsFalse(result.isValid);
        Assert.AreEqual(1, result.violations.Count);

        Object.DestroyImmediate(a.gameObject);
    }

    [Test]
    public void Validate_BoardOnFloor_IsValid()
    {
        // Пол: верхняя грань на y=0. деталь 400мм высотой стоит на полу → центр y=0.2.
        var floor = CreateElement("Floor", new Vector3Int(3000, 18, 3000), new Vector3(0, -0.009f, 0));
        var board = CreateElement("Board", new Vector3Int(800, 400, 18), new Vector3(0, 0.2f, 0));

        var result = ConstraintValidator.Validate(new List<KitchenElement> { floor, board });

        Assert.IsTrue(result.isValid);
        Assert.AreEqual(1, result.contacts.Count);
        Assert.IsTrue(result.contacts[0].isFaceToFace);

        Object.DestroyImmediate(floor.gameObject);
        Object.DestroyImmediate(board.gameObject);
    }

    [Test]
    public void Validate_BoardTouchingByEdge_Violation()
    {
        // A стоит на полу (валидна). B касается A только ребром (грани совпадают лишь
        // по линии y=0.4), поэтому face-to-face контакта нет и B — нарушение.
        var floor = CreateElement("Floor", new Vector3Int(3000, 18, 3000), new Vector3(0, -0.009f, 0));
        var a = CreateElement("A", new Vector3Int(800, 400, 18), new Vector3(0, 0.2f, 0));
        var b = CreateElement("B", new Vector3Int(800, 400, 18), new Vector3(0.8f, 0.6f, 0));

        var result = ConstraintValidator.Validate(new List<KitchenElement> { floor, a, b });

        Assert.IsFalse(result.isValid);
        Assert.AreEqual(1, result.violations.Count);
        Assert.AreEqual(b, result.violations[0]);

        Object.DestroyImmediate(floor.gameObject);
        Object.DestroyImmediate(a.gameObject);
        Object.DestroyImmediate(b.gameObject);
    }

    [Test]
    public void Validate_ChainABC_IsValid()
    {
        var a = CreateElement("A", new Vector3Int(800, 400, 18), Vector3.zero);
        var b = CreateElement("B", new Vector3Int(800, 400, 18), new Vector3(0.8f, 0, 0));
        var c = CreateElement("C", new Vector3Int(800, 400, 18), new Vector3(1.6f, 0, 0));

        var result = ConstraintValidator.Validate(new List<KitchenElement> { a, b, c });

        Assert.IsTrue(result.isValid);
        Assert.AreEqual(2, result.contacts.Count);

        Object.DestroyImmediate(a.gameObject);
        Object.DestroyImmediate(b.gameObject);
        Object.DestroyImmediate(c.gameObject);
    }

    [Test]
    public void Validate_TwoIsolatedGroups_Violation()
    {
        // Якорь связности — BasePlate. Две пары деталей висят в воздухе, не касаясь пола:
        // каждая пара связна внутри себя, но изолирована от плиты → 2 группы, 4 нарушения.
        var plate = CreateElement("BasePlate", new Vector3Int(3000, 18, 3000), new Vector3(0, -0.009f, 0));
        plate.gameObject.AddComponent<BasePlate>();

        var a = CreateElement("A", new Vector3Int(800, 400, 18), new Vector3(0, 1.0f, 0));
        var b = CreateElement("B", new Vector3Int(800, 400, 18), new Vector3(0.8f, 1.0f, 0));
        var c = CreateElement("C", new Vector3Int(800, 400, 18), new Vector3(0, 3.0f, 0));
        var d = CreateElement("D", new Vector3Int(800, 400, 18), new Vector3(0.8f, 3.0f, 0));

        var result = ConstraintValidator.Validate(new List<KitchenElement> { plate, a, b, c, d });

        Assert.IsFalse(result.isValid);
        Assert.AreEqual(2, result.isolatedGroups.Count);
        Assert.AreEqual(4, result.violations.Count);

        Object.DestroyImmediate(plate.gameObject);
        Object.DestroyImmediate(a.gameObject);
        Object.DestroyImmediate(b.gameObject);
        Object.DestroyImmediate(c.gameObject);
        Object.DestroyImmediate(d.gameObject);
    }

    [Test]
    public void Validate_PartialOverlapBelow50Percent_NotFaceToFace()
    {
        // B лежит на A, но грани перекрываются лишь на 25% → контакт есть, но не
        // face-to-face, поэтому связности нет и обе детали — нарушения.
        var a = CreateElement("A", new Vector3Int(800, 400, 18), Vector3.zero);
        var b = CreateElement("B", new Vector3Int(800, 400, 18), new Vector3(0.6f, 0.4f, 0));

        var result = ConstraintValidator.Validate(new List<KitchenElement> { a, b });

        Assert.AreEqual(1, result.contacts.Count);
        Assert.IsFalse(result.contacts[0].isFaceToFace);
        Assert.IsFalse(result.isValid);
        Assert.AreEqual(2, result.violations.Count);

        Object.DestroyImmediate(a.gameObject);
        Object.DestroyImmediate(b.gameObject);
    }

    [Test]
    public void Validate_IntersectingBoards_NoContact()
    {
        // Пересекающиеся детали не образуют контакта (пара пропускается).
        var a = CreateElement("A", new Vector3Int(800, 400, 18), Vector3.zero);
        var b = CreateElement("B", new Vector3Int(800, 400, 18), new Vector3(0.1f, 0, 0));

        var result = ConstraintValidator.Validate(new List<KitchenElement> { a, b });

        Assert.AreEqual(0, result.contacts.Count);
        Assert.IsFalse(result.isValid);

        Object.DestroyImmediate(a.gameObject);
        Object.DestroyImmediate(b.gameObject);
    }

    [Test]
    public void Validate_EmptyList_IsValid()
    {
        var result = ConstraintValidator.Validate(new List<KitchenElement>());
        Assert.IsTrue(result.isValid);
    }

    // Broad-phase сетка не должна терять пары: далеко стоящая изолированная деталь
    // обязана остаться нарушением, а близкая цепочка — дать ровно 2 контакта.
    // Сцена сознательно раскидана по X, чтобы элементы попали в разные ячейки.
    [Test]
    public void Validate_NearChainAndFarIsolated_GridKeepsPairs()
    {
        var floor = CreateElement("Floor", new Vector3Int(3000, 18, 3000), new Vector3(0, -0.009f, 0));
        var a = CreateElement("A", new Vector3Int(800, 400, 18), new Vector3(0, 0.2f, 0));
        var b = CreateElement("B", new Vector3Int(800, 400, 18), new Vector3(0.8f, 0.2f, 0));
        // D висит в воздухе далеко (50м) — в отдельной ячейке, не касается никого.
        var d = CreateElement("D", new Vector3Int(800, 400, 18), new Vector3(50f, 5f, 0));

        var result = ConstraintValidator.Validate(new List<KitchenElement> { floor, a, b, d });

        Assert.IsFalse(result.isValid);
        // floor-A, floor-B (B тоже стоит на полу) и A-B — три face-контакта.
        Assert.AreEqual(3, result.contacts.Count, "floor-A, floor-B, A-B должны дать 3 контакта");
        Assert.AreEqual(1, result.violations.Count, "только D — нарушение");
        Assert.AreEqual(d, result.violations[0]);
        Assert.AreEqual(1, result.isolatedGroups.Count);

        Object.DestroyImmediate(floor.gameObject);
        Object.DestroyImmediate(a.gameObject);
        Object.DestroyImmediate(b.gameObject);
        Object.DestroyImmediate(d.gameObject);
    }

    // Пересечение вдали от начала координат: проверяет, что сетка корректно
    // работает с большими/отрицательными индексами ячеек и не пропускает пару.
    [Test]
    public void Validate_IntersectingFarFromOrigin_StillDetected()
    {
        var a = CreateElement("A", new Vector3Int(800, 400, 18), new Vector3(100f, -30f, 7f));
        var b = CreateElement("B", new Vector3Int(800, 400, 18), new Vector3(100.1f, -30f, 7f));

        var result = ConstraintValidator.Validate(new List<KitchenElement> { a, b });

        Assert.AreEqual(0, result.contacts.Count, "пересекающиеся детали не дают контакт");
        Assert.IsFalse(result.isValid);
        Assert.AreEqual(2, result.violations.Count);

        Object.DestroyImmediate(a.gameObject);
        Object.DestroyImmediate(b.gameObject);
    }

    [Test]
    public void Validate_WindowTooTall_Violation()
    {
        var wallGo = ElementFactory.CreateWall(
            new Vector3Int(100, 2500, 3000), "Wall_WinH", new Vector3(0, 1.25f, 0));
        var winGo = ElementFactory.CreateWindow(
            new Vector3Int(900, 1200, 100), "Win_Tall", new Vector3(0, 1.25f, 0));
        var wall = wallGo.GetComponent<KitchenElement>();
        var window = winGo.GetComponent<WindowElement>();

        window!.SnapToWall();
        // Устанавливаем высоту больше стены — сохраняем z от снапа (толщина стены).
        window.DimensionsMM = new Vector3Int(900, 3000, window.DimensionsMM.z);

        var result = ConstraintValidator.Validate(new List<KitchenElement> { wall, window! });

        Assert.IsFalse(result.isValid);
        Assert.Contains(window, result.violations);

        Object.DestroyImmediate(wallGo);
        Object.DestroyImmediate(winGo);
    }

    [Test]
    public void Validate_WindowAboveWallTop_Violation()
    {
        var wallGo = ElementFactory.CreateWall(
            new Vector3Int(100, 2500, 3000), "Wall_WinY", new Vector3(0, 1.25f, 0));
        var winGo = ElementFactory.CreateWindow(
            new Vector3Int(900, 500, 100), "Win_Above", new Vector3(0, 1.25f, 0));
        var wall = wallGo.GetComponent<KitchenElement>();
        var window = winGo.GetComponent<WindowElement>();

        window!.SnapToWall();
        winGo.transform.position = new Vector3(0, 2.8f, 0);

        var result = ConstraintValidator.Validate(new List<KitchenElement> { wall, window! });

        Assert.IsFalse(result.isValid);
        Assert.Contains(window, result.violations);

        Object.DestroyImmediate(wallGo);
        Object.DestroyImmediate(winGo);
    }

    [Test]
    public void Validate_WindowWithinWall_NoHeightViolation()
    {
        var wallGo = ElementFactory.CreateWall(
            new Vector3Int(100, 2500, 3000), "Wall_WinOK", new Vector3(0, 1.25f, 0));
        var winGo = ElementFactory.CreateWindow(
            new Vector3Int(900, 1200, 100), "Win_OK2", new Vector3(0, 1.0f, 0));
        var wall = wallGo.GetComponent<KitchenElement>();
        var window = winGo.GetComponent<WindowElement>();

        window!.SnapToWall();

        var result = ConstraintValidator.Validate(new List<KitchenElement> { wall, window! });

        Assert.IsTrue(result.isValid,
            "окно в пределах стены не должно давать нарушений по высоте");

        Object.DestroyImmediate(wallGo);
        Object.DestroyImmediate(winGo);
    }

    [Test]
    public void Validate_DoorTooTall_Violation()
    {
        var wallGo = ElementFactory.CreateWall(
            new Vector3Int(100, 2500, 3000), "Wall_DoorH2", new Vector3(0, 1.25f, 0));
        var doorGo = ElementFactory.CreateDoor(
            new Vector3Int(900, 2000, 100), "Door_Tall", new Vector3(0, 1.25f, 0));
        var wall = wallGo.GetComponent<KitchenElement>();
        var door = doorGo.GetComponent<DoorElement>();

        door!.SnapToWall();
        door.DimensionsMM = new Vector3Int(900, 3000, door.DimensionsMM.z);

        var result = ConstraintValidator.Validate(new List<KitchenElement> { wall, door! });

        Assert.IsFalse(result.isValid);
        Assert.Contains(door, result.violations);

        Object.DestroyImmediate(wallGo);
        Object.DestroyImmediate(doorGo);
    }

    [Test]
    public void Validate_DoorBelowWallBottom_Violation()
    {
        var wallGo = ElementFactory.CreateWall(
            new Vector3Int(100, 2500, 3000), "Wall_DoorY", new Vector3(0, 1.25f, 0));
        var doorGo = ElementFactory.CreateDoor(
            new Vector3Int(900, 500, 100), "Door_Below", new Vector3(0, 1.25f, 0));
        var wall = wallGo.GetComponent<KitchenElement>();
        var door = doorGo.GetComponent<DoorElement>();

        door!.SnapToWall();
        doorGo.transform.position = new Vector3(0, -0.1f, 0);

        var result = ConstraintValidator.Validate(new List<KitchenElement> { wall, door! });

        Assert.IsFalse(result.isValid);
        Assert.Contains(door, result.violations);

        Object.DestroyImmediate(wallGo);
        Object.DestroyImmediate(doorGo);
    }

    [Test]
    public void Validate_DoorWithinWall_NoHeightViolation()
    {
        var wallGo = ElementFactory.CreateWall(
            new Vector3Int(100, 2500, 3000), "Wall_DoorOK2", new Vector3(0, 1.25f, 0));
        var doorGo = ElementFactory.CreateDoor(
            new Vector3Int(900, 2000, 100), "Door_OK2", new Vector3(0, 1.0f, 0));
        var wall = wallGo.GetComponent<KitchenElement>();
        var door = doorGo.GetComponent<DoorElement>();

        door!.SnapToWall();

        var result = ConstraintValidator.Validate(new List<KitchenElement> { wall, door! });

        Assert.IsTrue(result.isValid,
            "дверь в пределах стены не должна давать нарушений по высоте");

        Object.DestroyImmediate(wallGo);
        Object.DestroyImmediate(doorGo);
    }

    [Test]
    public void Validate_WindowBelowWallBottom_Violation()
    {
        var wallGo = ElementFactory.CreateWall(
            new Vector3Int(100, 2500, 3000), "Wall_WinLow", new Vector3(0, 1.25f, 0));
        var winGo = ElementFactory.CreateWindow(
            new Vector3Int(900, 500, 100), "Win_Below", new Vector3(0, 1.25f, 0));
        var wall = wallGo.GetComponent<KitchenElement>();
        var window = winGo.GetComponent<WindowElement>();

        window!.SnapToWall();
        // Центр окна ниже нижней границы стены.
        winGo.transform.position = new Vector3(0, -0.1f, 0);

        var result = ConstraintValidator.Validate(new List<KitchenElement> { wall, window! });

        Assert.IsFalse(result.isValid);
        Assert.Contains(window, result.violations);

        Object.DestroyImmediate(wallGo);
        Object.DestroyImmediate(winGo);
    }

    /// <summary>Стены — якоря, и раньше их взаимные пересечения молча отбрасывались:
    /// считалось, что пол и стены расставляет приложение. С блочными стенами планировки
    /// это неверно — блок въезжал в блок на десятки миллиметров, а сцена была «чистой».</summary>
    [Test]
    public void Validate_WallDrivenIntoAnotherWall_Violation()
    {
        var a = ElementFactory.CreateWall(new Vector3Int(3000, 2700, 250), "Wall_A", Vector3.zero);
        // вторая стена перпендикулярно, её торец заходит в тело первой на 80 мм
        var b = ElementFactory.CreateWall(new Vector3Int(2000, 2700, 250), "Wall_B",
            new Vector3(0f, 0f, 0.955f));
        b.transform.rotation = ManagedRotation.Euler(0f, 90f, 0f);

        var elements = new List<KitchenElement>
            { a.GetComponent<KitchenElement>(), b.GetComponent<KitchenElement>() };
        var result = ConstraintValidator.Validate(elements);

        Assert.IsFalse(result.isValid, "стена в стене — это ошибка геометрии");
        CollectionAssert.Contains(result.violations, b.GetComponent<KitchenElement>());

        Object.DestroyImmediate(a);
        Object.DestroyImmediate(b);
    }

    /// <summary>А вот пол под стеной пересекается с ней штатно и краснеть не должен.</summary>
    [Test]
    public void Validate_FloorUnderWall_NoViolation()
    {
        var wall = ElementFactory.CreateWall(new Vector3Int(3000, 2700, 250), "Wall_OnFloor",
            new Vector3(0f, 1.35f, 0f));
        var floor = ElementFactory.CreateFloor(new Vector3Int(4000, 100, 4000), "Floor_Under",
            new Vector3(0f, 1.3f, 0f));

        var elements = new List<KitchenElement>
            { wall.GetComponent<KitchenElement>(), floor.GetComponent<KitchenElement>() };
        var result = ConstraintValidator.Validate(elements);

        CollectionAssert.DoesNotContain(result.violations, wall.GetComponent<KitchenElement>());
        CollectionAssert.DoesNotContain(result.violations, floor.GetComponent<KitchenElement>());

        Object.DestroyImmediate(wall);
        Object.DestroyImmediate(floor);
    }

    /// <summary>Сенсор на ПРОВОДКУ осевой линии из сцены в ядро. Само правило
    /// про углы живёт на быстром пути (<c>WallCornerJointTests</c>), но там
    /// осевая задаётся руками; здесь стены строит настоящая фабрика, а линию
    /// выводит <c>ValidationSnapshot</c> — если он перестанет её заводить, ядро
    /// молча вернёт четыре COL-01 на каждое замкнутое помещение, как было до
    /// правки, и ни один тест ядра этого не заметит.</summary>
    [Test]
    public void Validate_RoomOfFourWalls_CornersAreNotViolations()
    {
        const float half = 1.5f;
        var alongX = new Vector3Int(3000, 2500, 100);
        var alongZ = new Vector3Int(100, 2500, 3000);
        var north = ElementFactory.CreateWall(alongX, "Room_N", new Vector3(0f, 1.25f, -half));
        var south = ElementFactory.CreateWall(alongX, "Room_S", new Vector3(0f, 1.25f, half));
        var west = ElementFactory.CreateWall(alongZ, "Room_W", new Vector3(-half, 1.25f, 0f));
        var east = ElementFactory.CreateWall(alongZ, "Room_E", new Vector3(half, 1.25f, 0f));

        var elements = new List<KitchenElement>
        {
            north.GetComponent<KitchenElement>(), south.GetComponent<KitchenElement>(),
            west.GetComponent<KitchenElement>(), east.GetComponent<KitchenElement>(),
        };
        var result = ConstraintValidator.Validate(elements);

        Assert.IsEmpty(result.violations,
            "четыре стены по периметру перекрываются на углах на полтолщины — это ус, а не "
            + "дефект: осевые сходятся в общих точках. Помеченными оказались: "
            + string.Join(", ", result.violations.ConvertAll(e => e.PartName)));

        Object.DestroyImmediate(north);
        Object.DestroyImmediate(south);
        Object.DestroyImmediate(west);
        Object.DestroyImmediate(east);
    }

    /// <summary>Тот же угол, но стены повёрнуты — так их и ставит
    /// <c>create_walls</c>, считая позу из пары точек. Осевая обязана считаться
    /// от ПОВОРОТА элемента, а не от мировых осей.</summary>
    [Test]
    public void Validate_RotatedWallsMeetingAtACorner_IsNotAViolation()
    {
        var dims = new Vector3Int(3000, 2500, 100);
        var south = ElementFactory.CreateWall(dims, "Rot_S", new Vector3(1.5f, 1.25f, 0f));
        var east = ElementFactory.CreateWall(dims, "Rot_E", new Vector3(3f, 1.25f, 1.5f));
        east.transform.rotation = ManagedRotation.Euler(0f, 90f, 0f);

        var elements = new List<KitchenElement>
            { south.GetComponent<KitchenElement>(), east.GetComponent<KitchenElement>() };
        var result = ConstraintValidator.Validate(elements);

        Assert.IsEmpty(result.violations,
            "осевые обеих стен упираются в точку (3000, 0): угол сложен верно, и поворот "
            + "элемента обязан участвовать в выводе линии. Помеченными оказались: "
            + string.Join(", ", result.violations.ConvertAll(e => e.PartName)));

        Object.DestroyImmediate(south);
        Object.DestroyImmediate(east);
    }
}
