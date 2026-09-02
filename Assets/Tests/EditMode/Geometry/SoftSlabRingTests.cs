using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

namespace KitchenDesigner.Tests.Geometry
{
    /// <summary>Сечение кольца знает СВОЮ нормаль. Раньше это знание лежало
    /// приватной копией сразу в двух строителях — SoftSlabSurface и
    /// BasinSurface, — дословно одинаковой, шесть строк. Копия опасна не
    /// размером: нормаль здесь единственное, что отличает мягкую кромку от
    /// острой, и разъехавшись на один знак она не роняет ни один прогон, а
    /// просто гасит грань при взгляде с одной стороны.
    ///
    /// Что именно кодирует пара (Radial, Up): доли ГОРИЗОНТАЛЬНОЙ и
    /// ВЕРТИКАЛЬНОЙ составляющих нормали. Radial умножается на внешнюю
    /// нормаль контура в плане, поэтому одна и та же дуга обслуживает и
    /// выпуклую кромку плиты (Radial > 0, нормаль наружу), и вогнутую стенку
    /// чаши ванны (Radial &lt; 0, нормаль внутрь). Именно это и позволило
    /// ванне переиспользовать сечения мягкой плиты, а не заводить свои.
    ///
    /// Запасная ветка (обе доли нулевые) недостижима из обоих строителей: у
    /// каждого сечения хотя бы одна доля ненулевая. Она всё равно обязана
    /// возвращать ЕДИНИЧНЫЙ вектор, а не ноль: нулевая нормаль в Unity даёт
    /// чёрную грань, и искать её потом пришлось бы глазами.</summary>
    public class SoftSlabRingTests
    {
        private const float Tol = 1e-5f;

        private static readonly Vector2 Corner =
            new Vector2(Mathf.Sqrt(0.5f), Mathf.Sqrt(0.5f));

        [Test]
        public void AFlatRing_PointsStraightAlongItsVerticalShare()
        {
            var up = new SoftSlabRing(0f, 0f, 0f, 1f).Normal(Corner);
            var down = new SoftSlabRing(0f, 0f, 0f, -1f).Normal(Corner);

            Assert.AreEqual(Vector3.up, up,
                "плоская крышка обязана смотреть строго вверх: горизонтальная доля нулевая, "
                + "и план не должен подмешиваться в нормаль вовсе");
            Assert.AreEqual(Vector3.down, down, "плоское дно");
        }

        [Test]
        public void AVerticalRing_TakesItsDirectionFromThePlanOutward()
        {
            var outward = new SoftSlabRing(0f, 0f, 1f, 0f).Normal(Corner);

            Assert.AreEqual(0f, outward.y, Tol, "вертикальная стенка не смотрит ни вверх, "
                + "ни вниз");
            Assert.AreEqual(Corner.x, outward.x, Tol, "нормаль стенки идёт по внешней "
                + "нормали контура — на скруглённом углу это не ось, а направление дуги");
            Assert.AreEqual(Corner.y, outward.z, Tol, "то же по второй оси плана");
        }

        [Test]
        public void ANegativeRadialShare_TurnsTheSameArcInsideOut()
        {
            var wall = new SoftSlabRing(0f, 0f, 1f, 0f).Normal(Corner);
            var bowl = new SoftSlabRing(0f, 0f, -1f, 0f).Normal(Corner);

            Assert.AreEqual(-1f, Vector3.Dot(wall, bowl), Tol,
                "чаша ванны переиспользует сечения мягкой плиты ровно за счёт знака: "
                + "тот же контур, нормаль внутрь. Разъехались бы — стенка чаши стала бы "
                + "невидимой изнутри, а прогон остался бы зелёным");
        }

        [Test]
        public void EveryRing_ReturnsAUnitNormal_IncludingTheUnreachableFallback()
        {
            foreach (var ring in new[]
                {
                    new SoftSlabRing(0f, 0f, 1f, 0f),
                    new SoftSlabRing(0f, 0f, 0f, 1f),
                    new SoftSlabRing(0f, 0f, -0.5f, 0.5f),
                    new SoftSlabRing(0f, 0f, 0f, 0f),
                })
                Assert.AreEqual(1f, ring.Normal(Corner).magnitude, Tol,
                    "нормаль обязана быть единичной при любых долях, включая обе нулевые: "
                    + "нулевой вектор в Unity даёт чёрную грань, которую ищут глазами");
        }
    }
}
