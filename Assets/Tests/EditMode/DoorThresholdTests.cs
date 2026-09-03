using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

/// <summary>У двери нет порога. Проём в стене режется от нулевой отметки пола,
/// полоски стены под дверью не остаётся, а полотно висит на
/// <see cref="DoorOpeningLayout.LeafFloorGapMM"/> над полом — обычный зазор под
/// дверью.
///
/// Раньше нижняя отметка проёма задавалась позицией двери: дверь стояла где
/// угодно по высоте, и под ней оставалась перемычка. Теперь низ прибит к полу
/// в ДВУХ местах, и это не дублирование:
///
/// * <c>DoorElement.AlignToWall</c> ставит сам элемент — от него зависят
///   коллайдер, габарит в спецификации и правило <c>OutOfWallBounds</c>;
/// * <c>Wall.RebuildMesh</c> тянет ВЫРЕЗ до базы стены независимо от того, где
///   сейчас дверь, — иначе на кадре между сдвигом и посадкой (и при ошибке
///   float на границе <c>MinCellNorm</c>) внизу проступает волосковый простенок.
///
/// Окна правка не касается: их проём остаётся там, куда его поставили, и
/// подоконная часть стены обязана уцелеть — это проверяется здесь же.</summary>
public class DoorThresholdTests : SnapTestBase
{
    private const int WallLenMM = 3000;
    private const int WallHeightMM = 2500;
    private const int WallThickMM = 100;
    private const int DoorWidthMM = 900;
    private const int DoorHeightMM = 2100;

    private static readonly Vector3 WallCentre = new Vector3(0f, WallHeightMM * 0.5f * MM, 0f);
    private const float WallBaseY = 0f;

    private Wall SpawnWall(string name)
    {
        var go = ElementFactory.CreateWall(
            new Vector3Int(WallLenMM, WallHeightMM, WallThickMM), name, WallCentre);
        _spawned.Add(go);
        PartRegistry.Register(go.GetComponent<KitchenElement>());
        return go.GetComponent<Wall>()!;
    }

    private DoorElement SpawnDoor(string name, float y)
    {
        var go = ElementFactory.CreateDoor(
            new Vector3Int(DoorWidthMM, DoorHeightMM, WallThickMM), name, new Vector3(0f, y, 0f));
        _spawned.Add(go);
        var door = go.GetComponent<DoorElement>()!;
        PartRegistry.Register(door);
        return door;
    }

    /// <summary>Есть ли на передней грани стены (z = +0.5 в нормированных
    /// координатах меша) материал в точке (x, y). Именно это отличает «проём» от
    /// «простенка»: вершины по границе выреза есть в обоих случаях, а вот
    /// закрытая точка внутри — только там, где стена осталась.</summary>
    private static bool FrontFaceCovers(Mesh mesh, float x, float y)
    {
        var verts = mesh.vertices;
        var tris = mesh.triangles;
        for (int t = 0; t < tris.Length; t += 3)
        {
            var a = verts[tris[t]];
            var b = verts[tris[t + 1]];
            var c = verts[tris[t + 2]];
            if (Mathf.Abs(a.z - 0.5f) > 1e-4f ||
                Mathf.Abs(b.z - 0.5f) > 1e-4f ||
                Mathf.Abs(c.z - 0.5f) > 1e-4f) continue;
            if (PointInTriangle(x, y, a, b, c)) return true;
        }
        return false;
    }

    private static bool PointInTriangle(float x, float y, Vector3 a, Vector3 b, Vector3 c)
    {
        float d1 = Cross(x, y, a, b);
        float d2 = Cross(x, y, b, c);
        float d3 = Cross(x, y, c, a);
        bool anyNeg = d1 < 0f || d2 < 0f || d3 < 0f;
        bool anyPos = d1 > 0f || d2 > 0f || d3 > 0f;
        return !(anyNeg && anyPos);
    }

    private static float Cross(float x, float y, Vector3 p, Vector3 q) =>
        (x - q.x) * (p.y - q.y) - (p.x - q.x) * (y - q.y);

    [Test]
    public void Door_SnappedToWall_StandsOnTheFloor()
    {
        SpawnWall("Wall_DoorFloor");
        var door = SpawnDoor("Door_Floor", 1.8f);

        door.SnapToWall();

        float bottom = door.transform.position.y - DoorHeightMM * 0.5f * MM;
        Assert.AreEqual(WallBaseY, bottom, Tol,
            "низ двери обязан стоять на полу — иначе под проёмом остаётся порог");
    }

    [Test]
    public void Door_DraggedUp_ComesBackToTheFloor()
    {
        SpawnWall("Wall_DoorDrag");
        var door = SpawnDoor("Door_Drag", 1.05f);
        door.SnapToWall();

        door.transform.position += new Vector3(0f, 0.4f, 0f);
        door.SnapToWall();

        float bottom = door.transform.position.y - DoorHeightMM * 0.5f * MM;
        Assert.AreEqual(WallBaseY, bottom, Tol,
            "у двери нет вертикальной степени свободы: подняли — вернулась на пол");
    }

    [Test]
    public void DoorLeaf_HangsTenMillimetresAboveTheFloor_AndKeepsTheOpeningTop()
    {
        SpawnWall("Wall_DoorLeaf");
        var door = SpawnDoor("Door_Leaf", 1.05f);
        door.SnapToWall();

        var frame = door.transform.Find("_Static/FrameLeft");
        Assert.IsNotNull(frame, "стойка коробки должна существовать");
        float half = frame!.localScale.y * 0.5f;
        float leafBottom = frame.position.y - half;
        float leafTop = frame.position.y + half;

        Assert.AreEqual(WallBaseY + DoorOpeningLayout.LeafFloorGapMM * MM, leafBottom, Tol,
            "полотно обязано висеть на 10 мм над полом — зазор под дверью");
        Assert.AreEqual(WallBaseY + DoorHeightMM * MM, leafTop, Tol,
            "верх полотна обязан совпасть с верхом проёма: зазор съедает высоту полотна, а не растит дырку");
    }

    [Test]
    public void Wall_WithDoor_HasNoStripOfWallUnderTheOpening()
    {
        var wall = SpawnWall("Wall_NoStrip");
        var door = SpawnDoor("Door_NoStrip", 1.05f);
        door.SnapToWall();

        var mesh = wall.GetComponent<MeshFilter>()!.sharedMesh!;

        Assert.IsFalse(FrontFaceCovers(mesh, 0f, -0.45f),
            "под дверью осталась стена — это и есть порог, которого быть не должно");
        Assert.IsTrue(FrontFaceCovers(mesh, 0.3f, -0.45f),
            "простенок сбоку от двери снесло вместе с порогом");
        Assert.IsTrue(FrontFaceCovers(mesh, 0f, 0.45f),
            "перемычка над дверью обязана остаться");
    }

    [Test]
    public void Wall_WithDoorMovedUpBeforeSnap_StillHasNoStripUnderTheOpening()
    {
        var wall = SpawnWall("Wall_LiftedDoor");
        var door = SpawnDoor("Door_Lifted", 1.05f);
        door.SnapToWall();

        door.transform.position += new Vector3(0f, 0.5f, 0f);
        wall.RebuildMesh();

        var mesh = wall.GetComponent<MeshFilter>()!.sharedMesh!;
        Assert.IsFalse(FrontFaceCovers(mesh, 0f, -0.45f),
            "вырез обязан тянуться до пола независимо от того, куда уехала дверь");
    }

    [Test]
    public void Wall_WithWindow_KeepsTheWallUnderTheSill()
    {
        var wall = SpawnWall("Wall_SillKept");
        var winGo = ElementFactory.CreateWindow(
            new Vector3Int(900, 1200, WallThickMM), "Win_SillKept", new Vector3(0f, 1.2f, 0f));
        _spawned.Add(winGo);
        winGo.GetComponent<WindowElement>()!.SnapToWall();

        var mesh = wall.GetComponent<MeshFilter>()!.sharedMesh!;

        Assert.IsTrue(FrontFaceCovers(mesh, 0f, -0.45f),
            "подоконная часть стены снесена — привязка низа к полу утекла с двери на окно");
        Assert.IsFalse(FrontFaceCovers(mesh, 0f, 0f),
            "окно не вырезано — тест смотрит не туда");
    }

    /// <summary>Порог — это не только простенок в стене, но и нижняя перекладина
    /// коробки: горизонтальный брус во всю ширину проёма, стоявший на 10 мм над
    /// полом. Под ним нельзя пройти, и у настоящей двери его нет — коробка у
    /// двери П-образная: две стойки и притолока.</summary>
    [Test]
    public void Door_Frame_HasNoBottomRail()
    {
        SpawnWall("Wall_NoSill");
        var door = SpawnDoor("Door_NoSill", 1.05f);
        door.SnapToWall();

        var frame = door.transform.Find("_Static");
        Assert.IsNotNull(frame, "коробка двери должна существовать");

        var names = new List<string>();
        foreach (Transform child in frame!) names.Add(child.name);

        CollectionAssert.DoesNotContain(names, "FrameBottom",
            "нижняя перекладина коробки — это и есть порог под дверью");
        CollectionAssert.Contains(names, "FrameLeft", "стойка коробки на месте");
        CollectionAssert.Contains(names, "FrameRight", "вторая стойка на месте");
        CollectionAssert.Contains(names, "FrameTop", "притолока на месте");
    }

    /// <summary>Убрав нижнюю перекладину, полотно обязано занять её место, а не
    /// оставить щель: низ полотна встаёт ровно на низ стоек — 10 мм над полом.</summary>
    [Test]
    public void DoorLeaf_FillsTheSpaceWhereTheBottomRailUsedToBe()
    {
        SpawnWall("Wall_LeafDown");
        var door = SpawnDoor("Door_LeafDown", 1.05f);
        door.SnapToWall();

        var jamb = door.transform.Find("_Static/FrameLeft")!;
        float jambBottom = jamb.position.y - jamb.localScale.y * 0.5f;

        var sashBottom = door.transform.Find("_Sash/SashBottom")!;
        float leafBottom = sashBottom.position.y - sashBottom.lossyScale.y * 0.5f;

        Assert.AreEqual(jambBottom, leafBottom, Tol,
            "низ полотна обязан совпасть с низом стоек — иначе на месте порога осталась щель");
        Assert.AreEqual(WallBaseY + DoorOpeningLayout.LeafFloorGapMM * MM, leafBottom, Tol,
            "и это те же 10 мм над полом, зазор под дверью");
    }

    private static List<WallMeshBuilder.WindowCutout> CutoutOnTheWallBase() =>
        new List<WallMeshBuilder.WindowCutout>
        {
            new WallMeshBuilder.WindowCutout
            {
                centerNorm = new Vector2(0f, -0.2f),
                halfSizeNorm = new Vector2(0.15f, 0.3f),
            }
        };

    /// <summary>Порог в дверном проёме бывает двух родов, и оба — горизонтальная
    /// поверхность на уровне пола внутри выреза. Первый — откос: квад, дублирующий
    /// нижнюю крышку стены. Второй — сама НИЖНЯЯ КРЫШКА, которая раньше шла сплошной
    /// плитой во всю длину стены, включая проём: под дверью оставалась пластина
    /// шириной проёма и толщиной стены. При совпадении с полом она давала
    /// z-fighting, а при 250-мм стене читалась как настоящий порожек.
    ///
    /// Проверяется НАКРЫТИЕ ТОЧКИ, а не число треугольников: после починки крышка
    /// разрезана на куски по бокам от проёма, и у них появились вершины внутри
    /// диапазона проёма по x — счётчик треугольников краснел бы на исправном
    /// меше.</summary>
    [Test]
    public void Builder_CutoutReachingTheWallBase_LeavesNoFloorLevelSurfaceInTheOpening()
    {
        var mesh = WallMeshBuilder.Build(CutoutOnTheWallBase());

        Assert.IsFalse(FloorPlaneCovers(mesh, 0f, 0f),
            "на уровне пола внутри проёма осталась горизонтальная поверхность — это порог");
        Assert.IsFalse(FloorPlaneCovers(mesh, 0.14f, 0.4f),
            "у самого края проёма — тоже проём, там пола стены быть не должно");
        Assert.IsTrue(FloorPlaneCovers(mesh, 0.3f, 0f),
            "рядом с проёмом стена стоит на полу, и её нижняя крышка обязана остаться — "
            + "иначе снизу видно нутро стены");
    }

    /// <summary>Окно низ стены не режет: под подоконником стена стоит, и её нижняя
    /// крышка обязана быть сплошной.</summary>
    [Test]
    public void Builder_CutoutAboveTheWallBase_KeepsTheBottomCapWhole()
    {
        var mesh = WallMeshBuilder.Build(new List<WallMeshBuilder.WindowCutout>
        {
            new WallMeshBuilder.WindowCutout
            {
                centerNorm = new Vector2(0f, 0.1f),
                halfSizeNorm = new Vector2(0.15f, 0.2f),
            }
        });

        Assert.IsTrue(FloorPlaneCovers(mesh, 0f, 0f),
            "вырез не доходит до пола, значит под ним стена — крышку резать нечем");
    }

    /// <summary>Есть ли горизонтальная поверхность в плоскости пола (y = -0,5 в
    /// нормированных координатах меша) над точкой (x, z). Вертикальные щёки проёма
    /// в эту плоскость не попадают целиком, поэтому берутся только треугольники,
    /// у которых в ней ВСЕ три вершины.</summary>
    private static bool FloorPlaneCovers(Mesh mesh, float x, float z)
    {
        var verts = mesh.vertices;
        var tris = mesh.triangles;
        for (int t = 0; t < tris.Length; t += 3)
        {
            var a = verts[tris[t]];
            var b = verts[tris[t + 1]];
            var c = verts[tris[t + 2]];
            if (Mathf.Abs(a.y + 0.5f) > 1e-4f ||
                Mathf.Abs(b.y + 0.5f) > 1e-4f ||
                Mathf.Abs(c.y + 0.5f) > 1e-4f) continue;
            if (PointInTriangle(x, z,
                new Vector3(a.x, a.z, 0f), new Vector3(b.x, b.z, 0f), new Vector3(c.x, c.z, 0f)))
                return true;
        }
        return false;
    }

    /// <summary>Обратная сторона той же правки: срезать откосы на границе грани
    /// целиком нельзя. Вертикальная щека проёма стоит НА ПОЛУ, и если её
    /// потерять, в дверную коробку будет видно нутро стены.</summary>
    [Test]
    public void Builder_CutoutReachingTheWallBase_KeepsTheJambStandingOnTheFloor()
    {
        var mesh = WallMeshBuilder.Build(CutoutOnTheWallBase());

        bool jambTouchesFloor = false;
        foreach (var v in mesh.vertices)
            if (Mathf.Abs(v.x + 0.15f) < 1e-4f && Mathf.Abs(v.y + 0.5f) < 1e-4f)
                jambTouchesFloor = true;

        Assert.IsTrue(jambTouchesFloor,
            "щека проёма не доходит до пола — правка срезала откосы на границе грани целиком");
    }
}
