using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

namespace KitchenDesigner.Tests.Geometry
{
    /// <summary>Одна вальмовая или скатная плоскость крыши — это выпуклый многоугольник (3
    /// точки для треугольного вальма, 4 для прямоугольного или трапецеидального ската),
    /// вытянутый по толщине кровельного пирога. Обход тот же, что у ProfileExtrusionMesh:
    /// верхняя крышка → нижняя крышка → боковые стенки по кольцу рёбер, только сама «крышка»
    /// здесь не замкнутый профиль из N сегментов, а простой веерный многоугольник без
    /// центральной вершины — три-четыре точки веера не нуждаются в ней.
    ///
    /// Верх и низ идут по ОДНИМ И ТЕМ ЖЕ входным точкам (низ — те же точки минус нормаль ×
    /// толщина), поэтому боковые стенки стыкуются с крышками по координатам без арифметической
    /// погрешности — проверка замкнутости ниже полагается именно на это.</summary>
    public class RoofPlaneMeshTests
    {
        private static readonly Vector3[] Rectangle2By3AtY1 =
        {
            new Vector3(0f, 1f, 0f),
            new Vector3(0f, 1f, 3f),
            new Vector3(2f, 1f, 3f),
            new Vector3(2f, 1f, 0f),
        };

        private static readonly Vector3[] Triangle4By3AtY0 =
        {
            new Vector3(0f, 0f, 0f),
            new Vector3(4f, 0f, 0f),
            new Vector3(0f, 0f, 3f),
        };

        [Test]
        public void RoofPlaneMesh_Rectangle_HasTheHandCountedVertexAndTriangleCounts()
        {
            var mesh = new RoofPlaneMesh(Rectangle2By3AtY1, 0.1f);

            Assert.AreEqual(24, mesh.Positions.Length,
                "4 (верх) + 4 (низ) + 4 стороны × 4 вершины = 24 — каждая грань несёт свои "
                + "вершины ради плоского затенения, как ProfileExtrusionMesh");
            Assert.AreEqual(36, mesh.Triangles.Length,
                "2 (верх) + 2 (низ) + 4 стороны × 2 треугольника = 8, ×3 индекса = 36 "
                + "(12 треугольников)");
        }

        [Test]
        public void RoofPlaneMesh_Rectangle_TopFaceKeepsTheInputPointsExactly_BottomDropsByThickness()
        {
            var mesh = new RoofPlaneMesh(Rectangle2By3AtY1, 0.1f);

            for (int i = 0; i < 4; i++)
                Assert.AreEqual(Rectangle2By3AtY1[i], mesh.Positions[i],
                    "верхняя крышка — это ВХОДНЫЕ точки один в один, без пересчёта");

            Assert.AreEqual(Vector3.up, mesh.Normal, "обход (0,0)→(0,3)→(2,3)→(2,0) при "
                + "взгляде сверху идёт против часовой стрелки — нормаль вверх");

            for (int i = 0; i < 4; i++)
            {
                var expectedBottom = Rectangle2By3AtY1[i] - Vector3.up * 0.1f;
                Assert.AreEqual(expectedBottom, mesh.Positions[4 + i],
                    "нижняя крышка — те же точки, смещённые на толщину ПРОТИВ нормали");
            }
        }

        [Test]
        public void RoofPlaneMesh_Triangle_HasTheHandCountedVertexAndTriangleCounts()
        {
            var mesh = new RoofPlaneMesh(Triangle4By3AtY0, 0.05f);

            Assert.AreEqual(18, mesh.Positions.Length,
                "3 (верх) + 3 (низ) + 3 стороны × 4 вершины = 18 — треугольный вальм устроен "
                + "так же, как прямоугольный скат, просто без четвёртой точки веера");
            Assert.AreEqual(24, mesh.Triangles.Length,
                "1 (верх) + 1 (низ) + 3 стороны × 2 треугольника = 8, ×3 индекса = 24");
        }

        [TestCaseSource(nameof(Shapes))]
        public void RoofPlaneMesh_IsClosed_EveryDirectedEdgeMeetsExactlyOneEdgeGoingTheOtherWay(
            Vector3[] boundary)
        {
            var mesh = new RoofPlaneMesh(boundary, 0.1f);
            var edges = new Dictionary<((float, float, float), (float, float, float)), int>();

            for (int t = 0; t < mesh.Triangles.Length; t += 3)
            {
                var a = Key(mesh.Positions[mesh.Triangles[t]]);
                var b = Key(mesh.Positions[mesh.Triangles[t + 1]]);
                var c = Key(mesh.Positions[mesh.Triangles[t + 2]]);
                foreach (var edge in new[] { (a, b), (b, c), (c, a) })
                    edges[edge] = edges.TryGetValue(edge, out int n) ? n + 1 : 1;
            }

            var open = edges.Where(e =>
                !edges.TryGetValue((e.Key.Item2, e.Key.Item1), out int back)
                || back != e.Value).ToList();

            Assert.IsEmpty(open,
                "плоскость крыши рваная: " + open.Count + " направленных рёбер без встречного. "
                + "Крышки и боковые стенки построены по одним и тем же координатам, поэтому "
                + "щель означает разошедшийся обход, а не погрешность округления");
        }

        [TestCaseSource(nameof(Shapes))]
        public void RoofPlaneMesh_EveryTriangle_WindsTheSameWayItsVertexNormalsPoint(Vector3[] boundary)
        {
            var mesh = new RoofPlaneMesh(boundary, 0.1f);

            for (int t = 0; t < mesh.Triangles.Length; t += 3)
            {
                int ia = mesh.Triangles[t];
                int ib = mesh.Triangles[t + 1];
                int ic = mesh.Triangles[t + 2];
                var geometric = Vector3.Cross(
                    mesh.Positions[ib] - mesh.Positions[ia],
                    mesh.Positions[ic] - mesh.Positions[ia]);
                var declared = mesh.Normals[ia] + mesh.Normals[ib] + mesh.Normals[ic];

                if (declared.sqrMagnitude < Tolerance.EpsilonSqr) continue;

                Assert.Greater(Vector3.Dot(geometric.normalized, declared.normalized), 0f,
                    "треугольник " + t / 3 + " намотан против своих же нормалей — такая грань "
                    + "исчезает с той стороны, с которой на неё смотрят");
            }
        }

        [Test]
        public void RoofPlaneMesh_FewerThanThreePoints_ReturnsEmptyArrays_NotACrash()
        {
            var mesh = new RoofPlaneMesh(new[] { Vector3.zero, Vector3.right }, 0.1f);

            Assert.AreEqual(0, mesh.Positions.Length,
                "две точки не образуют многоугольник — строить нечего, а не бросать исключение");
            Assert.AreEqual(0, mesh.Triangles.Length, "без вершин не может быть треугольников");
            Assert.AreEqual(Vector3.zero, mesh.Normal,
                "вырожденный вход — нулевая нормаль, а не деление на ноль внутри Cross().normalized");
        }

        [Test]
        public void RoofPlaneMesh_NullBoundary_ReturnsEmptyArrays_NotACrash()
        {
            var mesh = new RoofPlaneMesh(null, 0.1f);

            Assert.AreEqual(0, mesh.Positions.Length,
                "контур ещё не собран на вызывающей стороне — пустой результат, а не NullReferenceException");
            Assert.AreEqual(0, mesh.Triangles.Length, "то же самое для треугольников");
        }

        [Test]
        public void RoofPlaneMesh_ZeroThickness_CollapsesTopAndBottomToTheSamePlane()
        {
            var mesh = new RoofPlaneMesh(Rectangle2By3AtY1, 0f);

            for (int i = 0; i < 4; i++)
                Assert.AreEqual(mesh.Positions[i], mesh.Positions[4 + i],
                    "нулевая толщина — вырожденная плита, верх и низ совпадают, но массивы "
                    + "не падают и не теряют вершины");
        }

        private static IEnumerable<Vector3[]> Shapes()
        {
            yield return Rectangle2By3AtY1;
            yield return Triangle4By3AtY0;
        }

        private static (float, float, float) Key(Vector3 v) =>
            (Round(v.x), Round(v.y), Round(v.z));

        private static float Round(float v) => Mathf.Round(v * 1000f) / 1000f;
    }
}
