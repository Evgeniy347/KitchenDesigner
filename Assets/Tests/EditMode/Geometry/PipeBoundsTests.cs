using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

namespace KitchenDesigner.Tests.Geometry
{
    /// <summary>Габарит трубчатой сборки. Сантехника — это набор цилиндров,
    /// и наивный габарит «точка ± радиус» врёт ровно там, где это больнее
    /// всего: настенный элемент опирается ЗАДНЕЙ гранью коробки на плоскость
    /// стены, а отражатель смесителя — это цилиндр, чья ось перпендикулярна
    /// стене и чей торец лежит в самой плоскости. Раздув торцевой диск на
    /// радиус во все стороны, габарит уехал бы на 31 мм ВНУТРЬ стены, и
    /// смеситель повис бы в воздухе на этом же расстоянии.
    ///
    /// Поэтому диск считается честно: вдоль собственной оси он не занимает
    /// ничего, а по остальным осям — r*sqrt(1 - d_i^2). Для осевого цилиндра
    /// это ноль вдоль оси и r поперёк, для косого излива — промежуточное
    /// значение, и коробка всё ещё гарантированно накрывает меш.</summary>
    public class PipeBoundsTests
    {
        private const float Tol = 1e-3f;

        [Test]
        public void PipeBounds_Of_DoesNotGrowAnAxialCylinderAlongItsOwnAxis()
        {
            var bounds = PipeBounds.Of(new[]
                { new PipeSegment(Vector3.zero, new Vector3(0f, 0f, 12f), 31f) });

            Assert.AreEqual(0f, bounds.min.z, Tol,
                "торец отражателя лежит в плоскости стены: расширив его на радиус вдоль "
                + "собственной оси, габарит увёл бы заднюю грань элемента внутрь стены");
            Assert.AreEqual(12f, bounds.max.z, Tol,
                "передний торец обязан остаться там, где он есть");
            Assert.AreEqual(31f, bounds.max.x, Tol,
                "поперёк оси диск занимает ровно свой радиус");
            Assert.AreEqual(31f, bounds.max.y, Tol,
                "поперёк оси диск занимает ровно свой радиус по обеим поперечным осям");
        }

        [Test]
        public void PipeBounds_Of_CoversTheWholeDiscOfADiagonalSegment()
        {
            var diagonal = new Vector3(100f, 100f, 0f);
            var bounds = PipeBounds.Of(new[] { new PipeSegment(Vector3.zero, diagonal, 10f) });

            float expected = 10f * Mathf.Sqrt(0.5f);
            Assert.AreEqual(expected, -bounds.min.x, Tol,
                "у отрезка под 45° диск наклонён: вдоль X он занимает r/sqrt(2), а не 0 и не r");
            Assert.AreEqual(10f, bounds.max.z, Tol,
                "перпендикулярно плоскости отрезка диск занимает весь радиус");
        }

        [Test]
        public void PipeBounds_Of_TakesTheEndRadiusOfATaperedSegment()
        {
            var bounds = PipeBounds.Of(new[]
                { new PipeSegment(Vector3.zero, new Vector3(0f, -50f, 0f), 20f, 6f) });

            Assert.AreEqual(20f, bounds.max.x, Tol,
                "широкий торец конического излива задаёт габарит у основания");
            Assert.AreEqual(-50f, bounds.min.y, Tol,
                "узкий торец не должен выпирать вдоль оси");
        }

        [Test]
        public void PipeBounds_Of_ReturnsAnEmptyBoxForNoSegments()
        {
            var bounds = PipeBounds.Of(new PipeSegment[0]);

            Assert.AreEqual(Vector3.zero, bounds.size,
                "пустая сборка не имеет габарита: float.MaxValue в min и MinValue в max дали "
                + "бы NaN-размер и утащили бы за собой габарит всего элемента");
        }

        [Test]
        public void PipeBounds_OfPolyline_WrapsEveryPointByTheHoseRadius()
        {
            var bounds = PipeBounds.OfPolyline(
                new[] { Vector3.zero, new Vector3(0f, -200f, 100f), new Vector3(0f, 0f, 200f) },
                7.5f);

            Assert.AreEqual(-207.5f, bounds.min.y, Tol,
                "нижняя точка петли шланга опускает габарит ещё на радиус шланга");
            Assert.AreEqual(207.5f, bounds.max.z, Tol,
                "по ломаной радиус берётся во все стороны: направление в узле не определено");
        }
    }
}
