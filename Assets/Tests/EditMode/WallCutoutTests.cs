using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

/// <summary>Вырез под окно в мешe стены: сквозное отверстие, корректная
/// позиция для смещённого окна и обеих ориентаций стены.</summary>
public class WallCutoutTests : SnapTestBase
{
    private static WallMeshBuilder.WindowCutout Cut(float cx, float cy, float hx, float hy) =>
        new WallMeshBuilder.WindowCutout
        {
            centerNorm = new Vector2(cx, cy),
            halfSizeNorm = new Vector2(hx, hy),
        };

    /// <summary>Ни одна вершина грани face (|coord по нормали| = 0.5) не лежит
    /// строго внутри прямоугольника выреза.</summary>
    private static void AssertFaceHasHole(Mesh mesh, int normalAxis, float cu, float cv, float hu, float hv, int uAxis)
    {
        int vAxis = 1; // высота всегда Y
        bool foundEdge = false;
        foreach (var v in mesh.vertices)
        {
            if (Mathf.Abs(Mathf.Abs(v[normalAxis]) - 0.5f) > 1e-4f) continue; // не на этой паре граней
            float u = v[uAxis], w = v[vAxis];
            bool strictlyInside = u > cu - hu + 1e-4f && u < cu + hu - 1e-4f &&
                                  w > cv - hv + 1e-4f && w < cv + hv - 1e-4f;
            Assert.IsFalse(strictlyInside, $"вершина {v} внутри проёма — дыра не вырезана");
            bool onEdge = (Mathf.Abs(u - (cu - hu)) < 1e-4f || Mathf.Abs(u - (cu + hu)) < 1e-4f) &&
                          w >= cv - hv - 1e-4f && w <= cv + hv + 1e-4f;
            if (onEdge) foundEdge = true;
        }
        Assert.IsTrue(foundEdge, "нет вершин по границе проёма — вырез не построен");
    }

    [Test]
    public void Builder_CutsThroughZ_ForOffCenterWindow()
    {
        var mesh = WallMeshBuilder.Build(new List<WallMeshBuilder.WindowCutout> { Cut(0.2f, -0.1f, 0.1f, 0.2f) });
        // Дыра на обеих гранях ±Z и именно в заданном месте (раньше центр
        // делился на полуширину стены дважды и вырез уезжал к центру).
        AssertFaceHasHole(mesh, normalAxis: 2, cu: 0.2f, cv: -0.1f, hu: 0.1f, hv: 0.2f, uAxis: 0);
    }

    [Test]
    public void Builder_CutsThroughX_WhenThicknessAlongX()
    {
        var mesh = WallMeshBuilder.Build(
            new List<WallMeshBuilder.WindowCutout> { Cut(0.2f, -0.1f, 0.1f, 0.2f) },
            thicknessAlongX: true);
        // Для стены, повёрнутой длиной вдоль Z, дыра — сквозь X, ширина — по Z.
        AssertFaceHasHole(mesh, normalAxis: 0, cu: 0.2f, cv: -0.1f, hu: 0.1f, hv: 0.2f, uAxis: 2);
    }

    [Test]
    public void Builder_BackFace_HasRectangularHole_NotFullWidthGap()
    {
        // Регресс: задняя грань строилась с перевёрнутым диапазоном X, из-за
        // чего вырез растягивался на всю ширину. У корректного меша на грани
        // z=-0.5 должны быть вершины между вырезом и краем стены (простенки).
        var mesh = WallMeshBuilder.Build(new List<WallMeshBuilder.WindowCutout> { Cut(0f, 0f, 0.15f, 0.24f) });

        bool hasBackPier = false;
        foreach (var v in mesh.vertices)
        {
            if (Mathf.Abs(v.z + 0.5f) > 1e-4f) continue; // только задняя грань
            // Вершина на границе выреза (x = ±0.15) в полосе высоты окна.
            if (Mathf.Abs(Mathf.Abs(v.x) - 0.15f) < 1e-4f && Mathf.Abs(v.y) <= 0.24f + 1e-4f)
                hasBackPier = true;
        }
        Assert.IsTrue(hasBackPier, "на задней грани нет простенков по бокам проёма — вырез во всю ширину");
    }

    [Test]
    public void Wall_RebuildMesh_UsesWindowOffsetAlongWall()
    {
        // Стена длиной вдоль X, окно смещено на +0.6 м от центра стены.
        var wallGo = ElementFactory.CreateWall(
            new Vector3Int(3000, 2500, 100), "Wall_Cut", new Vector3(0f, 1.25f, -1.5f));
        _spawned.Add(wallGo);
        var winGo = ElementFactory.CreateWindow(
            new Vector3Int(900, 1200, 100), "Win_Cut", new Vector3(0.6f, 1.2f, -1.5f));
        _spawned.Add(winGo);
        winGo.GetComponent<WindowElement>()!.SnapToWall();

        var mesh = wallGo.GetComponent<MeshFilter>()!.sharedMesh;
        Assert.IsNotNull(mesh);

        // Нормализованный центр проёма: 0.6/3.0 = 0.2 по ширине,
        // (1.2 − 1.25)/2.5 = −0.02 по высоте; полуразмеры 0.45/3, 0.6/2.5.
        AssertFaceHasHole(mesh!, normalAxis: 2,
            cu: 0.2f, cv: -0.02f, hu: 0.45f / 3f, hv: 0.6f / 2.5f, uAxis: 0);
    }
}
