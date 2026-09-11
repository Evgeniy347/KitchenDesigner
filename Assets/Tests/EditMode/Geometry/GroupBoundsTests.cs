using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

namespace KitchenDesigner.Tests.Geometry
{
    /// <summary>Общий габарит группы. Считается из ВЕРШИН каждого участника
    /// (GetVerticesAt), а не из его transform: у повёрнутой детали коробка шире
    /// собственных размеров, и габарит, сложенный из позиций и DimensionsMM, срезал
    /// бы углы. Грани общей коробки выдаёт тот же GappedBox, что и деталь, — иначе
    /// у ручек группы оказался бы второй порядок граней, а ResizeHandleManager
    /// выводит ось из index/2.</summary>
    public class GroupBoundsTests
    {

        private static Vector3[] Box(Vector3 center, Vector3 size) =>
            GappedBox.Vertices(size, BoxGaps.None, center, Quaternion.identity);

        [Test]
        public void Of_OneMember_GivesExactlyItsOwnBox()
        {
            var sets = new List<Vector3[]> { Box(new Vector3(1f, 2f, 3f), new Vector3(0.8f, 0.4f, 0.6f)) };

            Assert.IsTrue(GroupBounds.Of(sets, out var center, out var size),
                "непустое выделение обязано дать габарит, иначе рисовать нечего");
            Assert.AreEqual(1f, center.x, 1e-4f, "один участник — общий габарит равен его собственной коробке");
            Assert.AreEqual(2f, center.y, 1e-4f, "один участник — общий габарит равен его собственной коробке");
            Assert.AreEqual(3f, center.z, 1e-4f, "один участник — общий габарит равен его собственной коробке");
            Assert.AreEqual(0.8f, size.x, 1e-4f, "один участник — общий габарит равен его собственной коробке");
            Assert.AreEqual(0.4f, size.y, 1e-4f, "один участник — общий габарит равен его собственной коробке");
            Assert.AreEqual(0.6f, size.z, 1e-4f, "один участник — общий габарит равен его собственной коробке");
        }

        [Test]
        public void Of_TwoMembers_GivesTheirUnion_NotOneOfThem()
        {
            var sets = new List<Vector3[]>
            {
                Box(new Vector3(0f, 0f, 0f), new Vector3(1f, 1f, 1f)),
                Box(new Vector3(3f, 0f, 0f), new Vector3(1f, 1f, 1f)),
            };

            Assert.IsTrue(GroupBounds.Of(sets, out var center, out var size),
                "два участника — габарит есть");
            Assert.AreEqual(1.5f, center.x, 1e-4f, "середина между -0,5 и 3,5, а не центр первого");
            Assert.AreEqual(4f, size.x, 1e-4f, "два участника — объединение, а не коробка первого");
            Assert.AreEqual(1f, size.y, 1e-4f, "по Y участники совпадают — габарит не обязан расти");
            Assert.AreEqual(1f, size.z, 1e-4f, "по Z участники совпадают — габарит не обязан расти");
        }

        [Test]
        public void Of_ARotatedMember_WidensTheGroupBeyondItsOwnDimensions()
        {
            var turned = GappedBox.Vertices(new Vector3(1f, 0.1f, 0.2f), BoxGaps.None,
                Vector3.zero, ManagedRotation.RotY(45f));

            Assert.IsTrue(GroupBounds.Of(new List<Vector3[]> { turned }, out _, out var size),
                "повёрнутый участник тоже даёт габарит");
            Assert.Greater(size.x, 0.7f,
                "повёрнутая на 45° деталь занимает по X больше половины своей длины: "
                + "габарит из вершин это видит, габарит из DimensionsMM — нет");
            Assert.Greater(size.z, 0.7f,
                "по Z то же самое, и обе оси нужны: одна ось прошла бы и у габарита, "
                + "посчитанного без поворота");
        }

        [Test]
        public void Of_NoMembers_ReportsFailure_AndLeavesTheBoxAtZero()
        {
            Assert.IsFalse(GroupBounds.Of(new List<Vector3[]>(), out var center, out var size),
                "пустое выделение габарита не имеет — «ложь» здесь несущая: иначе "
                + "коробка нулевого размера нарисовалась бы в начале координат");
            Assert.AreEqual(Vector3.zero, center, "отказ не оставляет за собой мусорных координат");
            Assert.AreEqual(Vector3.zero, size, "отказ не оставляет за собой мусорного размера");
            Assert.IsFalse(GroupBounds.Of(null, out _, out _),
                "отсутствующий список — тот же отказ, а не NullReferenceException в LateUpdate");
        }

        [Test]
        public void FacesOf_KeepsTheFaceOrderContract_AxisIsIndexOverTwo_EvenIsPositive()
        {
            var center = new Vector3(1f, 2f, 3f);
            var size = new Vector3(0.4f, 0.6f, 0.8f);
            var faces = GroupBounds.FacesOf(center, size);

            Assert.AreEqual(Face.BoxFaceCount, faces.Length,
                "у общей коробки шесть граней — по стрелке на каждую сторону переноса");
            var axes = new[] { Vector3.right, Vector3.up, Vector3.forward };
            for (int i = 0; i < faces.Length; i++)
            {
                Vector3 axis = axes[i / 2];
                float sign = i % 2 == 0 ? 1f : -1f;
                Assert.AreEqual(1f, Vector3.Dot(faces[i].normal.normalized, axis * sign), 1e-4f,
                    $"грань {i}: ось = index/2, чётный индекс — положительное направление");
                Assert.AreEqual(sign * size[i / 2] * 0.5f,
                    Vector3.Dot(faces[i].center - center, axis), 1e-4f,
                    $"грань {i} стоит на половине габарита группы, а не на габарите участника");
            }
        }
    }
}
