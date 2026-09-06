using System.Collections.Generic;
using NUnit.Framework;
using KitchenDesigner.Core;

namespace KitchenDesigner.Tests.Geometry
{
    /// <summary>Крышки стены — нижняя и ВЕРХНЯЯ. Обе идут горизонтальными плитами
    /// во всю длину стены, и обе обязаны разрезаться проёмом, который до них
    /// доходит.
    ///
    /// Про нижнюю это уже знали: под дверью оставался порог. Про верхнюю — нет,
    /// и она невидима ровно до тех пор, пока стена полной высоты: плита лежит на
    /// 2500 мм, выше любого проёма. Но «опустить все стены» — это СЖАТИЕ меша по
    /// Y до 100 мм, и та же плита садится на 100 мм над полом поперёк дверного
    /// прохода. Это и есть полоса, которую видит пользователь.
    ///
    /// Считаем в миллиметрах на стене 3000 мм: сколько её остаётся внутри проёма
    /// шириной 900 мм. Ответ обязан быть нулём у двери и полной длиной у окна —
    /// пара, а не один тест: вырезать верх по окну значит продырявить стену над
    /// подоконником.</summary>
    public class WallCapSpansTests
    {
        private const float WallLenMM = 3000f;
        private const float WallHeightMM = 2500f;
        private const float DoorWidthMM = 900f;

        private static Span NormX(float centreMM, float widthMM) =>
            Span.FromCenter(centreMM / WallLenMM, widthMM / WallLenMM);

        private static Span NormY(float bottomMM, float topMM) =>
            new Span(bottomMM / WallHeightMM + DoorOpeningLayout.WallBaseNorm,
                topMM / WallHeightMM + DoorOpeningLayout.WallBaseNorm);

        /// <summary>Сколько миллиметров стены осталось внутри отрезка проёма.
        /// Именно это число видно на экране полосой, и именно его называет
        /// сообщение об ошибке.</summary>
        private static float CoveredMM(IEnumerable<Span> solid, Span hole)
        {
            float mm = 0f;
            foreach (var s in solid)
            {
                float lo = s.Min > hole.Min ? s.Min : hole.Min;
                float hi = s.Max < hole.Max ? s.Max : hole.Max;
                if (hi > lo) mm += (hi - lo) * WallLenMM;
            }
            return mm;
        }

        private static float TotalMM(IEnumerable<Span> solid)
        {
            float mm = 0f;
            foreach (var s in solid) mm += s.Size * WallLenMM;
            return mm;
        }

        private static List<Span> TopCapOf(params (Span x, Span y)[] cutouts)
        {
            var holes = new List<Span>();
            foreach (var (x, y) in cutouts)
                if (WallCapSpans.ReachesTop(y)) holes.Add(x);
            return WallCapSpans.Solid(holes, DoorOpeningLayout.WallBaseNorm, WallCapSpans.TopNorm);
        }

        private static List<Span> BottomCapOf(params (Span x, Span y)[] cutouts)
        {
            var holes = new List<Span>();
            foreach (var (x, y) in cutouts)
                if (WallCapSpans.ReachesBase(y)) holes.Add(x);
            return WallCapSpans.Solid(holes, DoorOpeningLayout.WallBaseNorm, WallCapSpans.TopNorm);
        }

        /// <summary>Опущенная стена: дверной вырез растянут на всю нормированную
        /// высоту (<see cref="DoorOpeningLayout.FullHeightSpanNorm"/>), значит он
        /// достаёт до верхней крышки и обязан её разрезать. Иначе на 100 мм над
        /// полом поперёк прохода лежит плита в 900 мм.</summary>
        [Test]
        public void TopCap_OpeningReachingTheWallTop_KeepsNoMillimetreInsideIt()
        {
            var doorX = NormX(0f, DoorWidthMM);
            var solid = TopCapOf((doorX, DoorOpeningLayout.FullHeightSpanNorm));

            Assert.AreEqual(0f, CoveredMM(solid, doorX), 0.01f,
                "в дверном проёме опущенной стены на её верхней отметке не должно "
                + "оставаться ни миллиметра стены — это и есть полоса поперёк прохода");
            Assert.AreEqual(WallLenMM - DoorWidthMM, TotalMM(solid), 0.01f,
                "по бокам от проёма верхняя крышка обязана уцелеть: без неё сверху "
                + "видно нутро стены");
        }

        /// <summary>Обратная половина пары. Окно до верха стены не доходит, и
        /// верхнюю крышку резать нечем: над окном стена настоящая.</summary>
        [Test]
        public void TopCap_OpeningBelowTheWallTop_StaysWhole()
        {
            var winX = NormX(0f, DoorWidthMM);
            var solid = TopCapOf((winX, NormY(600f, 1800f)));

            Assert.AreEqual(WallLenMM, TotalMM(solid), 0.01f,
                "вырез не доходит до верха стены, значит над ним стена — "
                + "крышку резать нечем");
        }

        /// <summary>Дверь до пола: нижнюю крышку режет (порог), верхнюю на полной
        /// стене — нет. Одна и та же арифметика отвечает на оба вопроса разными
        /// ответами только потому, что спрашивают про разные края.</summary>
        [Test]
        public void FullHeightWall_DoorToTheFloor_CutsTheBottomCapOnly()
        {
            var doorX = NormX(0f, DoorWidthMM);
            var doorY = DoorOpeningLayout.GroundedSpanNorm(
                NormY(0f, 2100f).Min + NormY(0f, 2100f).Size * 0.5f,
                NormY(0f, 2100f).Size * 0.5f);

            Assert.AreEqual(0f, CoveredMM(BottomCapOf((doorX, doorY)), doorX), 0.01f,
                "под дверью пола стены быть не должно — это порог");
            Assert.AreEqual(WallLenMM, TotalMM(TopCapOf((doorX, doorY))), 0.01f,
                "дверь 2100 мм в стене 2500 мм до потолка не достаёт: верхнюю крышку "
                + "она не режет");
        }

        /// <summary>Окно ничего не режет ни сверху, ни снизу: подоконная часть
        /// стены настоящая, и в опущенной полоске она обязана остаться сплошной.</summary>
        [Test]
        public void BottomCap_WindowAboveTheFloor_StaysWhole()
        {
            var winX = NormX(0f, DoorWidthMM);
            var winY = NormY(600f, 1800f);

            Assert.AreEqual(WallLenMM, TotalMM(BottomCapOf((winX, winY))), 0.01f,
                "под подоконником стена стоит на полу, крышку резать нечем");
        }

        /// <summary>Два проёма в одной стене режут крышку двумя дырами, а не одной:
        /// перемычка между ними — настоящая стена. Проверяется тем, что остаётся
        /// ТРИ куска, и их сумма равна длине стены минус две ширины.</summary>
        [Test]
        public void Cap_TwoOpenings_KeepsThePierBetweenThem()
        {
            var leftX = NormX(-900f, DoorWidthMM);
            var rightX = NormX(900f, DoorWidthMM);
            var solid = TopCapOf(
                (leftX, DoorOpeningLayout.FullHeightSpanNorm),
                (rightX, DoorOpeningLayout.FullHeightSpanNorm));

            Assert.AreEqual(3, solid.Count,
                "две дыры в одной плите оставляют три куска: край, простенок, край");
            Assert.AreEqual(WallLenMM - 2f * DoorWidthMM, TotalMM(solid), 0.01f,
                "простенок между проёмами — настоящая стена, его резать нечем");
        }
    }
}
