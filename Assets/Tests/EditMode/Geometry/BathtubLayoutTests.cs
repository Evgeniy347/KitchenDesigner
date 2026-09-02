using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

namespace KitchenDesigner.Tests.Geometry
{
    /// <summary>Ванна — единственный элемент проекта, у которого ЧЕТЫРЕ
    /// параметра формы завязаны друг на друга и все четыре подрезаются
    /// габаритом, который пользователь тянет мышью. Борт зависит от плана,
    /// радиус чаши — от плана И борта, глубина чаши — от высоты, скругление
    /// дна — от плана, борта И глубины. Здесь эта лестница и живёт: в элементе
    /// её быть не должно, а без dotnet-тестов её нельзя гонять за 0,3 с.
    ///
    /// Ключевое решение, которое держат эти тесты: НАРУЖНЫЙ радиус — не пятое
    /// свойство, а производная. Пользователь задаёт радиус ЧАШИ, потому что
    /// именно её видно; корпус получает radius + борт. Это ровно эквидистанта:
    /// сжатие скруглённого прямоугольника внутрь на d даёт стороны −2d и
    /// радиусы −d, поэтому борт выходит одинаковой ширины и на прямом участке,
    /// и в углу. Задай наружный радиус отдельно — и в углу борт станет то шире,
    /// то уже, а при малом наружном радиусе чаша получит квадратные углы внутри
    /// круглого корпуса.
    ///
    /// Обе крайности обязаны выглядеть осмысленно, и это не совпадение, а
    /// причина выбрать такую параметризацию. Радиус чаши 0 — строгая
    /// прямоугольная ванна, у которой снаружи скруглён только борт. Радиус
    /// чаши в максимуме — ванна с полукруглыми торцами. Одним числом, без
    /// переключателя формы.
    ///
    /// Второе, что здесь удерживается, — подрезка НИКОГДА не выдаёт
    /// вырожденную ванну. Под чашей обязан остаться материал (иначе ванна стоит
    /// дырой в пол), внутри борта обязана остаться чаша, а на дне чаши —
    /// плоская площадка. Все три проверяются перебором габаритов, потому что
    /// ломается это не на умолчаниях, а когда пользователь ужимает ванну до
    /// детской.</summary>
    public class BathtubLayoutTests
    {
        private static IEnumerable<Vector3Int> Sizes()
        {
            foreach (int w in new[] { 300, 700, 1200, 1700, 2400 })
            foreach (int d in new[] { 250, 500, 700, 1100 })
            foreach (int h in new[] { 60, 200, 420, 600, 900 })
                yield return new Vector3Int(w, h, d);
        }

        private static readonly int[] Asked = { -500, 0, 5, 40, 120, 450, 5000 };

        private static void Sweep(System.Func<Vector3Int, int, string?> check)
        {
            var broken = new List<string>();
            foreach (var dims in Sizes())
            foreach (int asked in Asked)
            {
                string? failure = check(dims, asked);
                if (failure != null) broken.Add(dims + " ← " + asked + ": " + failure);
            }

            Assert.IsEmpty(broken,
                "подрезка выдала ванну, которую нельзя построить. Ломается это не на "
                + "умолчаниях, а когда габарит ужимают мышью, и каждая строка ниже — "
                + "«габарит ← запрошенное значение: что вышло»:\n"
                + string.Join("\n", broken));
        }

        [Test]
        public void EveryDefault_SurvivesItsOwnClampAtTheDefaultSize()
        {
            var dims = BathtubLayout.DefaultDimensionsMM;

            Assert.AreEqual(BathtubLayout.DefaultRimWidthMM,
                BathtubLayout.ClampRimWidthMM(dims, BathtubLayout.DefaultRimWidthMM), "борт");
            Assert.AreEqual(BathtubLayout.DefaultBowlRadiusMM,
                BathtubLayout.ClampBowlRadiusMM(dims, BathtubLayout.DefaultRimWidthMM,
                    BathtubLayout.DefaultBowlRadiusMM), "радиус чаши");
            Assert.AreEqual(BathtubLayout.DefaultBowlDepthMM,
                BathtubLayout.ClampBowlDepthMM(dims, BathtubLayout.DefaultBowlDepthMM),
                "глубина чаши");
            Assert.AreEqual(BathtubLayout.DefaultBowlFilletMM,
                BathtubLayout.ClampBowlFilletMM(dims, BathtubLayout.DefaultRimWidthMM,
                    BathtubLayout.DefaultBowlDepthMM, BathtubLayout.DefaultBowlFilletMM),
                "скругление дна: умолчание вне собственного диапазона молча съезжает при "
                + "первой же перестройке, и элемент рождается не тем, что объявлено");
        }

        [Test]
        public void TheRim_AlwaysLeavesABowlInsideIt()
        {
            Sweep((dims, asked) =>
            {
                int rim = BathtubLayout.ClampRimWidthMM(dims, asked);
                return 2 * rim < Mathf.Min(dims.x, dims.z)
                    ? null
                    : "борт " + rim + " съел чашу целиком";
            });
        }

        [Test]
        public void TheBowl_AlwaysLeavesMaterialUnderItself()
        {
            Sweep((dims, asked) =>
            {
                float floor = BathtubLayout.BowlFloorYMM(dims, asked);
                return floor > -dims.y * 0.5f
                    ? null
                    : "дно чаши на " + floor + " при полувысоте " + dims.y * 0.5f
                        + " — ванна стоит дырой в пол";
            });
        }

        [Test]
        public void TheBottomFillet_NeverClosesTheBowlFloorToAPoint()
        {
            Sweep((dims, asked) =>
            {
                int rim = BathtubLayout.ClampRimWidthMM(dims, BathtubLayout.DefaultRimWidthMM);
                int fillet = BathtubLayout.ClampBowlFilletMM(dims, rim,
                    BathtubLayout.DefaultBowlDepthMM, asked);
                int floorSide = BathtubLayout.InnerSideMM(dims, rim) - 2 * fillet;
                return floorSide > 0
                    ? null
                    : "площадка дна " + floorSide + " мм при скруглении " + fillet;
            });
        }

        [Test]
        public void TheBottomFillet_NeverReachesHigherThanTheRim()
        {
            Sweep((dims, asked) =>
            {
                int rim = BathtubLayout.ClampRimWidthMM(dims, BathtubLayout.DefaultRimWidthMM);
                int depth = BathtubLayout.ClampBowlDepthMM(dims, asked);
                int fillet = BathtubLayout.ClampBowlFilletMM(dims, rim, asked,
                    BathtubLayout.DefaultBowlFilletMM);
                return fillet <= depth
                    ? null
                    : "дуга дна " + fillet + " выше самой чаши " + depth;
            });
        }

        [Test]
        public void TheShellRadius_IsTheBowlRadiusPushedOutByExactlyTheRim()
        {
            Sweep((dims, asked) =>
            {
                int rim = BathtubLayout.ClampRimWidthMM(dims, BathtubLayout.DefaultRimWidthMM);
                int bowl = BathtubLayout.ClampBowlRadiusMM(dims, rim, asked);
                int shell = BathtubLayout.ShellCornerRadiusMM(dims, rim, asked);
                float recovered = CornerRadii.Uniform(shell).Inset(rim).PlusXPlusZ;

                return Mathf.Approximately(recovered, bowl)
                    ? null
                    : "чаша просила радиус " + bowl + ", а корпус, сжатый на борт "
                        + rim + ", вернул " + recovered;
            });
        }

        [Test]
        public void TheShellRadius_AlwaysFitsThePlanWithoutBeingShrunkByTheProfile()
        {
            Sweep((dims, asked) =>
            {
                int rim = BathtubLayout.ClampRimWidthMM(dims, BathtubLayout.DefaultRimWidthMM);
                int shell = BathtubLayout.ShellCornerRadiusMM(dims, rim, asked);
                float fitted = RoundedRectProfile
                    .Fit(dims.x, dims.z, CornerRadii.Uniform(shell)).PlusXPlusZ;

                return Mathf.Abs(fitted - shell) < 1e-3f
                    ? null
                    : "RoundedRectProfile ужал корпус с " + shell + " до " + fitted
                        + ": подрезка радиуса чаши обязана учитывать борт, иначе борт "
                        + "в углу окажется уже, чем на прямом участке";
            });
        }

        [Test]
        public void AZeroBowlRadius_IsAllowedAndKeepsTheShellRoundedByTheRimAlone()
        {
            var dims = BathtubLayout.DefaultDimensionsMM;
            int rim = BathtubLayout.ClampRimWidthMM(dims, BathtubLayout.DefaultRimWidthMM);

            Assert.AreEqual(0, BathtubLayout.ClampBowlRadiusMM(dims, rim, 0),
                "строго прямоугольная чаша — законная ванна, а не ошибка ввода");
            Assert.AreEqual(rim, BathtubLayout.ShellCornerRadiusMM(dims, rim, 0),
                "при прямой чаше корпус скруглён ровно на ширину борта: острая кромка "
                + "борта нулевой толщины в акриле не существует");
        }

        [Test]
        public void TheLargestBowlRadius_TurnsTheTubIntoASemicircularEnd()
        {
            var dims = BathtubLayout.DefaultDimensionsMM;
            int rim = BathtubLayout.ClampRimWidthMM(dims, BathtubLayout.DefaultRimWidthMM);
            int biggest = BathtubLayout.MaxBowlRadiusMM(dims, rim);

            Assert.AreEqual(dims.z * 0.5f,
                BathtubLayout.ShellCornerRadiusMM(dims, rim, biggest), 0.51f,
                "в максимуме корпус обязан доходить ровно до полукруга по узкой стороне — "
                + "это верхняя граница, за которой RoundedRectProfile начал бы ужимать "
                + "радиус сам, и борт в углу поехал бы");
        }
    }
}
