using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

namespace KitchenDesigner.Tests.Geometry
{
    /// <summary>Прямоугольный отвод 90° = один шов под 45° (не дуга): два прямых куска,
    /// каждый обрезан ОДНОЙ и той же плоскостью через хаб, и сшиты по этому резу. В
    /// плоскости изгиба (u вдоль axisA, v вдоль axisB) это семиугольник с ОДНИМ вогнутым
    /// углом — внутренний угол отвода, где стенки сходятся под прямым углом без реза.
    /// Форма и площадь проверены дважды: вычитанием (прямоугольник A + прямоугольник B −
    /// перекрытие − два обрезанных уголка) и формулой Гаусса по семи точкам — оба метода
    /// дают 4×L×hh − 2×hh² на крышку; здесь же третья, независимая проверка — сумма
    /// площадей треугольников готового меша.</summary>
    public class RectElbowMeshTests
    {
        private const float WidthMm = 100f;
        private const float HeightMm = 200f;
        private const float LegLengthMm = 300f;

        private static (Vector3[] Vertices, int[] Triangles) BuildSample() =>
            RectElbowMesh.Build(Vector3.zero, Vector3.down, Vector3.right,
                WidthMm, HeightMm, LegLengthMm);

        [Test]
        public void Build_HasNineFaces_FortyTwoVerticesAndTwentyFourTriangles()
        {
            var (vertices, triangles) = BuildSample();

            Assert.AreEqual(42, vertices.Length,
                "2 крышки-семиугольника × 7 вершин + 7 бортов × 4 вершины на плоскую нормаль");
            Assert.AreEqual(72, triangles.Length,
                "2 крышки × 5 треугольников (веер от вогнутого угла) + 7 бортов × 2 = 24 "
                + "треугольника × 3 индекса");
        }

        [Test]
        public void Build_BoundingBoxMatchesTheLegSpanAndTheWidth()
        {
            var (vertices, _) = BuildSample();

            var min = vertices[0];
            var max = vertices[0];
            foreach (var vertex in vertices)
            {
                min = Vector3.Min(min, vertex);
                max = Vector3.Max(max, vertex);
            }

            float span = LegLengthMm + HeightMm * 0.5f;
            Assert.AreEqual(span, max.x - min.x, 1e-2f,
                "вдоль axisB (мировой X): от линии митры (−hh) до дальнего торца (+L)");
            Assert.AreEqual(span, max.y - min.y, 1e-2f,
                "вдоль axisA (мировой −Y): тот же размах, отвод симметричен по построению");
            Assert.AreEqual(WidthMm, max.z - min.z, 1e-2f,
                "поперёк плоскости изгиба (мировой Z) — ровно заданная ширина воздуховода");
        }

        [Test]
        public void Build_ClosedSurfaceArea_MatchesTheHandCount()
        {
            var (vertices, triangles) = BuildSample();

            const float hh = HeightMm * 0.5f;
            float capAreaMm2 = 4f * LegLengthMm * hh - 2f * hh * hh;
            float mitreLengthMm = 2f * hh * Mathf.Sqrt(2f);
            float sideRimLengthMm = 4f * LegLengthMm + mitreLengthMm;
            float expectedMm2 = 2f * capAreaMm2 + WidthMm * sideRimLengthMm;

            Assert.AreEqual(348284.27f, expectedMm2, 1f,
                "контрольное число руками: 2×(4×300×100−2×100²) + 100×(4×300+2×100×√2)");
            Assert.AreEqual(expectedMm2, MeshArea.TotalMm2(vertices, triangles), 1f,
                "сумма площадей треугольников готового меша обязана сойтись с формулой сечения");
        }

        [Test]
        public void ClosedSurfaceAreaMm2_AgreesWithTheBuiltMesh()
        {
            float formula = RectElbowMesh.ClosedSurfaceAreaMm2(WidthMm, HeightMm, LegLengthMm);
            var (vertices, triangles) = BuildSample();

            Assert.AreEqual(formula, MeshArea.TotalMm2(vertices, triangles), 1f,
                "формула площади (для будущей ведомости жести) не должна разойтись с мешем");
        }
    }
}
