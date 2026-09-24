using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

namespace KitchenDesigner.Tests.Geometry
{
    /// <summary>Переход между двумя разными прямоугольными сечениями — прямой лофт, без
    /// шва: каждый борт соединяет ребро ближнего прямоугольника с СООТВЕТСТВУЮЩИМ ребром
    /// дальнего (обе пары рёбер — чистые кратные одной и той же оси), поэтому четырёхугольник
    /// остаётся плоским, даже когда ширина и высота меняются independently. BoxRunMesh —
    /// частный случай (widthA=widthB, heightA=heightB) и теперь делегирует сюда.</summary>
    public class RectTransitionMeshTests
    {
        private const float WidthAMm = 100f;
        private const float HeightAMm = 200f;
        private const float WidthBMm = 300f;
        private const float HeightBMm = 150f;
        private const float LengthMm = 400f;

        private static readonly Vector3 From = Vector3.zero;
        private static readonly Vector3 To = new Vector3(0f, 0f, LengthMm);

        private static (Vector3[] Vertices, int[] Triangles) BuildSample() =>
            RectTransitionMesh.Build(From, To, Vector3.right,
                WidthAMm, HeightAMm, WidthBMm, HeightBMm);

        [Test]
        public void Build_HasSixFaces_TwentyFourVerticesAndTwelveTriangles()
        {
            var (vertices, triangles) = BuildSample();

            Assert.AreEqual(24, vertices.Length, "6 граней × 4 вершины на плоскую нормаль");
            Assert.AreEqual(36, triangles.Length, "6 граней × 2 треугольника × 3 индекса");
        }

        [Test]
        public void Build_BoundingBoxTakesTheWiderEndOnEachAxis()
        {
            var (vertices, _) = BuildSample();

            var min = vertices[0];
            var max = vertices[0];
            foreach (var vertex in vertices)
            {
                min = Vector3.Min(min, vertex);
                max = Vector3.Max(max, vertex);
            }

            Assert.AreEqual(WidthBMm, max.x - min.x, 1e-3f,
                "дальний торец шире (300 против 100) — он и задаёт габарит по X");
            Assert.AreEqual(HeightAMm, max.y - min.y, 1e-3f,
                "ближний торец выше (200 против 150) — он и задаёт габарит по Y");
            Assert.AreEqual(LengthMm, max.z - min.z, 1e-3f, "длина перехода вдоль Z");
        }

        [Test]
        public void Build_IsWoundConsistentlyOutward()
        {
            var (vertices, triangles) = BuildSample();
            var interiorPoint = (From + To) * 0.5f;

            Assert.IsTrue(MeshArea.IsWoundOutward(vertices, triangles, interiorPoint),
                "переход выпуклый (лофт двух прямоугольников без скручивания) — середина оси "
                + "гарантированно внутри, и нормаль КАЖДОГО треугольника обязана смотреть от неё");
        }

        [Test]
        public void Build_ClosedSurfaceArea_MatchesTheHandCount()
        {
            var (vertices, triangles) = BuildSample();

            float capsMm2 = WidthAMm * HeightAMm + WidthBMm * HeightBMm;
            float slantAlongHeightMm = Mathf.Sqrt(LengthMm * LengthMm
                + (HeightBMm - HeightAMm) * (HeightBMm - HeightAMm) * 0.25f);
            float slantAlongWidthMm = Mathf.Sqrt(LengthMm * LengthMm
                + (WidthBMm - WidthAMm) * (WidthBMm - WidthAMm) * 0.25f);
            float lateralMm2 = (WidthAMm + WidthBMm) * slantAlongHeightMm
                + (HeightAMm + HeightBMm) * slantAlongWidthMm;
            float expectedMm2 = capsMm2 + lateralMm2;

            Assert.AreEqual(369620.9f, expectedMm2, 1f,
                "контрольное число руками: два торца (20 000+45 000) плюс четыре трапеции "
                + "((100+300)×√(400²+25²) + (200+150)×√(400²+100²))");
            Assert.AreEqual(expectedMm2, MeshArea.TotalMm2(vertices, triangles), 1f,
                "сумма площадей треугольников готового меша обязана сойтись с формулой сечения");
        }

        [Test]
        public void ClosedSurfaceAreaMm2_AgreesWithTheBuiltMesh()
        {
            float formula = RectTransitionMesh.ClosedSurfaceAreaMm2(From, To,
                WidthAMm, HeightAMm, WidthBMm, HeightBMm);
            var (vertices, triangles) = BuildSample();

            Assert.AreEqual(formula, MeshArea.TotalMm2(vertices, triangles), 1f,
                "формула площади (для будущей ведомости жести) не должна разойтись с мешем");
        }

        [Test]
        public void EqualEndSections_MatchBoxRunMeshExactly()
        {
            var transition = RectTransitionMesh.Build(From, To, Vector3.right,
                WidthAMm, HeightAMm, WidthAMm, HeightAMm);
            var box = BoxRunMesh.Build(From, To, Vector3.right, WidthAMm, HeightAMm);

            CollectionAssert.AreEqual(box.Triangles, transition.Triangles,
                "BoxRunMesh — частный случай перехода с одинаковыми торцами, индексы обязаны совпасть");
            for (int i = 0; i < box.Vertices.Length; i++)
                Assert.AreEqual(box.Vertices[i], transition.Vertices[i],
                    "и вершины обязаны совпасть поточечно, а не только по счёту");
        }

        [Test]
        public void UnfoldedAreaM2_OfBoxRunMesh_MatchesTransitionsLateralArea()
        {
            float boxArea = BoxRunMesh.UnfoldedAreaM2(From, To, WidthAMm, HeightAMm);
            float transitionArea = RectTransitionMesh.LateralAreaM2(From, To,
                WidthAMm, HeightAMm, WidthAMm, HeightAMm);

            Assert.AreEqual(transitionArea, boxArea, 1e-6f,
                "при равных торцах развёртка перехода обязана свестись к периметру × длину");
        }
    }
}
