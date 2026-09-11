using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core.Handles;

namespace KitchenDesigner.Tests.Geometry
{
    /// <summary>Луч курсора против оси ручки: одна арифметика на две поверхности —
    /// ручку одиночной детали и ручку общего габарита группы. Пока формула жила
    /// приватным методом ResizeHandleManager, второй её носитель мог появиться
    /// только копией, а копия расходится молча.</summary>
    public class HandleAxisRayTests
    {
        [Test]
        public void APerpendicularRay_MeetsTheAxisAtTheDistanceItPointsAt()
        {
            float s = HandleAxisRay.ParamAlongAxis(
                rayOrigin: new Vector3(0f, 0f, -5f), rayDirection: Vector3.forward,
                axisPoint: Vector3.zero, axisDirection: Vector3.right);

            Assert.AreEqual(0f, s, 1e-4f, "луч проходит через саму точку оси");

            float shifted = HandleAxisRay.ParamAlongAxis(
                new Vector3(2f, 0f, -5f), Vector3.forward, Vector3.zero, Vector3.right);
            Assert.AreEqual(2f, shifted, 1e-4f,
                "сдвиг луча на 2 вдоль оси даёт параметр 2 — иначе перетаскивание "
                + "отставало бы от курсора на постоянный множитель");
        }

        [Test]
        public void TheParamIsMeasuredFromTheAxisPoint_NotFromTheWorldOrigin()
        {
            float s = HandleAxisRay.ParamAlongAxis(
                new Vector3(3f, 0f, -5f), Vector3.forward,
                axisPoint: new Vector3(1f, 0f, 0f), axisDirection: Vector3.right);

            Assert.AreEqual(2f, s, 1e-4f,
                "отрицательный контроль к предыдущему тесту: начало отсчёта переехало "
                + "на 1, и ответ обязан уменьшиться ровно на столько же");
        }

        [Test]
        public void LookingStraightDownTheAxis_HasNoAnswer()
        {
            float s = HandleAxisRay.ParamAlongAxis(
                new Vector3(-5f, 0f, 0f), Vector3.right, Vector3.zero, Vector3.right);

            Assert.IsTrue(float.IsNaN(s),
                "камера смотрит вдоль оси: пересечения нет, и молчаливый ноль увёл бы "
                + "деталь в начало оси одним скачком");
        }

        [Test]
        public void ParamAlongAxis_AnUnnormalisedAxis_GivesTheSameAnswerAsANormalisedOne()
        {
            float unit = HandleAxisRay.ParamAlongAxis(
                new Vector3(2f, 0f, -5f), Vector3.forward, Vector3.zero, Vector3.right);
            float scaled = HandleAxisRay.ParamAlongAxis(
                new Vector3(2f, 0f, -5f), new Vector3(0f, 0f, 7f), Vector3.zero,
                new Vector3(13f, 0f, 0f));

            Assert.AreEqual(unit, scaled, 1e-4f,
                "параметр измеряется в метрах вдоль оси, а не в её длинах: грань "
                + "отдаёт normal ненормированным, и множитель уехал бы в размер");
        }
    }
}
