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
    /// Связь третья — она же лекарство от «бесформенного батона», которым
    /// смеситель был в первой версии. Корпус НЕ цилиндр постоянного сечения:
    /// он делится межосевым расстоянием на три части. Между эксцентриками
    /// лежит тонкая перемычка, а наружу от каждого эксцентрика выходит своя
    /// толстая головка — слева рукоятка расхода, справа термоголовка со
    /// шкалой. Длина головки поэтому не своё число, а производная
    /// (длина − межосевое)/2, и при любом межосевом внутри подрезки она
    /// остаётся положительной: на максимуме межосевого она равна ровно
    /// радиусу корпуса.
    ///
    /// Связь четвёртая: дивертор — ОДИН узел. Кнопка-переключатель сверху и
    /// штуцер под шланг снизу сидят на одной вертикальной оси, потому что это
    /// два конца одного клапана. Разъедь они по длине корпуса — и модель
    /// станет показывать два независимых прибора вместо одного.</summary>
    public class BathMixerLayoutTests
    {
        private const float Tol = 1e-3f;

        [Test]
        public void BathMixerLayout_Escutcheons_HoldTheReferenceCentresOnTheWallPlane()
        {
            var spec = BathMixerSpec.Default;
            var left = BathMixerLayout.EscutcheonDisc(spec, -1f);
            var right = BathMixerLayout.EscutcheonDisc(spec, 1f);

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
        public void BathMixerLayout_Escutcheon_IsADiscWithAShoulder_NotAConeIntoTheBody()
        {
            var spec = BathMixerSpec.Default;
            var disc = BathMixerLayout.EscutcheonDisc(spec, -1f);
            var shoulder = BathMixerLayout.EscutcheonShoulder(spec, -1f);

            Assert.AreEqual(disc.FromRadiusMM, disc.ToRadiusMM, Tol,
                "отражатель начинается ЦИЛИНДРОМ: у конуса от самой стены нет силуэта, и "
                + "именно так он сливался с корпусом в первой версии");
            Assert.AreEqual(disc.ToMM.z, shoulder.FromMM.z, Tol,
                "плечо продолжает диск без разрыва");
            Assert.AreEqual(spec.EscutcheonReachMM, shoulder.ToMM.z, Tol,
                "и заканчивается ровно на заявленном вылете: вылет отражателя — это то, "
                + "что видно от стены, а не половина этого");
            Assert.Less(shoulder.ToRadiusMM, shoulder.FromRadiusMM,
                "плечо СУЖАЕТСЯ к корпусу — это уступ, по которому глаз отделяет "
                + "отражатель от трубы");
        }

        [Test]
        public void BathMixerLayout_Inlet_LeavesAVisibleGapBetweenTheEscutcheonAndTheBody()
        {
            var spec = BathMixerSpec.Default;
            var shoulder = BathMixerLayout.EscutcheonShoulder(spec, 1f);
            var inlet = BathMixerLayout.Inlet(spec, 1f);

            Assert.AreEqual(shoulder.ToMM.z, inlet.FromMM.z, Tol,
                "эксцентрик начинается там, где кончается отражатель");
            Assert.AreEqual(BathMixerLayout.BodyAxisZMM(spec), inlet.ToMM.z, Tol,
                "и кончается на оси корпуса, а не перед ним");
            Assert.AreEqual(shoulder.FromMM.x, inlet.FromMM.x, Tol,
                "эксцентрик соосен своему отражателю");
            Assert.Less(inlet.FromRadiusMM, shoulder.ToRadiusMM,
                "и он ТОНЬШЕ горловины отражателя: без этой ступеньки отражатель, "
                + "эксцентрик и корпус читаются одной сплошной колбасой");
            Assert.Greater(BathMixerLayout.BodyAxisZMM(spec) - BathMixerLayout.BarRadiusMM(spec),
                spec.EscutcheonReachMM,
                "между передним торцом отражателя и задней образующей перемычки остаётся "
                + "просвет: сквозь него видно стену, и это главный признак, что отражатель "
                + "отдельная деталь");
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
        public void BathMixerLayout_Bar_SpansExactlyTheCentresAndIsThinnerThanBothHeads()
        {
            var spec = BathMixerSpec.Default;
            var bar = BathMixerLayout.Bar(spec);
            var flow = BathMixerLayout.FlowHead(spec);
            var thermostat = BathMixerLayout.ThermostatHead(spec);

            Assert.AreEqual(BathMixerLayout.InletXMM(spec, -1f), bar.FromMM.x, Tol,
                "перемычка начинается на оси левого подключения");
            Assert.AreEqual(BathMixerLayout.InletXMM(spec, 1f), bar.ToMM.x, Tol,
                "и кончается на оси правого: ровно тот участок корпуса, внутри которого "
                + "идёт вода между двумя вводами");
            Assert.AreEqual(bar.FromMM.x, flow.ToMM.x, Tol,
                "ручка расхода начинается там, где кончается перемычка");
            Assert.AreEqual(bar.ToMM.x, thermostat.FromMM.x, Tol,
                "и термоголовка — тоже: головка это ровно тот кусок корпуса, что торчит "
                + "наружу от своего ввода");
            Assert.Less(bar.FromRadiusMM, flow.ToRadiusMM,
                "перемычка ТОНЬШЕ головки: без этой талии корпус читается одним батоном, "
                + "на котором нечего различать");
            Assert.Less(bar.FromRadiusMM, thermostat.FromRadiusMM,
                "и тоньше термоголовки тоже — талия симметрична");
        }

        [Test]
        public void BathMixerLayout_Heads_ReachTheBodyEndsAndSwellOutwards()
        {
            var spec = BathMixerSpec.Default;
            var flow = BathMixerLayout.FlowHead(spec);
            var thermostat = BathMixerLayout.ThermostatHead(spec);

            Assert.AreEqual(-spec.BodyLengthMM * 0.5f, flow.FromMM.x, Tol,
                "левый торец корпуса — это торец ручки расхода");
            Assert.AreEqual(spec.BodyLengthMM * 0.5f, thermostat.ToMM.x, Tol,
                "правый — торец термоголовки");
            Assert.Greater(flow.FromRadiusMM, flow.ToRadiusMM,
                "ручка расширяется НАРУЖУ: за неё берутся рукой, и её торец — самая "
                + "толстая точка корпуса");
            Assert.Greater(thermostat.ToRadiusMM, thermostat.FromRadiusMM,
                "термоголовка тоже: обе головки раздуты к торцам, талия между ними");
        }

        [Test]
        public void BathMixerLayout_HeadLengthMM_StaysPositiveRightUpToTheWidestCentres()
        {
            int length = BathMixerSpec.DefaultBodyLengthMM;
            int diameter = BathMixerSpec.DefaultBodyDiameterMM;
            var widest = BathMixerSpec.Clamped(BathMixerSpec.MaxCentresForMM(length, diameter),
                length, diameter, BathMixerSpec.DefaultEscutcheonReachMM,
                BathMixerSpec.DefaultSpoutLengthMM, BathMixerSpec.DefaultOutletDiameterMM);

            Assert.AreEqual(BathMixerLayout.BodyRadiusMM(widest),
                BathMixerLayout.HeadLengthMM(widest), Tol,
                "на предельном межосевом головка укорачивается ровно до радиуса корпуса — "
                + "полусфера, а не исчезнувшая деталь. Это прямое следствие того, что "
                + "потолок межосевого равен длине минус ДИАМЕТР");
            Assert.Greater(BathMixerLayout.HeadLengthMM(widest), 0f,
                "и она никогда не выворачивается наизнанку: отрицательная головка "
                + "нарисовалась бы конусом, растущим внутрь корпуса");
        }

        [Test]
        public void BathMixerLayout_ScaleCollar_RidesOnTheThermostatHeadAndStandsProud()
        {
            var spec = BathMixerSpec.Default;
            var collar = BathMixerLayout.ScaleCollar(spec);
            var head = BathMixerLayout.ThermostatHead(spec);

            Assert.Greater(collar.FromMM.x, head.FromMM.x,
                "кольцо шкалы сидит НА термоголовке, а не на перемычке");
            Assert.Less(collar.ToMM.x, head.ToMM.x,
                "и не свисает с её торца");
            Assert.Greater(collar.FromRadiusMM, BathMixerLayout.BodyRadiusMM(spec),
                "кольцо ВЫСТУПАЕТ над головкой: заподлицо оно невидимо, а это единственная "
                + "деталь, по которой термоголовка отличается от ручки расхода");
        }

        [Test]
        public void BathMixerLayout_LimitButton_SitsOnTopOfTheHeadAndNotOnItsEndFace()
        {
            var spec = BathMixerSpec.Default;
            var button = BathMixerLayout.LimitButton(spec);

            Assert.Greater(button.ToMM.y, button.FromMM.y,
                "кнопка ограничителя торчит ВВЕРХ");
            Assert.Greater(button.ToMM.y, BathMixerLayout.BodyRadiusMM(spec),
                "и выступает над головкой: утопленная заподлицо кнопка не видна ни с "
                + "одного ракурса");
            Assert.Less(button.FromMM.x + button.FromRadiusMM, spec.BodyLengthMM * 0.5f,
                "но она сидит СВЕРХУ, а не на торце: вылези она за торец — и заявленные "
                + "270 мм длины перестали бы совпадать с габаритом");
        }

        [Test]
        public void BathMixerOutlets_KnobAndNipple_ShareOneValveAxis()
        {
            var spec = BathMixerSpec.Default;
            var knob = BathMixerOutlets.DiverterKnob(spec);
            var nipple = BathMixerOutlets.HoseNipple(spec);

            Assert.AreEqual(knob.FromMM.x, nipple.FromMM.x, Tol,
                "кнопка дивертора и штуцер под шланг — два конца ОДНОГО клапана: нажал "
                + "сверху, вода пошла вниз в шланг. Разъедутся по корпусу — модель начнёт "
                + "показывать два независимых прибора");
            Assert.AreEqual(knob.FromMM.z, nipple.FromMM.z, Tol,
                "и по глубине тоже соосны");
            Assert.Greater(knob.FromMM.y, nipple.FromMM.y,
                "кнопка сверху, штуцер снизу");
            Assert.Greater(knob.FromMM.x, 0f,
                "и весь узел смещён к термоголовке, как на референсе, а не стоит по центру "
                + "под изливом");
        }

        [Test]
        public void BathMixerOutlets_HoseNipple_HangsBelowTheBodySilhouette()
        {
            var spec = BathMixerSpec.Default;
            var nipple = BathMixerOutlets.HoseNipple(spec);

            Assert.AreEqual(spec.OutletDiameterMM * 0.5f, nipple.FromRadiusMM, Tol,
                "штуцер G 1/2 — это 13 мм с референса, и это тот диаметр, на который "
                + "сядет шланг душевой стойки");
            Assert.Less(nipple.ToMM.y, nipple.FromMM.y,
                "штуцер смотрит вниз: шланг вешается снизу");
            Assert.Greater(nipple.FromMM.y, -BathMixerLayout.BarRadiusMM(spec),
                "верх штуцера утоплен в перемычку: начни его от нижней образующей, и на "
                + "стыке будет видна щель при любом наклоне камеры");
            Assert.Less(nipple.ToMM.y, -BathMixerLayout.BodyRadiusMM(spec),
                "а низ выходит ЗА нижнюю образующую головок: короткий штуцер прячется в "
                + "силуэте корпуса, и его на кадре просто нет");
        }

        [Test]
        public void BathMixerOutlets_Spout_LeavesTheBodyForwardAndDownAndBreaksAtTheMouth()
        {
            var spec = BathMixerSpec.Default;
            var run = BathMixerOutlets.SpoutRun(spec);
            var mouth = BathMixerOutlets.SpoutMouth(spec);

            Assert.AreEqual(BathMixerLayout.BodyAxisZMM(spec), run.FromMM.z, Tol,
                "излив растёт из ОСИ корпуса: посади его на поверхность — и на стыке "
                + "появится кольцевой шов");
            Assert.Greater(run.ToMM.z, run.FromMM.z, "излив уходит ВПЕРЁД от стены");
            Assert.Less(run.ToMM.y, run.FromMM.y,
                "и ВНИЗ: горизонтальный излив лил бы мимо ванны");
            Assert.Less(run.ToRadiusMM, run.FromRadiusMM,
                "излив сужается к носику — он конический, а не трубка постоянного сечения");
            Assert.AreEqual(run.ToMM, mouth.FromMM,
                "носик продолжает излив без разрыва");
            Assert.Greater(Slope(mouth), Slope(run),
                "и падает КРУЧЕ прямого участка: этот излом и есть то, по чему излив "
                + "читается изливом, а не палкой, воткнутой в корпус");
            Assert.Greater(mouth.ToRadiusMM, mouth.FromRadiusMM,
                "срез носика слегка развальцован наружу");
        }

        [Test]
        public void BathMixerOutlets_SpoutMouth_EndsAtTheDeclaredReachBelowTheBody()
        {
            var spec = BathMixerSpec.Default;
            var mouth = BathMixerOutlets.SpoutMouth(spec);

            Assert.AreEqual(BathMixerLayout.BodyAxisZMM(spec) + spec.SpoutLengthMM,
                mouth.ToMM.z, Tol,
                "вылет излива отсчитывается от ОСИ корпуса: это то расстояние, на которое "
                + "струя выносится за край ванны");
            Assert.Less(mouth.ToMM.y, -BathMixerLayout.BodyRadiusMM(spec),
                "и носик висит НИЖЕ корпуса: окажись он внутри силуэта — излива на кадре "
                + "не видно вовсе");
        }

        [Test]
        public void BathMixerOutlets_Spout_DoesNotCollideWithTheHoseNipple()
        {
            var spec = BathMixerSpec.Default;
            var run = BathMixerOutlets.SpoutRun(spec);
            var nipple = BathMixerOutlets.HoseNipple(spec);

            Assert.Greater(Mathf.Abs(nipple.FromMM.x - run.FromMM.x),
                run.FromRadiusMM + nipple.FromRadiusMM,
                "излив и штуцер висят снизу рядом, и их оси обязаны разойтись дальше суммы "
                + "радиусов: иначе они срастаются в одну каплю под корпусом");
        }

        [Test]
        public void BathMixerLayout_BoundsMM_StartsExactlyAtTheWallPlane()
        {
            Assert.AreEqual(0f, BathMixerLayout.BoundsMM(BathMixerSpec.Default).min.z, Tol,
                "задняя грань габарита — это плоскость стены. Сдвиг здесь превращается в "
                + "зазор или в утопленный в стену смеситель при посадке на грань");
        }

        [Test]
        public void BathMixerLayout_BoundsMM_HoldsEveryPartInside()
        {
            var spec = BathMixerSpec.Default;
            var bounds = BathMixerLayout.BoundsMM(spec);

            foreach (var part in BathMixerLayout.Parts(spec))
            {
                var extent = PipeBounds.DiscExtentMM(part.Direction,
                    Mathf.Max(part.FromRadiusMM, part.ToRadiusMM));
                var low = Vector3.Min(part.FromMM, part.ToMM) - extent;
                var high = Vector3.Max(part.FromMM, part.ToMM) + extent;

                Assert.IsTrue(low.x + Tol >= bounds.min.x && high.x <= bounds.max.x + Tol
                    && low.y + Tol >= bounds.min.y && high.y <= bounds.max.y + Tol
                    && low.z + Tol >= bounds.min.z && high.z <= bounds.max.z + Tol,
                    "любая деталь целиком внутри габарита: торчащая наружу деталь не "
                    + "попадает ни в рамку выделения, ни в проверки пересечений");
            }
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
                "высота больше диаметра корпуса: снизу висят излив и штуцер под шланг, "
                + "сверху торчит кнопка дивертора");
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
        public void BathMixerLayout_CentreAboveFloorMM_PutsTheBodyAxisAtTapHeight()
        {
            var spec = BathMixerSpec.Default;
            float centre = BathMixerLayout.CentreAboveFloorMM(spec);
            float bodyAxis = centre - BathMixerLayout.BoundsMM(spec).center.y;

            Assert.AreEqual(BathMixerLayout.BodyAxisAboveFloorMM, bodyAxis, Tol,
                "смеситель вешается по ОСИ КОРПУСА, а не по центру габарита: центр смещён "
                + "изливом и штуцером вниз, и подставив его напрямую, получили бы кран, "
                + "висящий на пару сантиметров ниже задуманного");
            Assert.AreEqual(700f, BathMixerLayout.BodyAxisAboveFloorMM, Tol,
                "и это 700 мм над полом — та высота, на которой настенный смеситель ставят "
                + "над бортом ванны");
            Assert.Greater(centre, 0f,
                "и это высота НАД полом: настенный смеситель, рождённый на полу, "
                + "пользователь двигает руками каждый раз");
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

        private static float Slope(PipeSegment segment)
        {
            var axis = segment.AxisMM;
            float forward = new Vector2(axis.x, axis.z).magnitude;
            return forward > Tolerance.EpsilonUnits ? -axis.y / forward : float.MaxValue;
        }
    }
}
