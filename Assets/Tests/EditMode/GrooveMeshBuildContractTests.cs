using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

/// <summary>Контракт сборки меша детали: сквозные вырезы против пазов, ось
/// выреза и перестановка осей, деление на сабмеши.
///
/// Здесь живут причины, которые раньше были комментариями в GrooveMesh.cs:
/// вырез старше паза, стенки выреза идут в тело детали, паз существует только
/// в пласти ±Z, а перестановка осей зеркальна и потому разворачивает обход
/// треугольников.</summary>
public class GrooveMeshBuildContractTests
{
    private static readonly Vector3Int Sheet = new Vector3Int(800, 400, 18);
    private static readonly Vector3Int Countertop = new Vector3Int(1200, 40, 600);

    private readonly List<Mesh> _meshes = new List<Mesh>();

    [TearDown]
    public void TearDown()
    {
        foreach (var m in _meshes) if (m != null) Object.DestroyImmediate(m);
        _meshes.Clear();
    }

    private Mesh Build(Vector3Int dims, IReadOnlyList<GrooveSpec>? grooves = null,
        IReadOnlyList<GrooveMesh.Rect2>? holes = null, int holeAxis = 2)
    {
        var mesh = GrooveMesh.Build(dims, grooves, holes, holeAxis, 0, out _);
        _meshes.Add(mesh);
        return mesh;
    }

    private static GrooveMesh.Rect2 Hole(float xMin, float xMax, float yMin, float yMax)
        => new GrooveMesh.Rect2 { xMin = xMin, xMax = xMax, yMin = yMin, yMax = yMax };

    [Test]
    public void BlindGroove_OnAPartShorterThanItsTwoBlindEnds_IsSkipped()
    {
        int tooShort = 2 * AppConstants.GROOVE_BLIND_END_MM - 1;
        var rect = GrooveMesh.ComputeRect(new Vector3Int(tooShort, 400, 18),
            new GrooveSpec(GrooveKind.Blind, GrooveSide.Top));

        Assert.IsFalse(rect.IsValid,
            $"глухой паз укорочен с обоих торцов на {AppConstants.GROOVE_BLIND_END_MM} мм — "
            + "на детали короче этой пары ему не остаётся длины, и он не строится вовсе");
    }

    // --- Сквозные вырезы ---

    [Test]
    public void ClampHoles_TrimsToTheFace_AndDropsTheDegenerateOnes()
    {
        var clamped = GrooveMesh.ClampHoles(new List<GrooveMesh.Rect2>
        {
            Hole(-2f, 2f, -0.1f, 0.1f),
            Hole(0.1f, 0.1f, -0.2f, 0.2f),
            Hole(0.3f, -0.3f, -0.2f, 0.2f),
        });

        Assert.AreEqual(1, clamped.Count,
            "нулевой по ширине и вывернутый вырез — не вырезы: решётка получила бы "
            + "линию реза, за которой нет ни одной клетки");
        Assert.AreEqual(-0.5f, clamped[0].xMin, 1e-5f, "вырез шире детали обрезан по её краю");
        Assert.AreEqual(0.5f, clamped[0].xMax, 1e-5f);
    }

    [Test]
    public void Build_WithAHoleAndNoGrooves_StaysSingleMaterial()
    {
        var mesh = Build(Sheet, holes: new List<GrooveMesh.Rect2> { Hole(-0.2f, 0.2f, -0.2f, 0.2f) });

        Assert.AreEqual(1, mesh.subMeshCount,
            "стенки выреза идут в сабмеш тела: деталь с одной лишь врезкой не должна "
            + "требовать второго материала — врезанный прибор их всё равно закрывает");
    }

    [Test]
    public void Build_HoleOverAGroove_LeavesNoPocketFloorInsideTheHole()
    {
        var spec = new GrooveSpec(GrooveKind.Through, GrooveSide.Top);
        var grooveRect = GrooveMesh.ComputeRect(Sheet, spec);
        float grooveY = (grooveRect.yMin + grooveRect.yMax) * 0.5f;
        var hole = Hole(-0.2f, 0.2f, grooveRect.yMin, grooveRect.yMax);

        var mesh = Build(Sheet, new List<GrooveSpec> { spec },
            new List<GrooveMesh.Rect2> { hole });

        var verts = mesh.vertices;
        for (int sub = 0; sub < mesh.subMeshCount; sub++)
        {
            var tris = mesh.GetTriangles(sub);
            for (int t = 0; t < tris.Length; t += 3)
            {
                var c = (verts[tris[t]] + verts[tris[t + 1]] + verts[tris[t + 2]]) / 3f;
                bool insideHole = c.x > hole.xMin + 1e-4f && c.x < hole.xMax - 1e-4f
                                  && c.y > hole.yMin + 1e-4f && c.y < hole.yMax - 1e-4f;
                Assert.IsFalse(insideHole,
                    $"внутри выреза (y≈{grooveY:F3}) не должно остаться НИ пласти, ни дна "
                    + "кармана: там деталь прорезана насквозь, и вырез старше паза");
            }
        }
    }

    // --- Ось выреза и перестановка ---

    [Test]
    public void Build_HoleAcrossAnotherAxis_DropsTheGrooves()
    {
        var grooves = new List<GrooveSpec> { new GrooveSpec(GrooveKind.Through, GrooveSide.Top) };
        var holes = new List<GrooveMesh.Rect2> { Hole(-0.2f, 0.2f, -0.2f, 0.2f) };

        var mesh = GrooveMesh.Build(Countertop, grooves, holes, 1, 0, out var layout);
        _meshes.Add(mesh);

        Assert.AreEqual(-1, layout.Grooves,
            "паз живёт только в пласти ±Z, и перестановка увела бы его оттуда; деталь со "
            + "сквозным вырезом поперёк другой оси — это столешница-короб, пазов в ней нет");
        Assert.AreEqual(1, mesh.subMeshCount);
    }

    [Test]
    public void Build_PermutedMesh_KeepsItsNormalsFacingOutwards()
    {
        var mesh = Build(Countertop,
            holes: new List<GrooveMesh.Rect2> { Hole(-0.2f, 0.2f, -0.2f, 0.2f) },
            holeAxis: 1);

        var verts = mesh.vertices;
        var normals = mesh.normals;
        int topFaceVerts = 0;
        for (int i = 0; i < verts.Length; i++)
        {
            if (Mathf.Abs(verts[i].y - 0.5f) > 1e-4f) continue;
            if (Mathf.Abs(normals[i].y) < 0.9f) continue;
            topFaceVerts++;
            Assert.Greater(normals[i].y, 0f,
                "перестановка осей зеркальна: без разворота обхода треугольников нормали "
                + "смотрели бы ВНУТРЬ детали и столешница стала бы невидимой снаружи");
        }
        Assert.Greater(topFaceVerts, 0, "верхняя пласть у столешницы должна быть");
    }

    // --- Общий материал паза ---

    [Test]
    public void GrooveMaterial_IsOneInstanceForTheWholeScene()
    {
        Assert.AreSame(GrooveMesh.GrooveMaterial(), GrooveMesh.GrooveMaterial(),
            "тёмный материал паза общий на все детали — иначе каждая деталь с пазом "
            + "тянула бы за собой собственный материал");
    }
}
