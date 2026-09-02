using System.Linq;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

namespace KitchenDesigner.Tests.Geometry
{
    /// <summary>Мягкая плита ПРОИЗВОЛЬНОГО скруглённого плана — недостающий
    /// строитель между двумя крайностями, которые были в проекте раньше.
    ///
    /// ProfileExtrusionMesh выдавливает любой контур, но с ОСТРЫМИ кромками:
    /// доска, а не подушка. CushionSurface даёт настоящую мягкость, но только у
    /// КОРОБКИ: её угловое скругление подрезается собственной толщиной
    /// (MinComponent(core)), поэтому на подушке 50 мм фаска не больше 25 мм — в
    /// плане это остаётся квадрат. На круглом пуфике получалась круглая тумба с
    /// КВАДРАТНОЙ сидушкой; это видно на iso_pouffe_450x400x450_round.png, и
    /// именно с этой картинки начался этот класс.
    ///
    /// Здесь план и толщина РАЗВЕДЕНЫ. План задаёт CornerRadii и ограничен
    /// только сам собой (RoundedRectProfile.Fit) — круглый план разрешён при
    /// любой толщине. Толщина задаёт ФАСКУ верхней и нижней кромок, и только
    /// её. Поэтому «квадрат — скругление — круг» проходит одним параметром,
    /// ровно как у тумбы под сидушкой.
    ///
    /// Кольца строятся ЭКВИДИСТАНТНЫМ смещением контура внутрь: скруглённый
    /// прямоугольник, сжатый на d по обеим сторонам с радиусами r−d, — это
    /// точный внутренний офсет, пока r ≥ d. Отсюда у круглого плана каждое
    /// кольцо остаётся ОКРУЖНОСТЬЮ, а не превращается в квадрат с фасками.
    ///
    /// Надува граней (CushionSurface.Bulge) здесь НЕТ намеренно, и это решение,
    /// а не упущение: силуэт плиты обязан совпадать с объявленным контуром —
    /// сидушка лежит на тумбе того же контура, и раздутый бок вылез бы наружу
    /// тумбы, а раздутый низ утонул бы в ней. CONVENTIONS.md → «A mesh and its
    /// metadata must describe the SAME shape». Мягкость здесь несёт фаска
    /// кромки, и её видно; надув в 4 мм на детали 320 мм — нет.
    ///
    /// Половина без сцены живёт в Core/Geometry и проверяется за 0,3 с; сборка
    /// Mesh — SoftSlabMesh в Core/Elements, шесть строк.</summary>
    public class SoftSlabSurfaceTests
    {
        private const float Tol = 1e-4f;

        private const float Width = 0.6f;
        private const float Depth = 0.35f;
        private const float Thickness = 0.05f;

        private static SoftSlabSurface Round() => new SoftSlabSurface(
            Width, Depth, CornerRadii.Uniform(Depth * 0.5f), Thickness, Thickness * 0.5f);

        private static SoftSlabSurface Square() => new SoftSlabSurface(
            Width, Depth, CornerRadii.Uniform(0f), Thickness, Thickness * 0.5f);

        private static float MaxAlong(SoftSlabSurface surface, int axis)
            => surface.Positions.Max(p => p[axis]);

        private static float MinAlong(SoftSlabSurface surface, int axis)
            => surface.Positions.Min(p => p[axis]);

        [Test]
        public void EveryRing_LiesExactlyOnTheContourOffsetInwardByItsOwnInset()
        {
            var surface = Round();
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
                        "кольцо " + ring + " ушло с эквидистанты: точки обязаны лежать "
                        + "ровно на контуре, сжатом внутрь на " + inset + ". Именно этот "
                        + "точный офсет (стороны −2d, радиусы −d) и делает круглый план "
                        + "круглым на ВСЕЙ высоте — то, чего не умеет CushionSurface");
                }
            }
        }

        [Test]
        public void ARoundPlan_StaysACircleAtEveryHeight_NotOnlyAtTheWidestRing()
        {
            float side = 0.45f;
            float radius = side * 0.5f;
            var surface = new SoftSlabSurface(side, side, CornerRadii.Uniform(radius),
                Thickness, Thickness * 0.5f);
            int perRing = surface.PointsPerRing;

            for (int ring = 0; ring < surface.Sections.Count; ring++)
            {
                float expected = radius - surface.Sections[ring].Inset;

                for (int j = 0; j < perRing; j++)
                {
                    var p = surface.Positions[ring * perRing + j];
                    Assert.AreEqual(expected, new Vector2(p.x, p.z).magnitude, Tol,
                        "круглый пуфик: сечение сидушки на кольце " + ring + " перестало "
                        + "быть ОКРУЖНОСТЬЮ радиуса " + expected + ". Ровно здесь ломалась "
                        + "подушка CushionSurface — её угловой радиус подрезан собственной "
                        + "толщиной (50 мм дают 25), и на круглой тумбе лежала квадратная "
                        + "сидушка: см. iso_pouffe_450x400x450_round.png");
                }
            }
        }

        [Test]
        public void TheWidestRing_ReachesTheDeclaredPlan_AndNothingGoesOutsideIt()
        {
            var surface = Round();

            Assert.AreEqual(Width * 0.5f, MaxAlong(surface, 0), Tol,
                "самое широкое кольцо обязано доходить до объявленной ширины: сидушка "
                + "лежит на тумбе того же контура, и заниженный силуэт читался бы как "
                + "вторая, меньшая деталь");
            Assert.AreEqual(Depth * 0.5f, MaxAlong(surface, 2), Tol,
                "и до объявленной глубины: числа 600 и 350 взяты разными нарочно, на "
                + "квадрате перепутанные оси дали бы тот же ответ");

            foreach (var p in surface.Positions)
                Assert.LessOrEqual(
                    RoundedRectProfile.SignedDistance(new Vector2(p.x, p.z), Width, Depth,
                        surface.Radii), Tol,
                    "вершина вылезла за объявленный контур. Надува граней у мягкой плиты "
                    + "нет намеренно: раздутый бок свесился бы с тумбы, а раздутый низ "
                    + "утонул бы в ней (CONVENTIONS.md → «A mesh and its metadata must "
                    + "describe the SAME shape»)");
        }

        [Test]
        public void TheSlab_StaysInsideItsDeclaredThickness()
        {
            var surface = Round();

            Assert.AreEqual(Thickness * 0.5f, MaxAlong(surface, 1), Tol,
                "верх плиты — ровно половина толщины: рамка изометрического снимка и "
                + "AABB для привязки строятся по объявленному габариту");
            Assert.AreEqual(-Thickness * 0.5f, MinAlong(surface, 1), Tol, "и низ тоже");
        }

        [Test]
        public void TopAndBottom_AreInsetByTheFillet_SoTheEdgeIsSoftAndNotSharp()
        {
            var surface = Round();
            float half = Thickness * 0.5f;
            int perRing = surface.PointsPerRing;
            int top = (surface.Sections.Count - 1) * perRing;

            float widestTop = Enumerable.Range(0, perRing)
                .Max(j => surface.Positions[top + j].x);
            float widestBottom = Enumerable.Range(0, perRing)
                .Max(j => surface.Positions[j].x);

            Assert.AreEqual(half, surface.Positions[top].y, Tol,
                "последнее кольцо лежит на самом верху плиты");
            Assert.AreEqual(Width * 0.5f - surface.Fillet, widestTop, Tol,
                "верхняя плоскость утоплена внутрь ровно на фаску — это и есть мягкая "
                + "кромка. Ноль здесь означал бы доску профильного выдавливания");
            Assert.AreEqual(Width * 0.5f - surface.Fillet, widestBottom, Tol,
                "и нижняя симметрично: несимметричная плита села бы на тумбу с щелью "
                + "с одной стороны");
        }

        [Test]
        public void Fillet_IsCappedByHalfTheThickness_AndByAQuarterOfTheSmallerPlanSide()
        {
            Assert.AreEqual(Thickness * 0.5f,
                SoftSlabSurface.FitFillet(Width, Depth, Thickness, 10f), Tol,
                "фаска физически не может быть больше половины толщины: верхняя и нижняя "
                + "дуги встретились бы и вывернули бок наизнанку");
            Assert.AreEqual(Depth * SoftSlabSurface.MaxFilletPlanRatio,
                SoftSlabSurface.FitFillet(Width, Depth, 10f, 10f), Tol,
                "и не больше четверти МЕНЬШЕЙ стороны плана: фаска утапливает контур "
                + "внутрь, и на большем значении верхняя плоскость схлопнулась бы в точку. "
                + "Меньшая сторона здесь — глубина, ширина взята другой нарочно");
            Assert.AreEqual(0f, SoftSlabSurface.FitFillet(Width, Depth, Thickness, -1f), Tol,
                "отрицательная фаска — это острая кромка, а не вывернутая наружу");
            Assert.AreEqual(0.004f,
                SoftSlabSurface.FitFillet(Width, Depth, Thickness, 0.004f), Tol,
                "штатное значение проходит нетронутым: сторож, который правит ВСЁ, "
                + "не отличить от сторожа, который не правит ничего");
        }

        [Test]
        public void ZeroFillet_GivesTheSharpPrism_SoTheBuilderDegradesToAnExtrusion()
        {
            var sharp = new SoftSlabSurface(Width, Depth, CornerRadii.Uniform(0.1f),
                Thickness, 0f);

            Assert.AreEqual(2, sharp.Sections.Count,
                "без фаски у плиты ровно два кольца — верх и низ. Совпадающие кольца "
                + "вырожденной дуги дали бы сотни треугольников нулевой площади");
            Assert.AreEqual(Width * 0.5f,
                Enumerable.Range(0, sharp.PointsPerRing).Max(j => sharp.Positions[j].x), Tol,
                "и нижнее кольцо доходит до полного контура: без фаски утапливать нечего");
        }

        [Test]
        public void WhenTheFilletEatsTheWholeThickness_TheDuplicateSideRingIsDropped()
        {
            var full = new SoftSlabSurface(Width, Depth, CornerRadii.Uniform(0.1f),
                Thickness, Thickness * 0.5f);
            var partial = new SoftSlabSurface(Width, Depth, CornerRadii.Uniform(0.1f),
                Thickness, Thickness * 0.25f);

            Assert.AreEqual(2 * SoftSlabSurface.DefaultFilletSegments + 1, full.Sections.Count,
                "на фаске в половину толщины верхняя и нижняя дуги встречаются в одном "
                + "кольце — прямого бока нет, и второе такое же кольцо дало бы полосу "
                + "вырожденных треугольников. Это ровно случай сидушки пуфика: 50 мм "
                + "толщины, фаска 25");
            Assert.AreEqual(2 * SoftSlabSurface.DefaultFilletSegments + 2,
                partial.Sections.Count,
                "а при меньшей фаске прямой бок есть, и колец на одно больше");
        }

        [Test]
        public void Triangles_AreComplete_AndIndexEveryRingPlusTwoCapCentres()
        {
            var surface = Round();
            int rings = surface.Sections.Count;
            int perRing = surface.PointsPerRing;

            Assert.AreEqual(rings * perRing + 2, surface.Positions.Length,
                "вершины — это кольца плюс два центра крышек; швов по контуру нет, "
                + "кольцо замыкается по модулю");
            Assert.AreEqual((rings - 1) * perRing * 2 + 2 * perRing,
                surface.Triangles.Length / 3,
                "по два треугольника на каждую ячейку между соседними кольцами плюс два "
                + "веера крышек");
            Assert.AreEqual(surface.Positions.Length, surface.Normals.Length,
                "нормаль обязана быть у каждой вершины");
            Assert.AreEqual(surface.Positions.Length, surface.Uvs.Length,
                "и UV тоже");
            Assert.IsTrue(surface.Triangles.All(i => i >= 0 && i < surface.Positions.Length),
                "индекс за пределами массива вершин уронил бы Mesh.triangles в рантайме");
        }

        [Test]
        public void ThePouffeSeat_CostsUnder1000Triangles()
        {
            float toU = AppConstants.MM_TO_UNITS;
            var seat = PouffeLayout.Seat(new Vector3Int(PouffeLayout.DefaultWidthMM,
                    PouffeLayout.DefaultHeightMM, PouffeLayout.DefaultDepthMM),
                225, PouffeLayout.DefaultSeatThicknessMM);
            var surface = new SoftSlabSurface(seat.ProfileWidthMM * toU,
                seat.ProfileDepthMM * toU, CornerRadii.Uniform(seat.RadiusMM * toU),
                seat.ThicknessMM * toU,
                seat.ThicknessMM * toU * SoftSlabSurface.MaxFilletThicknessRatio);

            Assert.AreEqual(478, surface.Positions.Length,
                "топология сидушки круглого пуфика: 7 колец по 68 точек плюс два центра "
                + "крышек. 68 = 4 угла x (16 сегментов + 1) — то же разрешение дуги, что "
                + "у тумбы под ней, иначе круглый силуэт сидушки и тумбы разошёлся бы "
                + "гранями");
            Assert.AreEqual(952, surface.Triangles.Length / 3,
                "и 952 треугольника против 768 у подушки CushionMesh, которую сидушка "
                + "заменила: +24% за то, что круглый пуфик стал круглым целиком. "
                + "Число закреплено, чтобы разрешение не поехало молча");
        }

        [Test]
        public void SideNormals_PointOutward_AndCapNormalsPointAlongY()
        {
            var surface = Round();
            int perRing = surface.PointsPerRing;

            foreach (var normal in surface.Normals)
                Assert.AreEqual(1f, normal.magnitude, 1e-3f,
                    "ненормированная нормаль даёт пятна на затенении");

            Assert.AreEqual(Vector3.up, surface.Normals[surface.Normals.Length - 1],
                "центр верхней крышки смотрит вверх");
            Assert.AreEqual(Vector3.down, surface.Normals[surface.Normals.Length - 2],
                "центр нижней крышки — вниз");

            int side = surface.Sections
                .Select((s, i) => (s, i)).First(p => p.s.Inset <= Tol).i;
            for (int j = 0; j < perRing; j++)
            {
                var p = surface.Positions[side * perRing + j];
                var n = surface.Normals[side * perRing + j];
                Assert.AreEqual(0f, n.y, 1e-3f,
                    "на самом широком кольце нормаль горизонтальна");
                Assert.Greater(n.x * p.x + n.z * p.z, 0f,
                    "и смотрит НАРУЖУ: при обратном обходе контура вся боковина "
                    + "вывернулась бы внутрь детали");
            }
        }

        [Test]
        public void Uvs_UnwrapOverThePlan_SoTheOwnersDecorSurfaceAxesAreTheRightOnes()
        {
            var surface = Round();

            for (int i = 0; i < surface.Positions.Length; i++)
            {
                Assert.AreEqual(surface.Positions[i].x / Width + 0.5f, surface.Uvs[i].x, Tol,
                    "u идёт по ШИРИНЕ плана");
                Assert.AreEqual(surface.Positions[i].z / Depth + 0.5f, surface.Uvs[i].y, Tol,
                    "а v — по ГЛУБИНЕ, а не по высоте: плита лежит горизонтально, и "
                    + "владелец объявляет DecorSurfaceMM в (x, z). Оси стоячей панели "
                    + "уже уронили в релиз растянутую столешницу");
            }
        }

        [Test]
        public void ASquarePlan_KeepsItsCorners_AndTheFilletDoesNotRoundThemInPlan()
        {
            var surface = Square();
            int perRing = surface.PointsPerRing;
            int side = surface.Sections
                .Select((s, i) => (s, i)).First(p => p.s.Inset <= Tol).i;

            Assert.IsTrue(Enumerable.Range(0, perRing).Any(j =>
                Mathf.Abs(surface.Positions[side * perRing + j].x - Width * 0.5f) < Tol
                && Mathf.Abs(surface.Positions[side * perRing + j].z - Depth * 0.5f) < Tol),
                "квадратный план обязан сохранять острый угол в плане: фаска скругляет "
                + "ТОЛЬКО верхнюю и нижнюю кромки. Иначе параметр радиуса перестал бы "
                + "быть единственным, что задаёт форму плана");
        }

        [Test]
        public void TheSurface_IsBuiltInPhysicalSize_SoTheCallerNeverNeedsANonUniformScale()
        {
            var physical = new SoftSlabSurface(Width, Depth, CornerRadii.Uniform(Depth * 0.5f),
                Thickness, Thickness * 0.5f);

            foreach (var p in physical.Positions)
                Assert.LessOrEqual(
                    Mathf.Abs(RoundedRectProfile.SignedDistance(new Vector2(p.x, p.z),
                        Width, Depth, physical.Radii)), Width,
                    "страховка: контур построен в физических миллиметрах");

            var unit = new SoftSlabSurface(1f, 1f, CornerRadii.Uniform(0.5f), 1f, 0.5f);
            float worst = unit.Positions.Max(p => Mathf.Abs(
                RoundedRectProfile.SignedDistance(new Vector2(p.x * Width, p.z * Depth),
                    Width, Depth, physical.Radii)));

            Assert.Greater(worst, 0.001f,
                "положительный контроль к правилу «localScale остаётся (1,1,1)»: та же "
                + "плита, построенная в единичном пространстве и растянутая до этих "
                + "габаритов, даёт ЭЛЛИПС вместо стадиона и расходится с объявленным "
                + "контуром на " + worst + " м. Без этой проверки тест на физический "
                + "размер зеленел бы и на единичной сборке; проект наступал на это "
                + "четырежды");
        }
    }
}
