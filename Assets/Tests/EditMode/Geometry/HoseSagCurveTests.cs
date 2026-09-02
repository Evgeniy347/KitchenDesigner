using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

namespace KitchenDesigner.Tests.Geometry
{
    /// <summary>Провисающая петля душевого шланга. Шланг НЕ натянут между
    /// дивертором и ручной лейкой: между ними 400-500 мм, а самого шланга
    /// метр с лишним, и весь избыток обязан уйти вниз петлёй — иначе на
    /// картинке будет прямая палка, по которой сразу видно, что модель
    /// нарисована «по прямой от точки до точки».
    ///
    /// Кривая — кубическая Безье, а не катенария: у катенарии дуга заданной
    /// длины между двумя точками ищется решением трансцендентного уравнения
    /// и всё равно не даёт задать НАПРАВЛЕНИЕ выхода из штуцера, а оно тут
    /// главное — шланг выходит из дивертора вниз и входит в рукоятку тоже
    /// вниз. У Безье направление задаётся касательной бесплатно, а длина
    /// подбирается делением пополам по длине управляющих плеч: длина дуги
    /// растёт по плечу монотонно, поэтому бисекция сходится.
    ///
    /// Отсюда же и главный отказ: если шланга МЕНЬШЕ, чем расстояние между
    /// концами, петли не существует, и растягивать его нельзя — возвращается
    /// прямой отрезок. Молча выдать кривую длиннее заказанной значит соврать
    /// в спецификации.</summary>
    public class HoseSagCurveTests
    {
        private const float Tol = 1e-3f;

        private static readonly Vector3 Outlet = new Vector3(0f, 0f, 60f);
        private static readonly Vector3 Handle = new Vector3(0f, 450f, 140f);

        private static Vector3[] Loop(float hoseLengthMM) =>
            HoseSagCurve.Hanging(Outlet, Vector3.down, Handle, Vector3.down, hoseLengthMM,
                HoseSagCurve.DefaultSamples);

        [Test]
        public void HoseSagCurve_Hanging_KeepsBothEndsExactlyOnTheirFittings()
        {
            var loop = Loop(1200f);

            Assert.Less((loop[0] - Outlet).magnitude, Tol,
                "первая точка обязана совпасть со штуцером дивертора: щель между шлангом и "
                + "штуцером видна на любом ракурсе");
            Assert.Less((loop[loop.Length - 1] - Handle).magnitude, Tol,
                "последняя точка обязана совпасть с торцом рукоятки лейки");
        }

        [Test]
        public void HoseSagCurve_Hanging_MatchesTheRequestedHoseLength()
        {
            const float requested = 1200f;
            float actual = PipePath.LengthMM(Loop(requested));

            Assert.AreEqual(requested, actual, requested / 100f,
                "длина петли — это длина шланга из спецификации: расхождение больше процента "
                + "значит, что бисекция по плечу не сошлась и число в панели ничего не описывает");
        }

        [Test]
        public void HoseSagCurve_Hanging_DipsBelowBothFittings()
        {
            var lowest = PipePath.LowestPoint(Loop(1200f));

            Assert.Less(lowest.y, Outlet.y,
                "избыток шланга уходит вниз: петля обязана опуститься ниже нижнего штуцера, "
                + "иначе касательные заданы не туда и шланг торчит вверх");
            Assert.Less(lowest.y, Handle.y,
                "и тем более ниже верхнего крепления в рукоятке");
        }

        [Test]
        public void HoseSagCurve_Hanging_DipsDeeperWhenTheHoseGetsLonger()
        {
            float shortDip = PipePath.LowestPoint(Loop(900f)).y;
            float longDip = PipePath.LowestPoint(Loop(1600f)).y;

            Assert.Less(longDip, shortDip,
                "длиннее шланг — глубже петля. Немонотонность здесь означала бы, что "
                + "пользователь крутит длину шланга, а картинка живёт своей жизнью");
        }

        [Test]
        public void HoseSagCurve_Hanging_ReturnsTheStraightChordWhenTheHoseIsTooShort()
        {
            float chord = (Handle - Outlet).magnitude;
            var loop = Loop(chord / 2f);

            Assert.AreEqual(2, loop.Length,
                "петли нет — есть отрезок из двух точек");
            Assert.AreEqual(chord, PipePath.LengthMM(loop), Tol,
                "короткий шланг не растягивается до заказанной длины и не сжимается: он "
                + "просто натянут по прямой, и это видно");
        }

        [Test]
        public void HoseSagCurve_Hanging_LeavesTheOutletAlongTheGivenDirection()
        {
            var loop = Loop(1200f);
            var firstStep = (loop[1] - loop[0]).normalized;

            Assert.Greater(Vector3.Dot(firstStep, Vector3.down), Tolerance.UpDotThreshold,
                "шланг выходит из штуцера строго по его оси: излом прямо на резьбе — первое, "
                + "что бросается в глаза на рендере");
        }

        [Test]
        public void HoseSagCurve_Point_ReturnsTheControlEndpointsAtBothEnds()
        {
            var p0 = new Vector3(1f, 2f, 3f);
            var p3 = new Vector3(9f, 8f, 7f);
            var p1 = new Vector3(4f, -5f, 3f);
            var p2 = new Vector3(6f, -5f, 7f);

            Assert.AreEqual(p0, HoseSagCurve.Point(p0, p1, p2, p3, 0f),
                "при t=0 кубическая Безье равна первой опорной точке");
            Assert.AreEqual(p3, HoseSagCurve.Point(p0, p1, p2, p3, 1f),
                "при t=1 — последней");
        }

        [Test]
        public void HoseSagCurve_HandleLengthMM_IsZeroWhenThereIsNoSlack()
        {
            float chord = (Handle - Outlet).magnitude;

            Assert.AreEqual(0f, HoseSagCurve.HandleLengthMM(Outlet, Vector3.down, Handle,
                Vector3.down, chord, HoseSagCurve.DefaultSamples), Tol,
                "ровно натянутый шланг не имеет плеча: любое плечо удлинило бы дугу сверх "
                + "заказанного");
        }
    }
}
