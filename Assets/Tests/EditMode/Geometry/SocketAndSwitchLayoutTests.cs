using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

namespace KitchenDesigner.Tests.Geometry
{
    /// <summary>Что отличает розетку от выключателя — только содержимое проёма
    /// рамки. У розетки там круглый колодец, утопленный на глубину углубления,
    /// с двумя гнёздами под штыри и двумя заземляющими лапками. У выключателя
    /// там клавиша, вышедшая заподлицо с рамкой.
    ///
    /// Ни одна из этих деталей не вырезается булевой операцией — колодец и
    /// гнёзда читаются как углубление только потому, что стоят ПОЗАДИ рамки, а
    /// лапки — потому что стоят ВНУТРИ колодца. Стоит любой из них выехать
    /// вперёд или вылезти за окружность колодца, и вместо розетки получится
    /// нашлёпка. Именно эти отношения тут и заперты.
    ///
    /// Клавиша проверяется на обратное: она обязана выйти ровно вровень с
    /// рамкой (утоплённая клавиша читается как вторая розетка) и обязана
    /// оставить зазор со всех четырёх сторон (клавиша впритык сливается с
    /// рамкой в сплошную пластину).</summary>
    public class SocketAndSwitchLayoutTests
    {
        private const float Tol = 1e-3f;

        private const int PlateW = 80;
        private const int PlateH = 80;
        private const int Protrusion = 10;

        private static float BackZ(FurniturePartBox p) => p.CentreMM.z - p.SizeMM.z * 0.5f;

        private static float FrontZ(FurniturePartBox p) => p.CentreMM.z + p.SizeMM.z * 0.5f;

        private static FurniturePartBox Named(IEnumerable<FurniturePartBox> parts, string name)
        {
            foreach (var p in parts) if (p.Name == name) return p;
            Assert.Fail($"Раскладка не выдала детали {name} — тест ниже проверял бы пустоту");
            return default;
        }

        private static float RimBackZ(int protrusionMM)
            => WallDeviceLayout.FrontZMM(protrusionMM) - WallDeviceLayout.FrameDepthMM(protrusionMM);

        [Test]
        public void SocketLayout_ThePartCount_IsFivePerPostPlusFourContacts(
            [Values(1, 2, 3)] int posts)
        {
            Assert.AreEqual(posts * 5,
                SocketLayout.BodyParts(PlateW, PlateH, Protrusion, posts).Count,
                "На пост приходятся четыре перекладины рамки и один колодец");
            Assert.AreEqual(posts * 4,
                SocketLayout.ContactParts(PlateW, PlateH, Protrusion, posts).Count,
                "На пост приходятся два гнезда и две заземляющие лапки");
        }

        [Test]
        public void SocketLayout_TheWell_SitsExactlyTheRecessDepthBehindTheFrame(
            [Values(3, 10, 60)] int protrusion)
        {
            var well = Named(SocketLayout.BodyParts(PlateW, PlateH, protrusion, 1),
                SocketLayout.WellName + "0");

            Assert.AreEqual(RimBackZ(protrusion) - WallDeviceLayout.RecessDepthMM(protrusion),
                FrontZ(well), Tol,
                "Дно колодца обязано отстоять от изнанки рамки ровно на глубину углубления — "
                + "иначе углубления не видно");
            Assert.AreEqual(-WallDeviceLayout.FrontZMM(protrusion), BackZ(well), Tol,
                "Задняя грань колодца лежит на плоскости стены");
        }

        [Test]
        public void SocketLayout_TheWell_FitsInsideTheFrameOpening(
            [Values(40, 80, 200)] int plateSide)
        {
            Assert.LessOrEqual(SocketLayout.WellDiameterMM(plateSide, plateSide),
                WallDeviceLayout.InnerSideMM(plateSide, plateSide) + Tol,
                "Колодец шире проёма перекрыл бы рамку изнутри");
        }

        [Test]
        public void SocketLayout_ThePinHoles_AreSunkIntoTheWellAndNeverTouchEachOther(
            [Values(40, 80, 200)] int plateSide)
        {
            var contacts = SocketLayout.ContactParts(plateSide, plateSide, Protrusion, 1);
            var left = Named(contacts, SocketLayout.PinHoleName + "0L");
            var right = Named(contacts, SocketLayout.PinHoleName + "0R");
            float floorZ = WallDeviceLayout.RecessFloorZMM(Protrusion);
            float dia = SocketLayout.PinHoleDiameterMM(plateSide, plateSide);

            Assert.AreEqual(floorZ, FrontZ(left), Tol,
                "Гнездо начинается ровно на дне колодца и уходит в глубину, а не наружу");
            Assert.GreaterOrEqual(BackZ(left), -WallDeviceLayout.FrontZMM(Protrusion) - Tol,
                "Гнездо не имеет права пробить корпус насквозь до стены");
            Assert.Greater(right.CentreMM.x - left.CentreMM.x, dia,
                "Между гнёздами обязана остаться перемычка");
            Assert.Less(right.CentreMM.x + dia * 0.5f,
                SocketLayout.WellDiameterMM(plateSide, plateSide) * 0.5f,
                "Гнездо обязано целиком лежать внутри колодца");
        }

        [Test]
        public void SocketLayout_TheEarthClips_StandInsideTheWellCircleWithEveryCorner(
            [Values(40, 80, 200)] int plateSide)
        {
            var top = Named(SocketLayout.ContactParts(plateSide, plateSide, Protrusion, 1),
                SocketLayout.EarthName + "0Top");
            float wellRadius = SocketLayout.WellDiameterMM(plateSide, plateSide) * 0.5f;

            float cornerX = top.SizeMM.x * 0.5f;
            float cornerY = Mathf.Abs(top.CentreMM.y) + top.SizeMM.y * 0.5f;
            float cornerRadius = Mathf.Sqrt(cornerX * cornerX + cornerY * cornerY);

            Assert.Less(cornerRadius, wellRadius,
                "Угол заземляющей лапки вылез за окружность колодца — лапка повиснет в воздухе "
                + "поверх рамки");
        }

        [Test]
        public void SocketLayout_TheEarthClips_RiseFromTheWellFloorWithoutReachingTheFrame()
        {
            var top = Named(SocketLayout.ContactParts(PlateW, PlateH, Protrusion, 1),
                SocketLayout.EarthName + "0Top");
            float floorZ = WallDeviceLayout.RecessFloorZMM(Protrusion);

            Assert.AreEqual(floorZ, BackZ(top), Tol,
                "Лапка растёт со дна колодца, а не висит над ним");
            Assert.Less(FrontZ(top), RimBackZ(Protrusion) - Tol,
                "Лапка вровень с рамкой перестала бы читаться как контакт внутри углубления");
        }

        [Test]
        public void SocketLayout_TheTwoEarthClips_AreMirroredAcrossTheCentre()
        {
            var contacts = SocketLayout.ContactParts(PlateW, PlateH, Protrusion, 1);
            var top = Named(contacts, SocketLayout.EarthName + "0Top");
            var bottom = Named(contacts, SocketLayout.EarthName + "0Bottom");

            Assert.AreEqual(top.CentreMM.y, -bottom.CentreMM.y, Tol,
                "Лапки стоят строго напротив друг друга: несимметричная пара читается как брак");
            Assert.AreEqual(top.SizeMM, bottom.SizeMM,
                "Обе лапки одного размера — вилка входит в любую сторону");
        }

        [Test]
        public void LightSwitchLayout_TheKey_ComesOutFlushWithTheFrame(
            [Values(3, 10, 60)] int protrusion)
        {
            var key = Named(LightSwitchLayout.KeyParts(PlateW, PlateH, protrusion, 1),
                LightSwitchLayout.KeyName + "0");

            Assert.AreEqual(WallDeviceLayout.FrontZMM(protrusion), FrontZ(key), Tol,
                "Клавиша выходит вровень с рамкой: утопленная читается как розетка");
            Assert.AreEqual(WallDeviceLayout.RecessFloorZMM(protrusion), BackZ(key), Tol,
                "Задняя грань клавиши стоит на дне углубления — под ней нет пустоты");
        }

        [Test]
        public void LightSwitchLayout_TheKey_LeavesAGapOnAllFourSides(
            [Values(40, 80, 200)] int plateSide)
        {
            float gap = LightSwitchLayout.KeyGapMM(plateSide, plateSide);

            Assert.Greater(gap, 0f,
                "Без зазора клавиша сливается с рамкой в сплошную пластину");
            Assert.AreEqual(WallDeviceLayout.InnerWidthMM(plateSide, plateSide) - 2f * gap,
                LightSwitchLayout.KeyWidthMM(plateSide, plateSide), Tol,
                "Зазор снимается с обеих сторон проёма, не с одной");
            Assert.AreEqual(WallDeviceLayout.InnerHeightMM(plateSide, plateSide) - 2f * gap,
                LightSwitchLayout.KeyHeightMM(plateSide, plateSide), Tol,
                "По высоте зазор такой же, иначе клавиша сядет криво");
        }

        [Test]
        public void LightSwitchLayout_OnePostOneKey([Values(1, 2, 3)] int posts)
        {
            var keys = LightSwitchLayout.KeyParts(PlateW, PlateH, Protrusion, posts);

            Assert.AreEqual(posts, keys.Count,
                "Двухклавишный выключатель — это два поста, каждый со своей клавишей");
            for (int i = 0; i < posts; i++)
                Assert.AreEqual(WallDeviceLayout.PostCentreXMM(i, PlateW, posts),
                    keys[i].CentreMM.x, Tol,
                    "Клавиша стоит по центру своего поста");
        }

        [Test]
        public void LightSwitchLayout_TheBackPlate_FillsTheOpeningBehindTheKey()
        {
            var back = Named(LightSwitchLayout.BodyParts(PlateW, PlateH, Protrusion, 1),
                LightSwitchLayout.BackName + "0");

            Assert.AreEqual(WallDeviceLayout.InnerWidthMM(PlateW, PlateH), back.SizeMM.x, Tol,
                "Корпус закрывает проём целиком: щель по краю клавиши светилась бы насквозь");
            Assert.AreEqual(WallDeviceLayout.InnerHeightMM(PlateW, PlateH), back.SizeMM.y, Tol,
                "То же по высоте проёма");
            Assert.AreEqual(-WallDeviceLayout.FrontZMM(Protrusion), BackZ(back), Tol,
                "Корпус выключателя тоже упирается в стену");
        }
    }
}
