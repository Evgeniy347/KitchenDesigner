using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

/// <summary>Контракт базовой «Детали»: кто вообще является деталью, когда
/// пересобирается её собственный меш и какую позу она предъявляет геометрии.
///
/// Здесь живут причины, которые раньше были комментариями в KitchenElement.cs:
/// пересборка меша стоит дороже расчёта маски, паз не меняет габаритный короб,
/// а едущая за родителем деталь считается по позе покоя, а не по трансформу.</summary>
public class KitchenElementContractTests
{
    private const float U = AppConstants.MM_TO_UNITS;
    private static readonly Vector3Int Sheet = new Vector3Int(800, 400, 18);

    private readonly List<GameObject> _spawned = new List<GameObject>();

    [SetUp]
    public void Setup() => PartRegistry.Clear();

    [TearDown]
    public void Teardown()
    {
        KitchenElement.SuppressVisualRebuild = false;
        foreach (var go in _spawned) if (go != null) Object.DestroyImmediate(go);
        _spawned.Clear();
        foreach (var e in Object.FindObjectsByType<KitchenElement>())
            if (e != null) Object.DestroyImmediate(e.gameObject);
        PartRegistry.Clear();
    }

    private KitchenElement MakePart(Vector3Int dims, Vector3 pos = default)
    {
        var go = ElementFactory.CreatePart(dims, $"Part{_spawned.Count}", pos);
        _spawned.Add(go);
        var e = go.GetComponent<KitchenElement>();
        e.DimensionsMM = dims;
        e.ApplyDimensions();
        return e;
    }

    private static Mesh MeshOf(KitchenElement e) => e.GetComponent<MeshFilter>().sharedMesh;

    // --- Кто является деталью ---

    [Test]
    public void Wall_BasePlate_AndAppliance_AreNotParts_SoTheyGetNeitherGroovesNorGaps()
    {
        var wallGo = ElementFactory.CreateWall(new Vector3Int(3000, 2700, 100), "W", Vector3.zero);
        _spawned.Add(wallGo);
        var wall = wallGo.GetComponent<KitchenElement>();

        var plate = MakePart(Sheet);
        plate.gameObject.AddComponent<BasePlate>();

        var sinkGo = ElementFactory.CreateSink("S", Vector3.zero);
        _spawned.Add(sinkGo);
        var sink = sinkGo.GetComponent<KitchenElement>();

        Assert.IsFalse(wall.SupportsGrooves, "стена — не деталь: врезка пласти в неё не определена");
        Assert.IsFalse(plate.SupportsGrooves, "подложка — не деталь");
        Assert.IsFalse(sink.SupportsGrooves, "у техники геометрия своя процедурная");

        Assert.IsFalse(wall.SupportsGaps, "у стены габарит — сама конструкция, зазору взяться неоткуда");
        Assert.IsFalse(plate.SupportsGaps, "то же у подложки");
        Assert.IsFalse(sink.SupportsGaps, "габарит техники задан корпусом прибора");
    }

    [Test]
    public void EdgeBandingEnabled_OnAPartThatIsNotASheet_ReadsFalse()
    {
        var bar = MakePart(new Vector3Int(40, 40, 400));
        Assert.IsFalse(bar.SupportsEdges, "у бруска торцов в смысле кромки нет");

        bar.EdgeBandingEnabled = true;

        Assert.IsFalse(bar.EdgeBandingEnabled,
            "деталь без поддержки кромки обязана отвечать false, иначе спецификация напечатает кромку, которой негде лечь");
    }

    // --- Когда пересобирается собственный меш ---

    [Test]
    public void Part_Resized_BuildsANewMesh()
    {
        var e = MakePart(Sheet);
        var before = MeshOf(e);

        e.DimensionsMM = new Vector3Int(600, 300, 18);

        Assert.AreNotSame(before, MeshOf(e),
            "доли паза и UV декора считаются от размеров детали — без пересборки они растянулись бы вместе с localScale");
    }

    [Test]
    public void Part_AssignedTheSameDimensions_KeepsTheMeshItAlreadyBuilt()
    {
        var e = MakePart(Sheet);
        var before = MeshOf(e);

        e.DimensionsMM = Sheet;
        e.ApplyDimensions();

        Assert.AreSame(before, MeshOf(e),
            "размеры не изменились — пересобирать меш незачем, это дороже самого расчёта");
    }

    [Test]
    public void SetBareFaceMask_WithANewMask_RebuildsTheMesh()
    {
        var e = MakePart(Sheet);
        var before = MeshOf(e);

        e.SetBareFaceMask(0b000001);

        Assert.AreEqual(0b000001, e.BareFaceMask);
        Assert.AreNotSame(before, MeshOf(e), "набор граней под подложкой поменялся — сабмеш торцов пересобирается");
    }

    [Test]
    public void SetBareFaceMask_WithTheSameMask_KeepsTheMesh()
    {
        var e = MakePart(Sheet);
        e.SetBareFaceMask(0b000001);
        var before = MeshOf(e);

        e.SetBareFaceMask(0b000001);

        Assert.AreSame(before, MeshOf(e),
            "проход по сцене идёт на каждое изменение, а пересборка меша дороже расчёта маски");
    }

    [Test]
    public void SuppressVisualRebuild_AppliesTheScale_ButLeavesTheMeshAlone()
    {
        var e = MakePart(Sheet);
        var before = MeshOf(e);

        KitchenElement.SuppressVisualRebuild = true;
        e.DimensionsMM = new Vector3Int(600, 300, 18);

        Assert.AreEqual(600 * U, e.transform.localScale.x, 1e-6f, "размер применяется как обычно");
        Assert.AreSame(before, MeshOf(e),
            "расчётным тестам меш не нужен, а покадровый свип упирался именно в пересборку на каждый миллиметр");
    }

    [Test]
    public void SubmeshMaterials_HaveNoEmptySlot()
    {
        var e = MakePart(Sheet);
        e.EdgeBandingEnabled = false;
        e.SetBareFaceMask(0b001111);
        e.RefreshSubmeshMaterials();

        var mats = e.GetComponent<MeshRenderer>().sharedMaterials;
        Assert.AreEqual(MeshOf(e).subMeshCount, mats.Length);
        for (int i = 0; i < mats.Length; i++)
            Assert.IsNotNull(mats[i],
                $"слот {i} пуст: дыра в материалах рендерера рисует несуществующую грань, "
                + "поэтому торец без шейдера подложки остаётся на декоре");
    }

    // --- Паз: посадочная грань есть, габаритный короб не меняется ---

    [Test]
    public void GrooveSeatFace_SitsOnTheGrooveFloor_LookingOutOfThePlane()
    {
        var e = MakePart(Sheet);
        e.AddGroove(new GrooveSpec(GrooveKind.Through, GrooveSide.Top));

        var seats = e.GetGrooveSeatFaces();
        Assert.AreEqual(1, seats.Length, "одна посадочная грань на паз");

        float expectedZ = (0.5f - GrooveMesh.DepthFraction(Sheet)) * Sheet.z * U;
        Assert.AreEqual(expectedZ, seats[0].center.z, 1e-6f,
            "дно паза утоплено от пласти на глубину паза — иначе вкладная панель встанет мимо номинала");
        Assert.Less(seats[0].center.z, 0.5f * Sheet.z * U, "дно паза лежит ВНУТРИ детали");
        Assert.AreEqual(1f, Vector3.Dot(seats[0].normal, Vector3.forward), 1e-5f,
            "паз режется в пласти +Z, и его дно смотрит наружу той же пластью");
    }

    [Test]
    public void Groove_LeavesTheGabaritFacesUntouched()
    {
        var e = MakePart(Sheet);
        var before = e.GetFaces();

        e.AddGroove(new GrooveSpec(GrooveKind.Through, GrooveSide.Top));
        var after = e.GetFaces();

        Assert.AreEqual(before.Length, after.Length,
            "короб остаётся коробом: иначе поехали бы ручки ресайза, выделение и прилипание соседей");
        for (int i = 0; i < before.Length; i++)
        {
            Assert.AreEqual(before[i].center, after[i].center, $"грань {i} сдвинулась");
            Assert.AreEqual(before[i].size, after[i].size, $"грань {i} изменила размер");
        }
    }

    // --- Поза, по которой считается геометрия ---

    [Test]
    public void AttachRiddenPart_ProbedAtAnotherPosition_KeepsItsRestGeometry()
    {
        var facadeGo = ElementFactory.CreateFacade(new Vector3Int(400, 700, 18), "Фасад", Vector3.zero);
        _spawned.Add(facadeGo);
        var facade = facadeGo.GetComponent<FacadeElement>();

        var board = MakePart(new Vector3Int(300, 18, 250), new Vector3(0f, -0.35f, 0.25f));
        board.PartName = "Дно";
        board.AttachedToName = facade.PartName;
        var restCentre = board.GetFaces()[0].center;

        facade.SetOpen(true);
        facade.StepDoor(1f);
        AttachRider.Step();

        Assert.IsTrue(board.IsAttachRidden, "деталь едет за фасадом");
        Assert.AreEqual(restCentre, board.GetFaces()[0].center,
            "у едущей детали геометрия считается по позе покоя, а не по уехавшему трансформу");
        Assert.AreEqual(restCentre, board.GetFacesAt(new Vector3(5f, 5f, 5f))[0].center,
            "примерка в гипотетическую позицию у едущей детали тоже обязана дать позу покоя — "
            + "иначе снэп мажет и деталь прыгает при закрытии");
    }
}
