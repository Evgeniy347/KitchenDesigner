using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

namespace KitchenDesigner.Tests.Geometry
{
    /// <summary>Сиденье табуретки и посадка её ножек — обе фигуры строятся из
    /// одного примитива (<see cref="RoundedRectProfile"/>), и обе обязаны
    /// оставаться ПРАВИЛЬНЫМИ на НЕквадратном следе: круглый угол должен
    /// оставаться дугой окружности, а не эллипса.
    ///
    /// Почему это отдельный тест, а не «и так очевидно»: радиусный стол строит
    /// столешницу в ЕДИНИЧНОМ пространстве и растягивает её корневым
    /// transform-ом — капсуле это можно, потому что растянутая капсула всё ещё
    /// капсула. Табуретке нельзя: её радиус задан в физических миллиметрах, и
    /// растяжение единичного меша превратило бы окружности в эллипсы. Поэтому
    /// сиденье строится сразу в физических единицах, а этот тест держит
    /// свойство, ради которого так сделано.
    ///
    /// Тест лежит в каталоге ядра и потому исполняется ещё и вторым проходом
    /// под dotnet — 0,3 с вместо холодного Unity.</summary>
    public class StoolSeatProfileTests
    {
        private const float Tol = 1e-4f;

        private static float DistanceTo(Vector2 point, Vector2 centre)
            => (point - centre).magnitude;

        [Test]
        public void Uniform_ZeroRadius_ProfileIsExactlyTheFourCorners()
        {
            var profile = RoundedRectProfile.Uniform(0.36f, 0.36f, 0f,
                RoundedRectProfile.DefaultSegments);

            Assert.AreEqual(4, profile.Length,
                "радиус 0 — это квадратная табуретка: контур обязан состоять ровно из "
                + "четырёх точек, без единого сегмента дуги");
        }

        [Test]
        public void Uniform_NonSquareFootprint_CornerArcStaysCircular_NotElliptical()
        {
            const float width = 0.6f;
            const float depth = 0.36f;
            const float radius = 0.12f;

            var profile = RoundedRectProfile.Uniform(width, depth, radius,
                RoundedRectProfile.DefaultSegments);

            var centre = new Vector2(-width * 0.5f + radius, -depth * 0.5f + radius);
            int onArc = 0;
            foreach (var p in profile)
            {
                if (p.x > centre.x + Tol || p.y > centre.y + Tol) continue;
                onArc++;
                Assert.AreEqual(radius, DistanceTo(p, centre), Tol,
                    "точка углового скругления обязана лежать на ОКРУЖНОСТИ радиуса "
                    + radius + " м вокруг центра угла. Если сиденье строить в единичном "
                    + "пространстве и растягивать корневым transform-ом (как делает "
                    + "радиусный стол), у несимметричного следа 600x360 угол станет "
                    + "эллиптическим, и физический радиус в миллиметрах перестанет "
                    + "что-либо значить");
            }

            Assert.GreaterOrEqual(onArc, RoundedRectProfile.DefaultSegments,
                "дуга угла не найдена — тест позеленел бы, ничего не проверив");
        }

        [Test]
        public void Uniform_MaxRadiusOnASquareFootprint_IsACircle()
        {
            const float side = 0.36f;
            var profile = RoundedRectProfile.Uniform(side, side, side * 0.5f,
                RoundedRectProfile.DefaultSegments);

            foreach (var p in profile)
                Assert.AreEqual(side * 0.5f, p.magnitude, Tol,
                    "радиус = min(Ш, Г)/2 на квадратном следе — это полностью круглая "
                    + "табуретка: каждая точка контура равноудалена от центра");
        }

        [Test]
        public void Uniform_MaxRadiusOnANonSquareFootprint_IsACapsule_NotAnEllipse()
        {
            const float width = 0.6f;
            const float depth = 0.36f;
            const float radius = depth * 0.5f;

            var profile = RoundedRectProfile.Uniform(width, depth, radius,
                RoundedRectProfile.DefaultSegments);

            var centre = new Vector2(width * 0.5f - radius, depth * 0.5f - radius);
            int onArc = 0;
            foreach (var p in profile)
            {
                if (p.x < centre.x - Tol || p.y < centre.y - Tol) continue;
                onArc++;
                Assert.AreEqual(radius, DistanceTo(p, centre), Tol,
                    "при Ш != Г максимальный радиус даёт КАПСУЛУ: полукруги радиуса "
                    + "min(Ш, Г)/2 по торцам и прямые между ними");
            }

            Assert.Greater(onArc, 1, "дуга торца не найдена — сторож ослеп");
        }

        [Test]
        public void Footprint_SquareSeat_PutsLegsAtTheInsetFromEachEdge()
        {
            const float width = 0.36f;
            const float depth = 0.36f;
            const float inset = 0.03f;
            const float leg = 0.04f;

            var legs = StoolLegs.Footprint(width, depth, 0f, inset, leg);

            float expected = width * 0.5f - inset - leg * 0.5f;
            Assert.AreEqual(4, legs.Length, "у табуретки четыре ножки");
            foreach (var p in legs)
            {
                Assert.AreEqual(expected, Mathf.Abs(p.x), Tol,
                    "у квадратной табуретки внешняя грань ножки отстоит от края сиденья "
                    + "ровно на отступ " + inset + " м");
                Assert.AreEqual(expected, Mathf.Abs(p.y), Tol,
                    "то же по второй оси");
            }
        }

        [Test]
        public void Footprint_RoundSeat_KeepsEveryLegCornerUnderTheSeat()
        {
            const float width = 0.36f;
            const float depth = 0.36f;
            float radius = width * 0.5f;
            const float inset = 0.03f;
            const float leg = 0.04f;

            var legs = StoolLegs.Footprint(width, depth, radius, inset, leg);

            foreach (var centre in legs)
            {
                var farCorner = new Vector2(
                    centre.x + Mathf.Sign(centre.x) * leg * 0.5f,
                    centre.y + Mathf.Sign(centre.y) * leg * 0.5f);

                Assert.LessOrEqual(
                    RoundedRectProfile.SignedDistance(farCorner, width, depth, radius), 0f,
                    "у круглой табуретки углы сиденья срезаны, поэтому ножку, поставленную "
                    + "по прямоугольной схеме, вынесло бы наружу контура — она обязана "
                    + "подтягиваться внутрь по диагонали");
            }
        }

        [Test]
        public void Footprint_RoundSeatOnANonSquareFootprint_KeepsEveryLegCornerUnderTheSeat()
        {
            const float width = 0.6f;
            const float depth = 0.36f;
            float radius = depth * 0.5f;
            const float inset = 0.03f;
            const float leg = 0.04f;

            var legs = StoolLegs.Footprint(width, depth, radius, inset, leg);

            foreach (var centre in legs)
            {
                var farCorner = new Vector2(
                    centre.x + Mathf.Sign(centre.x) * leg * 0.5f,
                    centre.y + Mathf.Sign(centre.y) * leg * 0.5f);

                Assert.LessOrEqual(
                    RoundedRectProfile.SignedDistance(farCorner, width, depth, radius), 0f,
                    "несимметричный след — тот самый случай, на котором капсульный стол уже "
                    + "выносил ножки наружу столешницы (CONVENTIONS.md → «A mesh and its "
                    + "metadata must describe the SAME shape»)");
            }
        }
    }
}
