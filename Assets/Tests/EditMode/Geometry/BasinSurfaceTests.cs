using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

namespace KitchenDesigner.Tests.Geometry
{
    /// <summary>Чаша, открытая сверху: наружный скруглённый корпус, плоский
    /// борт по периметру и внутренняя полость со скруглённым дном. Из неё
    /// собрана ванна; тот же силуэт нужен душевому поддону и мойке, поэтому
    /// класс назван по ФОРМЕ, а не по изделию.
    ///
    /// Почему это не SoftSlabSurface с перевёрнутыми нормалями. Мягкая плита —
    /// ЗАМКНУТЫЙ объём с крышкой сверху; у ванны сверху дырка, и её крышка
    /// выглядела бы как налитая до краёв вода. Зато обход колец у обеих
    /// фигур один и тот же: список сечений SoftSlabRing (отступ внутрь,
    /// высота, радиальная и вертикальная доли нормали), кольцо строится
    /// RoundedRectProfile.Ring по контуру, сжатому внутрь на свой отступ.
    /// Отсюда и берётся главное свойство: сжатие скруглённого прямоугольника
    /// внутрь на d — это стороны минус 2d и радиусы минус d, поэтому чаша
    /// круглой ванны остаётся КРУГЛОЙ, а не превращается в квадрат с фасками.
    ///
    /// Весь обход — это ОДИН путь: низ корпуса → верх корпуса → внутрь по
    /// борту → вниз по стенке чаши → дуга на дно. Поэтому лента треугольников
    /// между соседними кольцами строится одним и тем же шаблоном, взятым у
    /// SoftSlabSurface, и намотка на всех трёх участках получается согласованной
    /// сама собой. Проверять это глазами нельзя: перевёрнутая лента в Unity не
    /// падает и не светится — она просто исчезает при взгляде снаружи. Поэтому
    /// здесь два теста: намотка обязана совпадать с нормалями на КАЖДОМ
    /// треугольнике, и поверхность обязана быть замкнутой.
    ///
    /// Замкнутость проверяется по КООРДИНАТАМ, а не по индексам: на жёстких
    /// кромках (верх корпуса, внешний край борта) кольца намеренно
    /// продублированы, чтобы нормаль ломалась, — по индексам такая поверхность
    /// выглядит рваной, хотя дырки в ней нет.
    ///
    /// Все радиусы в тестах строго положительные не для красоты: при радиусе 0
    /// RoundedRectProfile складывает дугу угла в одну точку, кольцо получает
    /// совпадающие вершины, и вырожденные треугольники появляются законно —
    /// это свойство контура, а не ошибка ленты.</summary>
    public class BasinSurfaceTests
    {
        private const float Tol = 1e-4f;

        private const float Width = 1.7f;
        private const float Depth = 0.7f;
        private const float Height = 0.6f;
        private const float Rim = 0.04f;
        private const float BowlDepth = 0.45f;
        private const float Fillet = 0.08f;
        private const float ShellRadius = 0.16f;

        private static BasinSurface Tub(float fillet = Fillet, float rim = Rim,
            float radius = ShellRadius) => new BasinSurface(
            Width, Depth, Height, CornerRadii.Uniform(radius), rim, BowlDepth, fillet);

        private static (long, long, long) Key(Vector3 p) => (
            Mathf.RoundToInt(p.x / Tol), Mathf.RoundToInt(p.y / Tol),
            Mathf.RoundToInt(p.z / Tol));

        private static int DegenerateTriangles(BasinSurface surface)
        {
            int count = 0;
            for (int t = 0; t < surface.Triangles.Length; t += 3)
            {
                var a = surface.Positions[surface.Triangles[t]];
                var b = surface.Positions[surface.Triangles[t + 1]];
                var c = surface.Positions[surface.Triangles[t + 2]];
                if (Vector3.Cross(b - a, c - a).magnitude < Tolerance.EpsilonSqr) count++;
            }
            return count;
        }

        [Test]
        public void EveryRing_LiesExactlyOnTheContourOffsetInwardByItsOwnInset()
        {
            var surface = Tub();
            int perRing = surface.PointsPerRing;

            for (int ring = 0; ring < surface.Sections.Count; ring++)
            {
                float inset = surface.Sections[ring].Inset;

                for (int j = 0; j < perRing; j++)
                {
                    var p = surface.Positions[ring * perRing + j];
                    Assert.AreEqual(0f, RoundedRectProfile.SignedDistance(
                            new Vector2(p.x, p.z), Width - 2f * inset, Depth - 2f * inset,
                            surface.Radii.Inset(inset)), Tol,
                        "кольцо " + ring + " ушло с эквидистанты. Именно точный офсет "
                        + "(стороны −2d, радиусы −d) делает чашу подобной корпусу: борт "
                        + "получает одинаковую ширину и на прямом участке, и в углу");
                }
            }
        }

        [Test]
        public void TheSurfaceIsClosed_EveryEdgeMeetsExactlyOneEdgeGoingTheOtherWay()
        {
            var surface = Tub();
            var edges = new Dictionary<((long, long, long), (long, long, long)), int>();

            for (int t = 0; t < surface.Triangles.Length; t += 3)
            {
                var a = Key(surface.Positions[surface.Triangles[t]]);
                var b = Key(surface.Positions[surface.Triangles[t + 1]]);
                var c = Key(surface.Positions[surface.Triangles[t + 2]]);
                foreach (var edge in new[] { (a, b), (b, c), (c, a) })
                    edges[edge] = edges.TryGetValue(edge, out int n) ? n + 1 : 1;
            }

            var open = edges.Where(e =>
                !edges.TryGetValue((e.Key.Item2, e.Key.Item1), out int back)
                || back != e.Value).ToList();

            Assert.IsEmpty(open,
                "поверхность рваная: " + open.Count + " направленных рёбер без встречного. "
                + "Ванна — замкнутый объём, а не три отдельные корки (борт, стенка, дно); "
                + "щель между ними в Unity не подсвечивается, её видно только сквозь модель");
        }

        [Test]
        public void EveryTriangle_WindsTheSameWayItsVertexNormalsPoint()
        {
            var surface = Tub();

            for (int t = 0; t < surface.Triangles.Length; t += 3)
            {
                int ia = surface.Triangles[t];
                int ib = surface.Triangles[t + 1];
                int ic = surface.Triangles[t + 2];
                var geometric = Vector3.Cross(
                    surface.Positions[ib] - surface.Positions[ia],
                    surface.Positions[ic] - surface.Positions[ia]);
                var declared = surface.Normals[ia] + surface.Normals[ib] + surface.Normals[ic];

                Assert.Greater(Vector3.Dot(geometric.normalized, declared.normalized), 0f,
                    "треугольник " + t / 3 + " намотан против своих же нормалей. Такая "
                    + "грань пропадает при взгляде с той стороны, с которой её освещают, "
                    + "и ванна выглядит дырявой ровно на одном из трёх участков");
            }
        }

        [Test]
        public void TheOuterSilhouette_IsExactlyTheDeclaredBox()
        {
            var surface = Tub();

            Assert.AreEqual(Width * 0.5f, surface.Positions.Max(p => p.x), Tol, "правый бок");
            Assert.AreEqual(-Width * 0.5f, surface.Positions.Min(p => p.x), Tol, "левый бок");
            Assert.AreEqual(Depth * 0.5f, surface.Positions.Max(p => p.z), Tol, "перёд");
            Assert.AreEqual(-Depth * 0.5f, surface.Positions.Min(p => p.z), Tol, "зад");
            Assert.AreEqual(Height * 0.5f, surface.Positions.Max(p => p.y), Tol, "борт");
            Assert.AreEqual(-Height * 0.5f, surface.Positions.Min(p => p.y), Tol, "низ");
        }

        [Test]
        public void TheBowlFloor_StaysAboveTheShellFloor()
        {
            var surface = Tub();
            float bowlFloor = surface.Positions
                .Where(p => p.y > -Height * 0.5f + Tol).Min(p => p.y);

            Assert.AreEqual(Height * 0.5f - BowlDepth, bowlFloor, Tol,
                "дно чаши уехало с объявленной глубины");
            Assert.Greater(bowlFloor, -Height * 0.5f,
                "чаша пробила корпус насквозь: под ней обязан остаться материал, иначе "
                + "ванна стоит на полу дырой вниз");
        }

        [Test]
        public void AFilletOfZero_MakesASharpBowlWithoutAddingDegenerateTriangles()
        {
            Assert.AreEqual(0, DegenerateTriangles(Tub(fillet: 0f)),
                "нулевое скругление дна обязано схлопывать лишние кольца, а не оставлять "
                + "их вырожденными лентами: вырожденный треугольник даёт NaN-нормаль");
            Assert.AreEqual(0, DegenerateTriangles(Tub()), "скруглённая чаша");
        }

        [Test]
        public void AskingForAnImpossibleRim_LeavesABowlInsteadOfTurningTheTubSolid()
        {
            var surface = Tub(rim: Depth);

            Assert.Less(surface.Rim, Depth * 0.5f,
                "борт шире половины ванны съел бы чашу целиком");
            Assert.Greater(Depth - 2f * surface.Rim, 0f, "чаша исчезла");
        }

        [Test]
        public void AskingForAFilletDeeperThanTheBowl_KeepsTheWallsVertical()
        {
            var surface = Tub(fillet: BowlDepth * 4f);

            Assert.LessOrEqual(surface.Fillet, BowlDepth,
                "скругление глубже самой чаши вывернуло бы дугу выше борта");
            Assert.LessOrEqual(surface.Fillet, (Depth - 2f * surface.Rim) * 0.5f,
                "скругление шире половины чаши схлопнуло бы дно в точку");
        }
    }
}
