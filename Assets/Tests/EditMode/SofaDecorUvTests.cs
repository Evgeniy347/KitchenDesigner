using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using KitchenDesigner.Core;
using KitchenDesigner.Tests.Geometry;

/// <summary>Декор на диване не растягивается: на каждой плоской грани каждой части
/// (сиденье, спинка, боковые валики, подушки) миллиметр детали равен миллиметру
/// текстуры по ОБЕИМ осям (docs/TEXTURES.md → «Масштаб физический»). Раньше
/// <c>_BaseMap_ST</c> получало только сиденье, а лежащая спинка и мягкие детали
/// несли свои развёртки «0..1» без пересчёта — на разложенном диване полосы вдоль
/// одной оси (скриншот пользователя).
///
/// Плитка в тесте НАМЕРЕННО непропорциональна (400 на 900, у второго декора 1200 на
/// 300): при квадратной плитке перепутанные оси дали бы тот же рисунок и тест не
/// заметил бы дефекта. Сенсор — <see cref="UvStretch"/>, он же проверен на старых
/// развёртках в <c>PartUvTests</c>.</summary>
public class SofaDecorUvTests
{
    private const string PrimaryDecor = "uv-test-primary";
    private const string SecondaryDecor = "uv-test-secondary";
    private const int PrimaryTileWidth = 400;
    private const int PrimaryTileHeight = 900;
    private const int SecondaryTileWidth = 1200;
    private const int SecondaryTileHeight = 300;

    private readonly List<GameObject> _spawned = new List<GameObject>();

    [SetUp]
    public void SetUp()
    {
        LogAssert.ignoreFailingMessages = true;
        var primary = new MaterialDef(PrimaryDecor, "Проба 1", "Ткань", Color.gray,
            null, PrimaryTileWidth);
        primary.tileHeightMM = PrimaryTileHeight;
        var secondary = new MaterialDef(SecondaryDecor, "Проба 2", "Ткань", Color.gray,
            null, SecondaryTileWidth);
        secondary.tileHeightMM = SecondaryTileHeight;
        MaterialCatalog.Register(primary);
        MaterialCatalog.Register(secondary);
    }

    [TearDown]
    public void TearDown()
    {
        foreach (var go in _spawned)
            if (go != null) Object.DestroyImmediate(go);
        _spawned.Clear();
        PartRegistry.Clear();
        MaterialCatalog.Reset();
        MaterialManager.ClearCache();
        LogAssert.ignoreFailingMessages = false;
    }

    private SofaElement DressedSofa(int width, int depth, int seatHeight)
    {
        var go = ElementFactory.CreateSofa(new Vector3Int(width, SofaLayout.OverallHeightMM, depth),
            120, seatHeight, "Диван-UV", Vector3.zero);
        _spawned.Add(go);
        var sofa = go.GetComponent<SofaElement>()!;
        sofa.PrimaryMaterialId = PrimaryDecor;
        sofa.SecondaryMaterialId = SecondaryDecor;
        MaterialManager.RefreshTiling(sofa);
        return sofa;
    }

    private static Transform Part(SofaElement sofa, string name)
    {
        var part = sofa.transform.Find(SofaLayout.FrontGroupName + "/" + name);
        if (part == null) part = sofa.transform.Find(SofaLayout.HingeGroupName + "/" + name);
        Assert.IsNotNull(part, "нет детали " + name);
        return part!;
    }

    private static UvStretch.Report Measure(Transform part, Vector2Int tileMM, float axisDot)
    {
        var mesh = part.GetComponent<MeshFilter>()!.sharedMesh;
        var block = new MaterialPropertyBlock();
        part.GetComponent<MeshRenderer>()!.GetPropertyBlock(block);
        var st = block.GetVector("_BaseMap_ST");
        Assert.AreNotEqual(Vector4.zero, st,
            "у детали " + part.name + " не выставлен _BaseMap_ST: рисунок растянется на её "
            + "развёртку 0..1");

        var vertices = mesh.vertices;
        var uv = mesh.uv;
        var positionsMm = new Vector3[vertices.Length];
        var textureMm = new Vector2[vertices.Length];
        for (int i = 0; i < vertices.Length; i++)
        {
            positionsMm[i] = vertices[i] * 1000f;
            textureMm[i] = new Vector2(uv[i].x * st.x * tileMM.x, uv[i].y * st.y * tileMM.y);
        }

        return UvStretch.Measure(positionsMm, textureMm, mesh.normals, mesh.triangles, axisDot);
    }

    private static void AssertHonest(Transform part, Vector2Int tileMM, float axisDot,
        float tolerance)
    {
        var report = Measure(part, tileMM, axisDot);

        Assert.Greater(report.Triangles, 8,
            part.name + ": плоских треугольников слишком мало — сенсор ничего не проверил бы");
        Assert.AreEqual(1f, report.MinSigma, tolerance,
            part.name + ": вдоль одной из осей рисунок сжат или превращён в полосы "
            + "(σ_min " + report.MinSigma + "): текстура растянута");
        Assert.AreEqual(1f, report.MaxSigma, tolerance,
            part.name + ": вдоль одной из осей рисунок раздут (σ_max " + report.MaxSigma + ")");
    }

    [Test]
    public void Seat_TakesItsTextureAtPhysicalScale_OnBothAxes()
    {
        var sofa = DressedSofa(2000, 900, 360);

        AssertHonest(Part(sofa, SofaLayout.SeatName),
            new Vector2Int(PrimaryTileWidth, PrimaryTileHeight), UvStretch.AxisAlignedDot, 0.02f);
    }

    [Test]
    public void Backrest_TakesItsTextureAtPhysicalScale_OnBothAxes_FoldedAndLying()
    {
        var sofa = DressedSofa(2000, 900, 360);
        var tile = new Vector2Int(PrimaryTileWidth, PrimaryTileHeight);

        AssertHonest(Part(sofa, SofaLayout.BackrestName), tile, UvStretch.AxisAlignedDot, 0.02f);

        sofa.SnapToStage(SofaStage.Bed);

        AssertHonest(Part(sofa, SofaLayout.BackrestName), tile, UvStretch.AxisAlignedDot, 0.02f);
    }

    [Test]
    public void ArmCushions_TakeTheirTextureAtPhysicalScale_InTheSecondaryDecorTile()
    {
        var sofa = DressedSofa(2000, 900, 360);
        var tile = new Vector2Int(SecondaryTileWidth, SecondaryTileHeight);

        AssertHonest(Part(sofa, SofaLayout.ArmCushionLeftName), tile,
            UvStretch.AxisAlignedDot, 0.02f);
        AssertHonest(Part(sofa, SofaLayout.ArmCushionRightName), tile,
            UvStretch.AxisAlignedDot, 0.02f);
    }

    [Test]
    public void BackCushions_TakeTheirTextureAtPhysicalScale_InTheSecondaryDecorTile()
    {
        var sofa = DressedSofa(2000, 900, 360);
        var tile = new Vector2Int(SecondaryTileWidth, SecondaryTileHeight);

        AssertHonest(Part(sofa, SofaLayout.BackCushionLeftName), tile, 0.9f, 0.15f);
        AssertHonest(Part(sofa, SofaLayout.BackCushionRightName), tile, 0.9f, 0.15f);
    }

    [Test]
    public void Resize_KeepsEveryPartUnstretched()
    {
        var sofa = DressedSofa(2000, 900, 360);

        sofa.DimensionsMM = new Vector3Int(2600, 800, 1100);

        AssertHonest(Part(sofa, SofaLayout.SeatName),
            new Vector2Int(PrimaryTileWidth, PrimaryTileHeight), UvStretch.AxisAlignedDot, 0.02f);
        AssertHonest(Part(sofa, SofaLayout.BackrestName),
            new Vector2Int(PrimaryTileWidth, PrimaryTileHeight), UvStretch.AxisAlignedDot, 0.02f);
        AssertHonest(Part(sofa, SofaLayout.ArmCushionRightName),
            new Vector2Int(SecondaryTileWidth, SecondaryTileHeight), UvStretch.AxisAlignedDot, 0.02f);
    }

    [Test]
    public void ChangingTheSecondaryDecor_RetilesTheCushions_WithoutTouchingTheUpholstery()
    {
        var sofa = DressedSofa(2000, 900, 360);
        var seatBefore = Measure(Part(sofa, SofaLayout.SeatName),
            new Vector2Int(PrimaryTileWidth, PrimaryTileHeight), UvStretch.AxisAlignedDot);

        sofa.SecondaryMaterialId = PrimaryDecor;

        AssertHonest(Part(sofa, SofaLayout.ArmCushionRightName),
            new Vector2Int(PrimaryTileWidth, PrimaryTileHeight), UvStretch.AxisAlignedDot, 0.02f);
        Assert.AreEqual(seatBefore.MinSigma, Measure(Part(sofa, SofaLayout.SeatName),
            new Vector2Int(PrimaryTileWidth, PrimaryTileHeight), UvStretch.AxisAlignedDot).MinSigma,
            1e-4f, "сиденье при смене подушечного декора не пересчитывается");
    }
}
