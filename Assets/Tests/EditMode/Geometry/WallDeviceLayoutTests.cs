using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

namespace KitchenDesigner.Tests.Geometry
{
    /// <summary>Розетка и выключатель — это одна и та же коробка на стене:
    /// рамка по периметру, за ней углубление, за углублением корпус. Отличаются
    /// они только тем, что стоит в проёме рамки — круглый колодец с контактами
    /// или клавиша.
    ///
    /// Числа здесь — умолчания (рамка 80×80, вынос 10 мм), а не константы:
    /// пользователь правит их в панели. Поэтому проверяются СВЯЗИ, которые
    /// обязаны держаться при любом наборе значений.
    ///
    /// Связь первая: плоскость стены — это z = -вынос/2, и ничто не имеет права
    /// уйти за неё. Посадка на стену (WallSeating.Seat) отодвигает центр от
    /// грани стены ровно на половину глубины габарита, так что задняя грань
    /// габарита ЛЕЖИТ на стене. Деталь, вылезшая назад, утонет в стене на
    /// собственную ошибку и никто этого не заметит.
    ///
    /// Связь вторая: вперёд дальше рамки не торчит ничего. Рамка — это самая
    /// передняя точка устройства, и габарит по z равен выносу; деталь впереди
    /// рамки означала бы, что габарит врёт о форме (CONVENTIONS: «меш и его
    /// метаданные описывают ОДНУ фигуру»).
    ///
    /// Связь третья: вынос делится на три без остатка — рамка, углубление,
    /// корпус. Если сумма перестанет сходиться, углубление либо исчезнет, либо
    /// прогрызёт корпус насквозь.
    ///
    /// Связь четвёртая: посты стоят вплотную и симметрично относительно центра.
    /// Крайняя грань крайнего поста — это ровно половина общей ширины, иначе
    /// двойная розетка окажется шире или уже своего же габарита.</summary>
    public class WallDeviceLayoutTests
    {
        private const float Tol = 1e-3f;

        private static float BackZ(FurniturePartBox part) => part.CentreMM.z - part.SizeMM.z * 0.5f;

        private static float FrontZ(FurniturePartBox part) => part.CentreMM.z + part.SizeMM.z * 0.5f;

        private static IEnumerable<FurniturePartBox> EveryPartOfBothDevices(
            int plateW, int plateH, int protrusion, int posts)
        {
            var all = new List<FurniturePartBox>();
            all.AddRange(SocketLayout.BodyParts(plateW, plateH, protrusion, posts));
            all.AddRange(SocketLayout.ContactParts(plateW, plateH, protrusion, posts));
            all.AddRange(LightSwitchLayout.BodyParts(plateW, plateH, protrusion, posts));
            all.AddRange(LightSwitchLayout.KeyParts(plateW, plateH, protrusion, posts));
            return all;
        }

        [Test]
        public void WallDeviceLayout_TheThreeDepthZones_AddUpToTheProtrusionExactly(
            [Values(3, 10, 25, 60)] int protrusion)
        {
            float sum = WallDeviceLayout.FrameDepthMM(protrusion)
                + WallDeviceLayout.RecessDepthMM(protrusion)
                + WallDeviceLayout.BodyDepthMM(protrusion);

            Assert.AreEqual(protrusion, sum, Tol,
                "Рамка + углубление + корпус обязаны дать ровно вынос: иначе углубление "
                + "либо схлопнется, либо прорежет корпус насквозь");
            Assert.Greater(WallDeviceLayout.BodyDepthMM(protrusion), 0f,
                "Корпусу должно остаться место позади углубления");
        }

        [Test]
        public void WallDeviceLayout_NoPart_ReachesBehindTheWallPlane(
            [Values(40, 80, 200)] int plateSide,
            [Values(3, 10, 60)] int protrusion,
            [Values(1, 2, 3)] int posts)
        {
            float wallPlaneZ = -WallDeviceLayout.FrontZMM(protrusion);

            foreach (var part in EveryPartOfBothDevices(plateSide, plateSide, protrusion, posts))
                Assert.GreaterOrEqual(BackZ(part), wallPlaneZ - Tol,
                    $"Деталь {part.Name} уходит за плоскость стены — посадка утопит её в стену");
        }

        [Test]
        public void WallDeviceLayout_NoPart_StandsProudOfTheFrame(
            [Values(40, 80, 200)] int plateSide,
            [Values(3, 10, 60)] int protrusion,
            [Values(1, 2, 3)] int posts)
        {
            float frontZ = WallDeviceLayout.FrontZMM(protrusion);

            foreach (var part in EveryPartOfBothDevices(plateSide, plateSide, protrusion, posts))
                Assert.LessOrEqual(FrontZ(part), frontZ + Tol,
                    $"Деталь {part.Name} торчит впереди рамки — габарит по z перестал "
                    + "описывать форму");
        }

        [Test]
        public void WallDeviceLayout_TheOutermostPostEdge_IsExactlyHalfTheTotalWidth(
            [Values(1, 2, 3)] int posts)
        {
            const int plateW = 80;
            float half = WallDeviceLayout.TotalWidthMM(plateW, posts) * 0.5f;
            float last = WallDeviceLayout.PostCentreXMM(posts - 1, plateW, posts);
            float first = WallDeviceLayout.PostCentreXMM(0, plateW, posts);

            Assert.AreEqual(half, last + plateW * 0.5f, Tol,
                "Правая грань крайнего поста обязана совпасть с правой гранью габарита");
            Assert.AreEqual(-half, first - plateW * 0.5f, Tol,
                "Левая грань крайнего поста обязана совпасть с левой гранью габарита");
        }

        [Test]
        public void WallDeviceLayout_Posts_StandFlushAgainstEachOther()
        {
            const int plateW = 80;
            float a = WallDeviceLayout.PostCentreXMM(0, plateW, 3);
            float b = WallDeviceLayout.PostCentreXMM(1, plateW, 3);

            Assert.AreEqual(plateW, b - a, Tol,
                "Соседние посты стоят вплотную: шаг между центрами равен ширине рамки");
            Assert.AreEqual(0f, WallDeviceLayout.PostCentreXMM(1, plateW, 3), Tol,
                "Средний пост тройного блока стоит в центре элемента");
        }

        [Test]
        public void WallDeviceLayout_Dimensions_GrowOnlyInWidthWithThePostCount()
        {
            var single = WallDeviceLayout.DimensionsMM(80, 80, 10, 1);
            var triple = WallDeviceLayout.DimensionsMM(80, 80, 10, 3);

            Assert.AreEqual(new Vector3Int(80, 80, 10), single,
                "Одиночная розетка ровно такая, какой её задали в панели");
            Assert.AreEqual(new Vector3Int(240, 80, 10), triple,
                "Три поста втрое шире одного и ровно той же высоты и выноса");
        }

        [Test]
        public void WallDeviceLayout_EverythingOutOfRange_IsClampedRatherThanAccepted()
        {
            Assert.AreEqual(WallDeviceLayout.MinPlateSideMM, WallDeviceLayout.ClampPlateSideMM(1),
                "Рамка меньше минимума перестаёт быть монтируемой в подрозетник");
            Assert.AreEqual(WallDeviceLayout.MaxPlateSideMM,
                WallDeviceLayout.ClampPlateSideMM(10000),
                "Рамка размером с шкаф — опечатка в поле, а не замысел");
            Assert.AreEqual(WallDeviceLayout.MinProtrusionMM,
                WallDeviceLayout.ClampProtrusionMM(0),
                "Нулевой вынос схлопнул бы все три зоны глубины в одну плоскость");
            Assert.AreEqual(WallDeviceLayout.MaxProtrusionMM,
                WallDeviceLayout.ClampProtrusionMM(999),
                "Вынос больше максимума превращает розетку в тумбу на стене");
            Assert.AreEqual(WallDeviceLayout.MinPostCount, WallDeviceLayout.ClampPostCount(0),
                "Ноль постов оставил бы элемент без единой детали");
            Assert.AreEqual(WallDeviceLayout.MaxPostCount, WallDeviceLayout.ClampPostCount(9),
                "Больше трёх постов не бывает в одной рамке");
        }

        [Test]
        public void WallDeviceLayout_TheRim_LeavesAnOpeningOnEverySide(
            [Values(40, 80, 200)] int plateSide)
        {
            float rim = WallDeviceLayout.RimWidthMM(plateSide, plateSide);

            Assert.GreaterOrEqual(rim, WallDeviceLayout.MinRimWidthMM,
                "Рамка тоньше минимума перестаёт читаться как рамка");
            Assert.AreEqual(plateSide - 2f * rim,
                WallDeviceLayout.InnerWidthMM(plateSide, plateSide), Tol,
                "Проём — это рамка минус две её толщины, слева и справа");
            Assert.Greater(WallDeviceLayout.InnerSideMM(plateSide, plateSide), 0f,
                "Проём рамки не имеет права схлопнуться в ноль");
        }

        [Test]
        public void WallDeviceLayout_TheRim_IsFourBarsClosingTheWholePerimeter()
        {
            var parts = new List<FurniturePartBox>();
            WallDeviceLayout.AddRim(parts, 0, 0f, 80, 80, 10);

            Assert.AreEqual(4, parts.Count,
                "Рамка собирается из четырёх перекладин: верх, низ и две боковины");
            float rim = WallDeviceLayout.RimWidthMM(80, 80);

            var top = parts[0];
            var left = parts[2];
            Assert.AreEqual(80f, top.SizeMM.x, Tol,
                "Верхняя перекладина идёт во всю ширину, накрывая углы");
            Assert.AreEqual(rim, top.SizeMM.y, Tol,
                "Высота перекладины — это и есть толщина рамки");
            Assert.AreEqual(80f - 2f * rim, left.SizeMM.y, Tol,
                "Боковины встают МЕЖДУ перекладинами, иначе углы удвоятся");
            Assert.AreEqual(rim, left.SizeMM.x, Tol,
                "Ширина боковины — это та же толщина рамки");
        }

        [Test]
        public void WallDeviceLayout_AtTheSmallestPlate_TheRimStillLeavesRoomForTheOpening()
        {
            int side = WallDeviceLayout.MinPlateSideMM;

            Assert.Greater(WallDeviceLayout.InnerSideMM(side, side),
                WallDeviceLayout.MinRimWidthMM,
                "На минимальной рамке проём обязан остаться шире самой рамки — "
                + "иначе розетка вырождается в сплошную пластину");
        }
    }
}
