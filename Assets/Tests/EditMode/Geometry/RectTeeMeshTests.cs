using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

namespace KitchenDesigner.Tests.Geometry
{
    /// <summary>Тройник упрощён до ДВУХ независимых замкнутых тел, а не единой поверхности
    /// с вырезом. Первая попытка (единая поверхность, вырез в стенке главной трубы под
    /// отвод) дала Т-стыки сразу на всех шести гранях главной трубы: как только одна грань
    /// получает внутреннее членение, её соседи обязаны повторить то же членение на общем
    /// ребре, а те — своим соседям, и 18 граней превращаются в 34. Здесь вместо этого — приём
    /// FoundationStripMesh на стыке осевых фундамента: тела просто ПЕРЕКРЫВАЮТСЯ в
    /// пространстве, шов не сшивается по кромке. Короб отвода начинается ровно на уровне
    /// наружной поверхности стенки главной трубы (BranchBodySpan) и хвостом уходит внутрь
    /// главного короба — воздуховод непрозрачен, перекрытие изнутри никто не увидит.
    ///
    /// Оба тела — это ПРОСТО два вызова уже проверенного BoxRunMesh.Build, поэтому
    /// замкнутость и намотка каждого тела заново не изобретаются: тесты здесь проверяют, что
    /// КОНКРЕТНЫЕ пролёты тройника (MainBodySpan/BranchBodySpan) передают в BoxRunMesh
    /// корректные точки, и что склейка (Build) не портит ни один из двух кусков при
    /// объединении вершин и переносе индексов.</summary>
    public class RectTeeMeshTests
    {
        private const float Tol = 1e-3f;

        private const float MainWidthMm = 200f;
        private const float MainHeightMm = 300f;
        private const float MainLegLengthMm = 500f;
        private const float BranchWidthMm = 100f;
        private const float BranchHeightMm = 100f;
        private const float BranchLegLengthMm = 400f;

        private static readonly Vector3 Hub = Vector3.zero;
        private static readonly Vector3 MainAxis = Vector3.up;
        private static readonly Vector3 BranchAxis = Vector3.right;
        private static readonly Vector3 DepthAxis = Vector3.Cross(MainAxis, BranchAxis);

        private static (Vector3[] Vertices, int[] Triangles) BuildSample() =>
            RectTeeMesh.Build(Hub, MainAxis, BranchAxis,
                MainWidthMm, MainHeightMm, MainLegLengthMm,
                BranchWidthMm, BranchHeightMm, BranchLegLengthMm);

        private static (long, long, long) Key(Vector3 p) => (
            (long)Mathf.Round(p.x / Tol), (long)Mathf.Round(p.y / Tol), (long)Mathf.Round(p.z / Tol));

        private static List<((long, long, long), (long, long, long))> OpenEdges(
            Vector3[] vertices, int[] triangles)
        {
            var edges = new Dictionary<((long, long, long), (long, long, long)), int>();
            for (int i = 0; i + 3 <= triangles.Length; i += 3)
            {
                var a = Key(vertices[triangles[i]]);
                var b = Key(vertices[triangles[i + 1]]);
                var c = Key(vertices[triangles[i + 2]]);
                foreach (var edge in new[] { (a, b), (b, c), (c, a) })
                    edges[edge] = edges.TryGetValue(edge, out int n) ? n + 1 : 1;
            }

            return edges.Where(e =>
                !edges.TryGetValue((e.Key.Item2, e.Key.Item1), out int back)
                || back != e.Value).Select(e => e.Key).ToList();
        }

        [Test]
        public void Build_RejectsABranchAsWideAsTheMainRun()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => RectTeeMesh.Build(
                Hub, MainAxis, BranchAxis, MainWidthMm, MainHeightMm, MainLegLengthMm,
                MainWidthMm, BranchHeightMm, BranchLegLengthMm),
                "отвод шириной вровень с главной трубой торчал бы за её боковые стенки");
        }

        [Test]
        public void Build_RejectsABranchTallerThanTheMainRun()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => RectTeeMesh.Build(
                Hub, MainAxis, BranchAxis, MainWidthMm, MainHeightMm, MainLegLengthMm,
                BranchWidthMm, MainHeightMm + 1f, BranchLegLengthMm),
                "отвод не может быть выше самой трубы, из которой растёт");
        }

        [Test]
        public void Build_RejectsAMainLegShorterThanHalfTheBranchHeight()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => RectTeeMesh.Build(
                Hub, MainAxis, BranchAxis, MainWidthMm, MainHeightMm, BranchHeightMm * 0.25f,
                BranchWidthMm, BranchHeightMm, BranchLegLengthMm),
                "нога короче полувысоты отвода — грубо непропорциональный тройник");
        }

        [Test]
        public void Build_RejectsABranchLegThatDoesNotClearTheMainWall()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => RectTeeMesh.Build(
                Hub, MainAxis, BranchAxis, MainWidthMm, MainHeightMm, MainLegLengthMm,
                BranchWidthMm, BranchHeightMm, MainHeightMm * 0.5f),
                "длина отвода от хаба обязана быть больше полувысоты трубы, иначе короб "
                + "отвода не выступает наружу вовсе");
        }

        [Test]
        public void Build_CombinesTwoFullBoxRunMeshBodies_TwentyFourVerticesEach()
        {
            var (vertices, triangles) = BuildSample();

            Assert.AreEqual(48, vertices.Length,
                "два целых закрытых короба (BoxRunMesh) по 24 вершины — упрощение вместо "
                + "единой поверхности с вырезом");
            Assert.AreEqual(72, triangles.Length, "два короба по 12 треугольников × 3 индекса");
        }

        [Test]
        public void MainBody_IsClosedAndWoundOutward_OnItsOwn()
        {
            var span = RectTeeMesh.MainBodySpan(Hub, MainAxis, MainLegLengthMm);
            var (vertices, triangles) = BoxRunMesh.Build(span.From, span.To, DepthAxis,
                MainWidthMm, MainHeightMm);

            Assert.IsEmpty(OpenEdges(vertices, triangles),
                "тело главной трубы — самостоятельный BoxRunMesh, обязано быть замкнутым само по себе");
            Assert.IsTrue(MeshArea.IsWoundOutward(vertices, triangles, Hub),
                "хаб — центр главного короба, нормаль каждого треугольника обязана смотреть от него");
        }

        [Test]
        public void BranchBody_IsClosedAndWoundOutward_OnItsOwn()
        {
            var span = RectTeeMesh.BranchBodySpan(Hub, BranchAxis, MainHeightMm, BranchLegLengthMm);
            var (vertices, triangles) = BoxRunMesh.Build(span.From, span.To, DepthAxis,
                BranchWidthMm, BranchHeightMm);
            var branchInteriorPoint = (span.From + span.To) * 0.5f;

            Assert.IsEmpty(OpenEdges(vertices, triangles),
                "тело отвода — тоже самостоятельный BoxRunMesh, независимо замкнутое");
            Assert.IsTrue(MeshArea.IsWoundOutward(vertices, triangles, branchInteriorPoint),
                "хаб лежит ВНЕ короба отвода (он начинается только у стенки главной трубы) — "
                + "точка проверки обязана быть своя, середина ЕГО СОБСТВЕННОЙ оси");
        }

        [Test]
        public void BranchBody_StartsFlushWithTheMainWallsOuterSurface()
        {
            var span = RectTeeMesh.BranchBodySpan(Hub, BranchAxis, MainHeightMm, BranchLegLengthMm);

            Assert.AreEqual(MainHeightMm * 0.5f, span.From.x, Tol,
                "ближняя крышка отвода стоит вровень с наружной поверхностью стенки главной "
                + "трубы (mainHeight/2 от хаба по оси отвода), а не у самого хаба");
        }

        [Test]
        public void TwoOverlappingBodies_CombinedMesh_HasNoOpenEdges()
        {
            var (vertices, triangles) = BuildSample();

            Assert.IsEmpty(OpenEdges(vertices, triangles),
                "склейка индексов не обязана портить замкнутость: у главного и отводного тел "
                + "нет общих вершин (они просто перекрываются в пространстве, силуэт как у "
                + "FoundationStripMesh на стыке осевых), поэтому рёбра одного тела не могут "
                + "случайно спариться с рёбрами другого");
        }

        [Test]
        public void SheetMetalAreaM2_FromTheFormula_NotFromTheOverlappingMesh()
        {
            float area = RectTeeMesh.SheetMetalAreaM2(MainWidthMm, MainHeightMm, MainLegLengthMm,
                BranchWidthMm, BranchHeightMm, BranchLegLengthMm);

            float mainLateralM2 = 2f * (MainWidthMm + MainHeightMm) * (2f * MainLegLengthMm) * 1e-6f;
            float openingM2 = BranchWidthMm * BranchHeightMm * 1e-6f;
            float branchSpanMm = BranchLegLengthMm - MainHeightMm * 0.5f;
            float branchLateralM2 = 2f * (BranchWidthMm + BranchHeightMm) * branchSpanMm * 1e-6f;

            Assert.AreEqual(1f, mainLateralM2, 1e-6f,
                "развёртка главной трубы: периметр 2×(200+300)=1000 мм на длину 1000 мм = 1 м²");
            Assert.AreEqual(0.01f, openingM2, 1e-6f, "вырез под отвод: 100×100 мм = 0,01 м²");
            Assert.AreEqual(0.1f, branchLateralM2, 1e-6f,
                "развёртка ВИДИМОЙ части отвода: периметр 2×(100+100)=400 мм на длину "
                + "400−150=250 мм = 0,1 м² — не от хаба, спрятанный внутри трубы кусок "
                + "короба отвода в реальную жесть не режется");
            Assert.AreEqual(1.09f, mainLateralM2 - openingM2 + branchLateralM2, 1e-6f,
                "1 − 0,01 + 0,1 = 1,09 м²");
            Assert.AreEqual(1.09f, area, 1e-6f,
                "формула площади жести не читает меш вовсе — перекрытие тел под коробом "
                + "отвода задвоило бы кусок стены, если бы площадь считалась по треугольникам");
        }
    }
}
