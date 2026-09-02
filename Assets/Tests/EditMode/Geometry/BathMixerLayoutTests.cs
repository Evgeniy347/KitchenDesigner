using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

namespace KitchenDesigner.Tests.Geometry
{
    /// <summary>Настенный термостатический смеситель для ванны, размеры сняты
    /// с референса 6702902950: межосевое подключений 150 мм, корпус 270 мм по
    /// крайним точкам при диаметре 70 мм, вылет отражателей от стены 34 мм,
    /// штуцер под шланг G 1/2 диаметром 13 мм.
    ///
    /// Все эти числа — умолчания, а не константы: пользователь правит их в
    /// панели, и потому важна не сама цифра, а СВЯЗИ между ними, которые
    /// нельзя нарушить никаким набором значений.
    ///
    /// Связь первая: плоскость стены — это z=0, и ничто не имеет права уйти
    /// за неё. Смеситель проёма не режет (в отличие от окна и двери), он
    /// висит на грани, и его задняя грань габарита обязана лежать в этой
    /// плоскости — иначе WallMountedPose.SeatedPosition посадит его с
    /// зазором или утопит в стену ровно на ошибку габарита.
    ///
    /// Связь вторая: корпус ЛЕЖИТ на отражателях. Ось корпуса отстоит от
    /// стены на вылет отражателя плюс собственный радиус, так что задняя
    /// образующая корпуса касается переднего торца отражателя. Задай ось по
    /// самому вылету 34 мм — и корпус радиусом 35 мм войдёт в стену на
    /// миллиметр.
    ///
    /// Связь третья: отражатели не вылезают за торцы корпуса. Межосевое
    /// расстояние ограничено длиной корпуса за вычетом его диаметра, иначе
    /// эксцентрики торчали бы из ручки расхода и термоголовки.</summary>
    public class BathMixerLayoutTests
    {
        private const float Tol = 1e-3f;

        private static PipeSegment Escutcheon(PipeSegment[] parts, int index) => parts[index];

        [Test]
        public void BathMixerLayout_Parts_HoldTheReferenceCentresOnTheWallPlane()
        {
            var spec = BathMixerSpec.Default;
            var parts = BathMixerLayout.Parts(spec);
            var left = Escutcheon(parts, 0);
            var right = Escutcheon(parts, 1);

            Assert.AreEqual(150f, right.FromMM.x - left.FromMM.x, Tol,
                "межосевое расстояние подключений — 150 мм по референсу: это стандарт "
                + "настенного подвода, и по нему смеситель садится на готовые эксцентрики");
            Assert.AreEqual(0f, left.FromMM.z, Tol,
                "торец отражателя лежит В плоскости стены, а не перед ней");
            Assert.AreEqual(0f, right.FromMM.z, Tol,
                "и второй отражатель тоже: перекос между ними означал бы, что смеситель "
                + "висит на одном подключении");
        }

        [Test]
        public void BathMixerLayout_BodyAxisZMM_LaysTheBodyOnTheEscutcheonFace()
        {
            var spec = BathMixerSpec.Default;

            Assert.AreEqual(spec.EscutcheonReachMM,
                BathMixerLayout.BodyAxisZMM(spec) - BathMixerLayout.BodyRadiusMM(spec), Tol,
                "задняя образующая корпуса касается переднего торца отражателя. Поставь ось "
                + "корпуса прямо на вылет 34 мм — и корпус радиусом 35 мм войдёт в стену");
        }

        [Test]
        public void BathMixerLayout_Parts_BridgeTheInletsFromTheEscutcheonToTheBody()
        {
            var spec = BathMixerSpec.Default;
            var parts = BathMixerLayout.Parts(spec);

            Assert.AreEqual(spec.EscutcheonReachMM, parts[2].FromMM.z, Tol,
                "эксцентрик начинается там, где кончается отражатель: щель между ними видно "
                + "насквозь");
            Assert.AreEqual(BathMixerLayout.BodyAxisZMM(spec), parts[2].ToMM.z, Tol,
                "и кончается на оси корпуса, а не перед ним");
            Assert.AreEqual(parts[2].FromMM.x, parts[0].FromMM.x, Tol,
                "эксцентрик соосен своему отражателю");
        }

        [Test]
        public void BathMixerLayout_BoundsMM_StartsExactlyAtTheWallPlane()
        {
            Assert.AreEqual(0f, BathMixerLayout.BoundsMM(BathMixerSpec.Default).min.z, Tol,
                "задняя грань габарита — это плоскость стены. Сдвиг здесь превращается в "
                + "зазор или в утопленный в стену смеситель при посадке на грань");
        }

        [Test]
        public void BathMixerLayout_DimensionsMM_AreAsLongAsTheBodyAndDeeperThanTheSpout()
        {
            var spec = BathMixerSpec.Default;
            var dims = BathMixerLayout.DimensionsMM(spec);

            Assert.AreEqual(270, dims.x,
                "длина по крайним точкам с референса: ручка расхода и термоголовка — это "
                + "торцы корпуса, и красная кнопка-ограничитель сидит СВЕРХУ головки, а не "
                + "на её торце, иначе габарит уехал бы за 270 мм");
            Assert.Greater(dims.z, spec.EscutcheonReachMM + spec.SpoutLengthMM,
                "глубину задаёт излив, а не корпус: он уходит вперёд от оси корпуса, "
                + "которая сама стоит впереди стены");
            Assert.Greater(dims.y, spec.BodyDiameterMM,
                "высота больше диаметра корпуса: снизу висят излив и штуцер под шланг");
        }

        [Test]
        public void BathMixerLayout_Parts_AimTheSpoutForwardAndDown()
        {
            var spec = BathMixerSpec.Default;
            var spout = BathMixerLayout.Parts(spec)[8];

            Assert.Greater(spout.ToMM.z, spout.FromMM.z,
                "излив уходит ВПЕРЁД от стены");
            Assert.Less(spout.ToMM.y, spout.FromMM.y,
                "и ВНИЗ: горизонтальный излив лил бы мимо ванны");
            Assert.Less(spout.ToRadiusMM, spout.FromRadiusMM,
                "излив сужается к носику — он конический, а не трубка постоянного сечения");
        }

        [Test]
        public void BathMixerLayout_Parts_HangTheHoseOutletUnderTheBody()
        {
            var spec = BathMixerSpec.Default;
            var outlet = BathMixerLayout.Parts(spec)[9];

            Assert.AreEqual(spec.OutletDiameterMM * 0.5f, outlet.FromRadiusMM, Tol,
                "штуцер G 1/2 — это 13 мм с референса, и это тот диаметр, на который "
                + "сядет шланг душевой стойки");
            Assert.Less(outlet.ToMM.y, outlet.FromMM.y,
                "штуцер смотрит вниз: шланг вешается снизу");
            Assert.Greater(outlet.FromMM.y, -BathMixerLayout.BodyRadiusMM(spec),
                "верх штуцера утоплен в корпус: начни его от нижней образующей, и на стыке "
                + "будет видна щель при любом наклоне камеры");
            Assert.Greater(outlet.FromMM.x, 0f,
                "штуцер смещён к термоголовке, как на референсе, а не стоит по центру под "
                + "изливом");
        }

        [Test]
        public void BathMixerSpec_Clamped_PullsTheCentresInsideTheBodyEnds()
        {
            var spec = BathMixerSpec.Clamped(500, 270, 70, 34, 110, 13);

            Assert.AreEqual(200, spec.CentresMM,
                "межосевое 500 мм на корпусе 270 мм невозможно: отражатели ушли бы за "
                + "торцы. Потолок — длина минус диаметр корпуса");
            Assert.LessOrEqual(spec.CentresMM * 0.5f + BathMixerLayout.EscutcheonRadiusMM(spec),
                spec.BodyLengthMM * 0.5f,
                "и после подрезки отражатель целиком помещается в проекцию корпуса");
        }

        [Test]
        public void BathMixerSpec_Clamped_GrowsTheBodyForAFatOne()
        {
            var spec = BathMixerSpec.Clamped(150, 150, 160, 34, 110, 13);

            Assert.AreEqual(220, spec.BodyLengthMM,
                "корпус диаметром 160 мм не бывает длиной 150 мм: минимальная длина — "
                + "диаметр плюс минимальное межосевое, иначе подрезка межосевого упёрлась бы "
                + "в отрицательный потолок и Mathf.Clamp вернул бы min больше max");
        }

        [Test]
        public void BathMixerSpec_Default_MatchesTheReferencePhoto()
        {
            var spec = BathMixerSpec.Default;

            Assert.AreEqual(150, spec.CentresMM, "межосевое подключений с референса");
            Assert.AreEqual(270, spec.BodyLengthMM, "длина корпуса с референса");
            Assert.AreEqual(70, spec.BodyDiameterMM, "высота-диаметр корпуса с референса");
            Assert.AreEqual(34, spec.EscutcheonReachMM, "вылет отражателя от стены с референса");
            Assert.AreEqual(13, spec.OutletDiameterMM,
                "штуцер G 1/2 — 13 мм, и это единственное число, которое обязано совпасть с "
                + "чужой деталью: на него садится шланг");
        }
    }
}
