using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;
using KitchenDesigner.Core.Construction;

/// <summary>test-results/review-construction.md #12: FoundationStripMesh.Build called
/// ProfileExtrusionMesh.Build once per polyline SEGMENT and copied its arrays into the
/// combined mesh by hand — the temporary segment Mesh was never destroyed, so a width drag
/// over N load-bearing walls leaked N orphaned native Mesh objects per rebuild. The fix
/// reuses MeshAccumulator (already used elsewhere in Core/Elements for exactly this shape
/// of problem), whose Consume() destroys the temporary immediately after copying it in.</summary>
public class FoundationStripMeshTests
{
    private static WallCentreline Wall(Vector3 centerUnits, int lengthAxisXmm, int lengthAxisZmm) =>
        WallCentreline.Of(centerUnits, Quaternion.identity, new Vector3Int(lengthAxisXmm, 2700, lengthAxisZmm));

    [Test]
    public void Build_TwoSegmentPolyline_LeavesExactlyOneMeshBehind_NotTheThrowawaySegments()
    {
        var a = Wall(new Vector3(2f, 0f, 0f), 4000, 250);
        var b = Wall(new Vector3(4f, 0f, 1.5f), 250, 3000);

        int before = Resources.FindObjectsOfTypeAll<Mesh>().Length;
        var mesh = FoundationStripMesh.Build(new[] { a, b }, Vector3.zero, 700f, 2300f);
        int after = Resources.FindObjectsOfTypeAll<Mesh>().Length;

        Assert.AreEqual(1, after - before,
            "Г-образная лента из двух сегментов (тот же угол, что и "
            + "FoundationLayoutTests.TotalLengthMm_LShape) обязана оставить в памяти ровно "
            + "ОДИН меш — объединённый результат; временные меши каждого сегмента "
            + "(ProfileExtrusionMesh.Build внутри AddSegment) обязаны быть уничтожены сразу, "
            + "а не висеть до UnloadUnusedAssets");

        Object.DestroyImmediate(mesh);
    }

    [Test]
    public void Build_NoPolylines_ReturnsAnEmptyMesh_NotACrash()
    {
        var mesh = FoundationStripMesh.Build(new WallCentreline[0], Vector3.zero, 700f, 2300f);

        Assert.IsNotNull(mesh);
        Assert.AreEqual(0, mesh.vertexCount);

        Object.DestroyImmediate(mesh);
    }
}
