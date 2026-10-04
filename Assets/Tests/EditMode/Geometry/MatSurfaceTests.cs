using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

namespace KitchenDesigner.Tests.Geometry
{
    /// <summary>Мат с скруглёнными верхними кромками ТОРЦОВ (сиденье и спинка дивана):
    /// кромка между верхней гранью и левым/правым торцом, по всей глубине, в вертикальном
    /// сечении. Передняя и задняя верхние кромки острые — так на скриншотах пользователя
    /// (обведены только кромки вдоль глубины). Класс данных без Mesh, поэтому проверяется
    /// на быстром пути: объём (замкнутость), высоты, нормали, развёртка.</summary>
    public class MatSurfaceTests
    {
        private const float Width = 2.0f;
        private const float Depth = 0.72f;
        private const float Thickness = 0.36f;
        private const float Edge = 0.04f;

        private static MatSurface Square(float edge, bool lower = false)
            => new MatSurface(Width, Depth, Thickness, CornerRadii.Uniform(0f), edge, lower);

        private static MatSurface Rounded(float edge, bool lower = false)
            => new MatSurface(Width, Depth, Thickness, CornerRadii.Uniform(0.12f), edge, lower);

        private static float Volume(MatSurface surface)
        {
            double volume = 0.0;
            for (int i = 0; i < surface.Triangles.Length; i += 3)
            {
                var a = surface.Positions[surface.Triangles[i]];
                var b = surface.Positions[surface.Triangles[i + 1]];
                var c = surface.Positions[surface.Triangles[i + 2]];
                volume += Vector3.Dot(a, Vector3.Cross(b, c)) / 6.0;
            }

            return (float)volume;
        }

        [Test]
        public void Volume_OfASquareMat_IsTheBoxMinusTheTwoRoundedEdges()
        {
            var surface = Square(Edge);

            float lossPerEnd = Depth * Edge * Edge * (1f - Mathf.PI * 0.25f);
            float expected = Width * Depth * Thickness - 2f * lossPerEnd;

            Assert.AreEqual(expected, Volume(surface), expected * 0.005f,
                "объём замкнутой оболочки: коробка минус две срезанные кромки торцов. Дыра в "
                + "оболочке или перепутанная ориентация граней дали бы совсем другое число");
        }

        [Test]
        public void WithoutAnEdgeRadius_TheMatIsAPlainBox_OfTheFullVolume()
        {
            var surface = Square(0f);

            Assert.AreEqual(Width * Depth * Thickness, Volume(surface),
                Width * Depth * Thickness * 0.001f,
                "радиус ноль — обычная плита: поле скругления можно выключить");
        }

        [Test]
        public void TheEndsDropByTheEdgeRadius_AndTheMiddleStaysAtTheFullThickness()
        {
            var surface = Square(Edge);

            Assert.AreEqual(Thickness * 0.5f, surface.TopHeightAt(0f), 1e-6f,
                "в середине верх на полной высоте");
            Assert.AreEqual(Thickness * 0.5f, surface.TopHeightAt(Width * 0.5f - Edge), 1e-6f,
                "и там, где начинается скругление");
            Assert.AreEqual(Thickness * 0.5f - Edge, surface.TopHeightAt(Width * 0.5f), 1e-6f,
                "на самом торце верх ниже ровно на радиус: четверть окружности радиусом "
                + "кромки кончается вертикальной касательной у торцевой грани");
            Assert.AreEqual(surface.TopHeightAt(Width * 0.5f), surface.TopHeightAt(-Width * 0.5f),
                1e-6f, "левый торец зеркален правому");
        }

        [Test]
        public void TheTopSurface_NeverRises_TowardsAnEnd()
        {
            var surface = Square(Edge);
            float last = surface.TopHeightAt(0f);
            for (float x = 0f; x <= Width * 0.5f; x += 0.002f)
            {
                float height = surface.TopHeightAt(x);
                Assert.LessOrEqual(height, last + 1e-6f,
                    "верх к торцу только опускается: x=" + x);
                last = height;
            }
        }

        [Test]
        public void FrontAndRearTopEdges_StaySharp_OnlyTheEndsAreRounded()
        {
            var surface = Square(Edge);
            int checkedVertices = 0;

            foreach (var p in surface.Positions)
            {
                bool onFrontOrRear = Mathf.Abs(Mathf.Abs(p.z) - Depth * 0.5f) < 1e-6f;
                bool inTheMiddle = Mathf.Abs(p.x) <= Width * 0.5f - Edge + 1e-6f;
                if (!onFrontOrRear || !inTheMiddle) continue;
                if (p.y < 0f) continue;
                checkedVertices++;
                Assert.AreEqual(Thickness * 0.5f, p.y, 1e-6f,
                    "передняя и задняя верхние кромки не скруглены: на скриншотах обведены "
                    + "только кромки торцов вдоль глубины");
            }

            Assert.Greater(checkedVertices, 4, "вершин на передней и задней кромке не нашлось");
        }

        [Test]
        public void Bounds_AreTheMatsOwnWidthDepthAndThickness_SoItsColliderFitsAsBefore()
        {
            var surface = Rounded(Edge);
            var min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
            var max = new Vector3(float.MinValue, float.MinValue, float.MinValue);
            foreach (var p in surface.Positions)
            {
                min = Vector3.Min(min, p);
                max = Vector3.Max(max, p);
            }

            Assert.AreEqual(new Vector3(-Width * 0.5f, -Thickness * 0.5f, -Depth * 0.5f), min,
                "нижний угол габарита");
            Assert.AreEqual(new Vector3(Width * 0.5f, Thickness * 0.5f, Depth * 0.5f), max,
                "верхний угол габарита: скругление кромок не меняет охватывающую коробку "
                + "(BoxCollider по bounds меша и сенсор совпадающих плоскостей остаются в силе)");
        }

        [Test]
        public void EveryTriangle_FacesOutward_AsItsVertexNormalsSay()
        {
            var surface = Rounded(Edge);
            int checkedTriangles = 0;

            for (int i = 0; i < surface.Triangles.Length; i += 3)
            {
                int i0 = surface.Triangles[i], i1 = surface.Triangles[i + 1], i2 = surface.Triangles[i + 2];
                var cross = Vector3.Cross(surface.Positions[i1] - surface.Positions[i0],
                    surface.Positions[i2] - surface.Positions[i0]);
                var normal = surface.Normals[i0] + surface.Normals[i1] + surface.Normals[i2];
                checkedTriangles++;

                Assert.Greater(Vector3.Dot(cross, normal), 0f,
                    "обход вершин треугольника " + (i / 3) + " противоположен его нормали: грань "
                    + "была бы видна изнутри");
            }

            Assert.Greater(checkedTriangles, 200, "треугольников слишком мало");
        }

        [Test]
        public void Normals_AreUnitLength_AndTheTopAtAnEndLooksSideways()
        {
            var surface = Square(Edge);
            bool sawSidewaysTop = false;

            for (int i = 0; i < surface.Normals.Length; i++)
            {
                Assert.AreEqual(1f, surface.Normals[i].magnitude, 1e-4f,
                    "нормаль " + i + " единичная");
                if (Mathf.Abs(surface.Positions[i].x) > Width * 0.5f - 1e-6f
                    && Mathf.Abs(surface.Normals[i].x) > 0.99f
                    && surface.Positions[i].y > 0f)
                    sawSidewaysTop = true;
            }

            Assert.IsTrue(sawSidewaysTop,
                "на самом торце верхняя поверхность смотрит вбок: скругление переходит в "
                + "вертикальную торцевую грань без излома");
        }

        [Test]
        public void ARoundedLowerFace_MirrorsTheMat_SoTheBackrestRoundsTheFaceThatLiesUp()
        {
            var upper = Square(Edge, false);
            var lower = Square(Edge, true);

            float upperMinAtEnd = float.MaxValue, upperMaxAtEnd = float.MinValue;
            float lowerMinAtEnd = float.MaxValue, lowerMaxAtEnd = float.MinValue;
            foreach (var p in upper.Positions)
                if (Mathf.Abs(p.x) > Width * 0.5f - 1e-6f)
                {
                    upperMinAtEnd = Mathf.Min(upperMinAtEnd, p.y);
                    upperMaxAtEnd = Mathf.Max(upperMaxAtEnd, p.y);
                }

            foreach (var p in lower.Positions)
                if (Mathf.Abs(p.x) > Width * 0.5f - 1e-6f)
                {
                    lowerMinAtEnd = Mathf.Min(lowerMinAtEnd, p.y);
                    lowerMaxAtEnd = Mathf.Max(lowerMaxAtEnd, p.y);
                }

            Assert.AreEqual(Thickness * 0.5f - Edge, upperMaxAtEnd, 1e-6f, "верхняя грань срезана на торце");
            Assert.AreEqual(-Thickness * 0.5f, upperMinAtEnd, 1e-6f, "нижняя целая");
            Assert.AreEqual(-(Thickness * 0.5f - Edge), lowerMinAtEnd, 1e-6f,
                "в зеркальном варианте срезана нижняя грань");
            Assert.AreEqual(Thickness * 0.5f, lowerMaxAtEnd, 1e-6f, "а верхняя целая");
            Assert.AreEqual(Volume(upper), Volume(lower), Mathf.Abs(Volume(upper)) * 1e-4f,
                "и объём зеркального мата тот же, знак не перевернулся");
        }

        [Test]
        public void TheUv_IsPhysical_OnEveryFlatFace_IncludingTheEndWallsBelowTheEdge()
        {
            var surface = Rounded(Edge);
            var positionsMm = new Vector3[surface.Positions.Length];
            var textureMm = new Vector2[surface.Positions.Length];
            for (int i = 0; i < positionsMm.Length; i++)
            {
                positionsMm[i] = surface.Positions[i] * 1000f;
                textureMm[i] = new Vector2(surface.Uvs[i].x * Width * 1000f,
                    surface.Uvs[i].y * Depth * 1000f);
            }

            var report = UvStretch.Measure(positionsMm, textureMm, surface.Normals,
                surface.Triangles);

            Assert.Greater(report.Triangles, 40, "плоских треугольников слишком мало");
            Assert.AreEqual(1f, report.MinSigma, 0.02f,
                "развёртка плоских граней мата честная: ST = размер детали / размер плитки, как "
                + "у остальных выдавленных деталей (docs/TEXTURES.md)");
            Assert.AreEqual(1f, report.MaxSigma, 0.02f, "и масштаб не раздут");
        }

        [Test]
        public void FitEdge_IsLimitedByHalfTheThicknessAndAQuarterOfTheWidth()
        {
            Assert.AreEqual(0f, MatSurface.FitEdge(2f, 0.36f, -1f), "отрицательный радиус — ноль");
            Assert.AreEqual(0.18f, MatSurface.FitEdge(2f, 0.36f, 5f), 1e-6f,
                "не больше половины толщины: больше — кромка срезала бы мат целиком");
            Assert.AreEqual(0.1f, MatSurface.FitEdge(0.4f, 0.36f, 5f), 1e-6f,
                "и не больше четверти ширины: у узкого мата два скругления не должны сойтись");
        }
    }
}
