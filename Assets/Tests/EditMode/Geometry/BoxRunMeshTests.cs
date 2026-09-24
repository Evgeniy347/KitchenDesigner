using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

namespace KitchenDesigner.Tests.Geometry
{
    /// <summary>Прямой участок прямоугольного воздуховода 100×200×1000 мм вдоль Z: 6 граней,
    /// каждая со своими 4 вершинами (плоская нормаль на грань, как у остальных мешей ядра) —
    /// 24 вершины, 12 треугольников. Площадь замкнутой поверхности и развёртка посчитаны
    /// руками отдельно от кода, который их производит.</summary>
    public class BoxRunMeshTests
    {
        private const float WidthMm = 100f;
        private const float HeightMm = 200f;
        private const float LengthMm = 1000f;

        private static (Vector3[] Vertices, int[] Triangles) BuildSample() =>
            BoxRunMesh.Build(Vector3.zero, new Vector3(0f, 0f, LengthMm),
                Vector3.right, WidthMm, HeightMm);

        [Test]
        public void Build_HasSixFaces_TwentyFourVerticesAndTwelveTriangles()
        {
            var (vertices, triangles) = BuildSample();

            Assert.AreEqual(24, vertices.Length, "6 граней × 4 вершины на плоскую нормаль");
            Assert.AreEqual(36, triangles.Length, "6 граней × 2 треугольника × 3 индекса");
        }

        [Test]
        public void Build_BoundingBoxMatchesWidthHeightAndLength()
        {
            var (vertices, _) = BuildSample();

            var min = vertices[0];
            var max = vertices[0];
            foreach (var vertex in vertices)
            {
                min = Vector3.Min(min, vertex);
                max = Vector3.Max(max, vertex);
            }

            Assert.AreEqual(WidthMm, max.x - min.x, 1e-3f, "ширина вдоль оси X (widthAxis = right)");
            Assert.AreEqual(HeightMm, max.y - min.y, 1e-3f, "высота вдоль оси Y (cross(Z, X) = Y)");
            Assert.AreEqual(LengthMm, max.z - min.z, 1e-3f, "длина вдоль оси прогона Z");
        }

        [Test]
        public void Build_ClosedSurfaceArea_MatchesTheHandCount()
        {
            var (vertices, triangles) = BuildSample();

            float expectedMm2 = 2f * (WidthMm * HeightMm)
                + 2f * (LengthMm * WidthMm)
                + 2f * (LengthMm * HeightMm);

            Assert.AreEqual(expectedMm2, MeshArea.TotalMm2(vertices, triangles), 1e-2f,
                "два торца 100×200 + два борта 1000×100 + два борта 1000×200 = 640 000 мм²");
        }

        [Test]
        public void Build_IsWoundConsistentlyOutward()
        {
            var (vertices, triangles) = BuildSample();
            var interiorPoint = new Vector3(0f, 0f, LengthMm * 0.5f);

            Assert.IsTrue(MeshArea.IsWoundOutward(vertices, triangles, interiorPoint),
                "коробка выпуклая — середина её оси гарантированно внутри, и нормаль КАЖДОГО "
                + "треугольника обязана смотреть от неё, а не внутрь тела");
        }

        [Test]
        public void UnfoldedAreaM2_ExcludesTheEndCaps()
        {
            float areaM2 = BoxRunMesh.UnfoldedAreaM2(Vector3.zero, new Vector3(0f, 0f, LengthMm),
                WidthMm, HeightMm);

            Assert.AreEqual(0.6f, areaM2, 1e-4f,
                "развёртка — периметр 2×(100+200)=600 мм на длину 1000 мм = 600 000 мм² = 0,6 м²; "
                + "торцы не режутся из полосы жести и в развёртку не входят");
        }
    }
}
