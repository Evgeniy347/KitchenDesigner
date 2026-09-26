using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;
using KitchenDesigner.Core.Analysis;
using KitchenDesigner.Core.Construction;

/// <summary>W2/W3 (part3-plan.md, WALL LAYERS): <see cref="WallLayerElement"/> следует за
/// хозяйской стеной (позиция, поворот, длина/высота) и вырезает у себя ТЕ ЖЕ проёмы, что и
/// стена — тем же u/v-расчётом, что и <c>Wall.RebuildMesh</c> (agents/TEST-DESIGN.md: ключевой
/// сценарий — слой перед окном получает такой же вырез, едет за стеной и роняет ATT-01, когда
/// хозяин удалён).</summary>
public class WallLayerElementTests
{
    private readonly List<GameObject> _spawned = new List<GameObject>();

    [TearDown]
    public void TearDown()
    {
        foreach (var go in _spawned)
            if (go != null) Object.DestroyImmediate(go);
        _spawned.Clear();
        PartRegistry.Clear();
    }

    private GameObject SpawnWall(Vector3Int dims, string name, Vector3 position)
    {
        var go = ElementFactory.CreateWall(dims, name, position);
        _spawned.Add(go);
        return go;
    }

    private T SpawnLayer<T>(string name, int thicknessMm, Vector3 nearPosition) where T : WallLayerElement
    {
        var go = ElementRoot.NewCube(name, "Слой стены", nearPosition);
        _spawned.Add(go);
        var layer = go.AddComponent<T>();
        layer.PartName = go.name;
        layer.Movable = true;
        layer.DimensionsMM = new Vector3Int(100, 100, thicknessMm);
        ElementRoot.Publish(go, layer);
        return layer;
    }

    private InsulationElement SpawnInsulation(string name, Vector3 nearPosition) =>
        SpawnLayer<InsulationElement>(name, WallLayerDefaults.InsulationThicknessMm, nearPosition);

    private static void AssertFaceHasHole(Mesh mesh, float cu, float cv, float hu, float hv)
    {
        bool foundEdge = false;
        foreach (var v in mesh.vertices)
        {
            if (Mathf.Abs(Mathf.Abs(v.z) - 0.5f) > 1e-4f) continue;
            float u = v.x, w = v.y;
            bool strictlyInside = u > cu - hu + 1e-4f && u < cu + hu - 1e-4f &&
                                  w > cv - hv + 1e-4f && w < cv + hv - 1e-4f;
            Assert.IsFalse(strictlyInside, $"вершина {v} внутри проёма — дыра не вырезана");
            bool onEdge = (Mathf.Abs(u - (cu - hu)) < 1e-4f || Mathf.Abs(u - (cu + hu)) < 1e-4f) &&
                          w >= cv - hv - 1e-4f && w <= cv + hv + 1e-4f;
            if (onEdge) foundEdge = true;
        }
        Assert.IsTrue(foundEdge, "нет вершин по границе проёма — вырез не построен у слоя");
    }

    [Test]
    public void InFrontOfWindow_GetsTheSameCutout_AsTheHostWall()
    {
        var wallGo = SpawnWall(new Vector3Int(3000, 2500, 100), "Wall_Layer_Cut", new Vector3(0f, 1.25f, 0f));
        var winGo = ElementFactory.CreateWindow(new Vector3Int(900, 1200, 100), "Win_Layer_Cut",
            new Vector3(0.6f, 1.2f, 0f));
        _spawned.Add(winGo);
        winGo.GetComponent<WindowElement>()!.SnapToWall();

        var layer = SpawnInsulation("Ins_Layer_Cut", new Vector3(0f, 1.25f, 0.5f));
        layer.SnapToNearestWall();

        var wallMesh = wallGo.GetComponent<MeshFilter>()!.sharedMesh!;
        var layerMesh = layer.GetComponent<MeshFilter>()!.sharedMesh!;

        Assert.Greater(layerMesh.vertexCount, 0);
        AssertFaceHasHole(layerMesh, cu: 0.2f, cv: -0.02f, hu: 0.45f / 3f, hv: 0.6f / 2.5f);
        Assert.Greater(wallMesh.vertexCount, 0, "у стены тоже обязан быть вырез — иначе сравнивать не с чем");
    }

    [Test]
    public void SnappedLayer_SitsFlushAgainstTheWallFace_OnTheChosenSide()
    {
        var wallGo = SpawnWall(new Vector3Int(3000, 2500, 100), "Wall_Layer_Seat", new Vector3(0f, 1.25f, 0f));
        var layer = SpawnInsulation("Ins_Layer_Seat", new Vector3(0f, 1.25f, 0.5f));

        layer.SnapToNearestWall();

        float expectedZ = 0.05f + WallLayerDefaults.InsulationThicknessMm * 0.001f * 0.5f;
        Assert.AreEqual(expectedZ, layer.transform.position.z, 0.001f,
            "слой обязан прилегать вплотную к грани стены — зазор стена/слой = 0");
        Assert.AreEqual(wallGo.GetComponent<KitchenElement>()!.DimensionsMM.x, layer.DimensionsMM.x,
            "длина слоя обязана совпасть с длиной стены");
        Assert.AreEqual(wallGo.GetComponent<KitchenElement>()!.DimensionsMM.y, layer.DimensionsMM.y,
            "высота слоя обязана совпасть с высотой стены");
    }

    [Test]
    public void MovedWall_DragsTheLayerAlong_OnTheNextResync()
    {
        var wallGo = SpawnWall(new Vector3Int(3000, 2500, 100), "Wall_Layer_Move", new Vector3(0f, 1.25f, 0f));
        var layer = SpawnInsulation("Ins_Layer_Move", new Vector3(0f, 1.25f, 0.5f));
        layer.SnapToNearestWall();
        var startPos = layer.transform.position;

        wallGo.transform.position += new Vector3(0.5f, 0f, 0f);
        layer.Update();

        Assert.AreEqual(startPos.x + 0.5f, layer.transform.position.x, 0.001f,
            "слой обязан проехать вместе со стеной вдоль её длины");
    }

    [Test]
    public void HostWallDeleted_RaisesATT01()
    {
        var wallGo = SpawnWall(new Vector3Int(3000, 2500, 100), "Wall_Layer_Gone", new Vector3(0f, 1.25f, 0f));
        var layer = SpawnInsulation("Ins_Layer_Gone", new Vector3(0f, 1.25f, 0.5f));
        layer.SnapToNearestWall();
        Assert.IsFalse(string.IsNullOrEmpty(layer.HostWallName), "слой обязан был привязаться к найденной стене");

        _spawned.Remove(wallGo);
        Object.DestroyImmediate(wallGo);

        var issue = FindAttachIssue();
        Assert.IsTrue(issue.HasValue, "удаление хозяйской стены обязано поднять ATT-01");
        Assert.AreEqual(IssueLevel.Error, issue!.Value.Level);
        Assert.AreEqual("ATT-01", issue.Value.Code);
    }

    private static AnalysisIssue? FindAttachIssue()
    {
        foreach (var issue in SceneAnalyzer.Analyze())
            if (issue.Code == "ATT-01") return issue;
        return null;
    }

    /// <summary>#5 (test-results/review-construction.md): the seat offset used to be
    /// computed only in <c>ResyncToHost</c>, so a thickness edit moved the mesh's scale
    /// but left the OLD centre — the new, thicker slab then reaches into the wall.</summary>
    [Test]
    public void ChangingThickness_ReSeatsTheLayer_InsteadOfPenetratingTheWall()
    {
        SpawnWall(new Vector3Int(3000, 2500, 100), "Wall_Layer_Thick", new Vector3(0f, 1.25f, 0f));
        var layer = SpawnInsulation("Ins_Layer_Thick", new Vector3(0f, 1.25f, 0.5f));
        layer.SnapToNearestWall();

        layer.ThicknessMm = 200;

        float wallHalfUnits = 0.05f;
        float expectedZ = wallHalfUnits + 200 * 0.001f * 0.5f;
        Assert.AreEqual(expectedZ, layer.transform.position.z, 0.001f,
            "смена толщины обязана пересадить слой вплотную к стене на НОВУЮ толщину, а не "
            + "оставить центр на месте — иначе более толстый слой уходит внутрь стены");
    }

    /// <summary>#6 (test-results/review-construction.md): insulation, vent gap and cladding
    /// on the same wall face all seated directly on the bare wall and overlapped each other.
    /// They must stack outward in physical order: wall -> insulation -> vent gap -> cladding.</summary>
    [Test]
    public void InsulationVentGapAndCladding_StackOutwardInPhysicalOrder_InsteadOfOverlapping()
    {
        SpawnWall(new Vector3Int(3000, 2500, 100), "Wall_Layer_Stack", new Vector3(0f, 1.25f, 0f));

        var insulation = SpawnInsulation("Ins_Layer_Stack", new Vector3(0f, 1.25f, 0.5f));
        insulation.SnapToNearestWall();

        var ventGap = SpawnLayer<VentGapElement>("Vent_Layer_Stack",
            WallLayerDefaults.VentGapThicknessMm, new Vector3(0f, 1.25f, 0.5f));
        ventGap.SnapToNearestWall();

        var cladding = SpawnLayer<CladdingElement>("Clad_Layer_Stack",
            WallLayerDefaults.CladdingThicknessMm, new Vector3(0f, 1.25f, 0.5f));
        cladding.SnapToNearestWall();

        const float wallHalf = 0.05f;
        float insFull = WallLayerDefaults.InsulationThicknessMm * 0.001f;
        float insHalf = insFull * 0.5f;
        float ventFull = WallLayerDefaults.VentGapThicknessMm * 0.001f;
        float ventHalf = ventFull * 0.5f;
        float cladHalf = WallLayerDefaults.CladdingThicknessMm * 0.001f * 0.5f;

        Assert.AreEqual(wallHalf + insHalf, insulation.transform.position.z, 0.001f,
            "утеплитель садится прямо на стену");
        Assert.AreEqual(wallHalf + insFull + ventHalf, ventGap.transform.position.z, 0.001f,
            "вентзазор обязан встать ЗА утеплителем, а не на голой грани стены");
        Assert.AreEqual(wallHalf + insFull + ventFull + cladHalf, cladding.transform.position.z, 0.001f,
            "облицовка обязана встать за вентзазором, а не на голой грани стены");
    }

    /// <summary>#8 (test-results/review-construction.md): <c>Update()</c> used to resolve the
    /// host wall by name through <c>PartRegistry.GetAll()</c> — a full scene copy — on EVERY
    /// frame, even when nothing changed. The reference is now cached and re-resolved by name
    /// only when <see cref="WallLayerElement.HostWallName"/> itself changes.</summary>
    [Test]
    public void RepeatedUpdatesWithNoChange_DoNotRescanTheSceneEveryFrame()
    {
        SpawnWall(new Vector3Int(3000, 2500, 100), "Wall_Layer_Perf", new Vector3(0f, 1.25f, 0f));
        var layer = SpawnInsulation("Ins_Layer_Perf", new Vector3(0f, 1.25f, 0.5f));
        layer.SnapToNearestWall();
        layer.Update();

        PartRegistryInstance.TakeGetAllCalls();
        for (int i = 0; i < 50; i++) layer.Update();

        int scans = PartRegistryInstance.TakeGetAllCalls();
        Assert.AreEqual(0, scans,
            "хозяйская стена не менялась ни разу за 50 кадров — обходить сцену незачем "
            + "(раньше Update() копировал весь реестр элементов на каждый кадр)");
    }
}
