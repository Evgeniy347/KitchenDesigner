using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

namespace KitchenDesigner.Tests.Geometry
{
    /// <summary>Ломаная — единственное представление трассы трубы в этом
    /// проекте: и прямой участок штанги, и дуга гусака над тропическим душем,
    /// и петля шланга приходят в построитель меша одним и тем же массивом
    /// точек. Поэтому дуга строится ДВУМЯ базисными векторами, а не поворотом:
    /// Quaternion.AngleAxis и Matrix4x4 — это ECall, и на быстром пути под
    /// CoreCLR они падают с SecurityException (README ядра).
    ///
    /// Chain склеивает участки и выбрасывает совпадающие стыки: без этого в
    /// месте перехода прямой в дугу оказываются две точки на нуле друг от
    /// друга, направление между ними не определено, и кольцо протяжки в этом
    /// узле схлопывается в мусор.</summary>
    public class PipePathTests
    {
        private const float Tol = 1e-3f;

        [Test]
        public void PipePath_Arc_TurnsAVerticalRiserIntoAHorizontalArm()
        {
            var arc = PipePath.Arc(new Vector3(0f, 900f, 60f), Vector3.down, Vector3.forward,
                110f, 90f, PipePath.DefaultArcSegments);

            Assert.AreEqual(0f, (arc[0] - new Vector3(0f, 790f, 60f)).magnitude, Tol,
                "дуга начинается на оси штанги, ниже центра сгиба на радиус");
            Assert.AreEqual(0f, (arc[arc.Length - 1] - new Vector3(0f, 900f, 170f)).magnitude, Tol,
                "и заканчивается на горизонтальном выносе, впереди центра на тот же радиус");
            Assert.AreEqual(PipePath.DefaultArcSegments + 1, arc.Length,
                "N сегментов дают N+1 точку: потеря последней укоротила бы гусак на сегмент");
        }

        [Test]
        public void PipePath_Arc_KeepsEveryPointOnTheCircle()
        {
            var centre = new Vector3(0f, 900f, 60f);
            var arc = PipePath.Arc(centre, Vector3.down, Vector3.forward, 110f, 90f, 8);

            foreach (var point in arc)
                Assert.AreEqual(110f, (point - centre).magnitude, Tol,
                    "точка дуги уехала с окружности: гусак перестал бы быть дугой постоянного "
                    + "радиуса и получил бы излом");
        }

        [Test]
        public void PipePath_Chain_DropsTheDuplicatedJoint()
        {
            var joint = new Vector3(0f, 790f, 60f);
            var chained = PipePath.Chain(
                new[] { new Vector3(0f, 0f, 60f), joint },
                new[] { joint, new Vector3(0f, 900f, 170f) });

            Assert.AreEqual(3, chained.Length,
                "стык прямой и дуги — одна точка, а не две совпадающие: нулевой отрезок "
                + "не имеет направления и ломает кольцо протяжки");
        }

        [Test]
        public void PipePath_LengthMM_SumsTheSegmentsAndIgnoresASinglePoint()
        {
            Assert.AreEqual(700f, PipePath.LengthMM(new[]
                {
                    Vector3.zero, new Vector3(0f, 300f, 0f), new Vector3(400f, 300f, 0f),
                }), Tol,
                "длина ломаной — сумма звеньев");
            Assert.AreEqual(0f, PipePath.LengthMM(new[] { Vector3.one }), Tol,
                "одна точка не имеет длины и не должна ронять цикл");
        }

        [Test]
        public void PipePath_LowestPoint_FindsTheBottomOfTheLoop()
        {
            var bottom = new Vector3(0f, -180f, 90f);

            Assert.AreEqual(bottom, PipePath.LowestPoint(new[]
                { Vector3.zero, bottom, new Vector3(0f, 450f, 140f) }),
                "нижняя точка петли задаёт нижнюю грань габарита элемента");
        }
    }
}
