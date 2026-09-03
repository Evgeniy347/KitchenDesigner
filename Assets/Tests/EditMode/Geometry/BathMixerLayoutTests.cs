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
    /// панели, и потому важна не сама цифра, а СВЯЗИ между ними.
    ///
    /// Связь первая: плоскость стены — это z=0, и ничто не имеет права уйти
    /// за неё. Смеситель проёма не режет, он висит на грани, и задняя грань
    /// его габарита обязана лежать в этой плоскости — иначе
    /// WallMountedPose.SeatedPosition посадит его с зазором или утопит в
    /// стену ровно на ошибку габарита.
    ///
    /// Связь вторая: корпус ЛЕЖИТ на отражателях. Ось корпуса отстоит от
    /// стены на вылет отражателя плюс радиус самой толстой его части, так что
    /// торцевые головки не входят в стену. Считай ось по тонкой части — и
    /// головка радиусом 35 мм утопится в кладку.
    ///
    /// Связь третья — про силуэт, и она стоила двух переделок. Сначала корпус
    /// был ОДНИМ цилиндром постоянного диаметра: различить на нём было
    /// нечего, модель читалась батоном. Тогда его перетянули в талию — и
    /// силуэт ПЕРЕВЕРНУЛСЯ: получилась гантель, два кома по краям и ниточка
    /// между ними, тогда как на референсе корпус идёт почти постоянным
    /// диаметром. Правильный ответ оказался третьим: диаметр почти
    /// постоянный, а детали отделяются УЗКИМИ КАНАВКАМИ и ступенями, как на
    /// настоящей хромированной арматуре. Отсюда MinBodySlendernessRatio — он
    /// сторожит, чтобы тонкая часть не уехала обратно в ниточку.
    ///
    /// Связь четвёртая: отражатели стоят У СТЕНЫ отдельными плоскими дисками,
    /// а не в одну линию с корпусом. Между их передним торцом и задней
    /// образующей корпуса обязан оставаться просвет, в котором виден только
    /// тонкий эксцентрик: именно этот зазор и отделяет их глазом.
    ///
    /// Связь пятая: дивертор — ОДИН узел. Кнопка-переключатель сверху и
    /// штуцер под шланг снизу сидят на одной вертикальной оси, потому что это
    /// два конца одного клапана.
    ///
    /// И связь шестая, про кадр: лицо смесителя смотрит в +Z. Камера
    /// изометрии стоит со стороны -Z, поэтому в своих кадрах смеситель
    /// обязан развернуться — иначе снимок показывает стену и затылки
    /// отражателей, а излива на нём нет вовсе. Так первый круг и был
    /// потерян.</summary>
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
        public void BathMixerLayout_Escutcheon_IsAFlatDiscWithAChamfer_NotAConeFromTheWall()
        {
            var spec = BathMixerSpec.Default;
            var disc = BathMixerLayout.EscutcheonDisc(spec, -1f);
            var chamfer = BathMixerLayout.EscutcheonChamfer(spec, -1f);

            Assert.AreEqual(disc.FromRadiusMM, disc.ToRadiusMM, Tol,
                "отражатель — ПЛОСКИЙ ДИСК постоянного радиуса: у конуса от самой стены "
                + "нет силуэта, и именно так он сливался с корпусом в первой версии");
            Assert.AreEqual(disc.ToMM.z, chamfer.FromMM.z, Tol,
                "фаска продолжает диск без разрыва");
            Assert.AreEqual(spec.EscutcheonReachMM, chamfer.ToMM.z, Tol,
                "и заканчивается ровно на заявленном вылете: вылет отражателя — это то, "
                + "что видно от стены, а не половина этого");
            Assert.Less(chamfer.ToRadiusMM, chamfer.FromRadiusMM,
                "фаска СУЖАЕТСЯ к корпусу — это уступ, по которому глаз отделяет "
                + "отражатель от трубы");
            Assert.LessOrEqual(BathMixerLayout.EscutcheonRadiusMM(spec),
                BathMixerLayout.BodyRadiusMM(spec),
                "и диск НЕ ШИРЕ корпуса: камера изометрии стоит со стороны стены, и "
                + "отражатель шире корпуса заслоняет собой всё остальное");
        }

        [Test]
        public void BathMixerLayout_Inlet_LeavesAVisibleGapBetweenTheEscutcheonAndTheBody()
        {
            var spec = BathMixerSpec.Default;
            var chamfer = BathMixerLayout.EscutcheonChamfer(spec, 1f);
            var inlet = BathMixerLayout.Inlet(spec, 1f);

            Assert.AreEqual(chamfer.ToMM.z, inlet.FromMM.z, Tol,
                "эксцентрик начинается там, где кончается отражатель");
            Assert.AreEqual(BathMixerLayout.BodyAxisZMM(spec), inlet.ToMM.z, Tol,
                "и кончается на оси корпуса, а не перед ним");
            Assert.AreEqual(chamfer.FromMM.x, inlet.FromMM.x, Tol,
                "эксцентрик соосен своему отражателю");
            Assert.Less(inlet.FromRadiusMM, chamfer.ToRadiusMM,
                "и он ТОНЬШЕ горловины отражателя: без этой ступеньки отражатель, "
                + "эксцентрик и корпус читаются одной сплошной колбасой");
            Assert.Greater(
                BathMixerLayout.BodyAxisZMM(spec) - BathMixerLayout.BodyTubeRadiusMM(spec),
                spec.EscutcheonReachMM,
                "между передним торцом отражателя и задней образующей корпуса остаётся "
                + "ПРОСВЕТ: сквозь него видно стену, и это главный признак, что "
                + "отражатель отдельная деталь, а не начало корпуса");
        }

        [Test]
        public void BathMixerLayout_BodyAxisZMM_KeepsTheFattestPartOutOfTheWall()
        {
            var spec = BathMixerSpec.Default;

            Assert.AreEqual(spec.EscutcheonReachMM,
                BathMixerLayout.BodyAxisZMM(spec) - BathMixerLayout.BodyRadiusMM(spec), Tol,
                "ось корпуса отсчитана от САМОЙ ТОЛСТОЙ его части — торцевых головок. "
                + "Посчитай её по тонкой средней трубе, и головка радиусом 35 мм войдёт в "
                + "стену на пять миллиметров");
        }

        [Test]
        public void BathMixerLayout_BodyTube_IsNearlyAsFatAsTheHeads_NotADumbbellWaist()
        {
            var spec = BathMixerSpec.Default;

            Assert.GreaterOrEqual(
                BathMixerLayout.BodyTubeRadiusMM(spec) / BathMixerLayout.BodyRadiusMM(spec),
                BathMixerLayout.MinBodySlendernessRatio,
                "на референсе корпус идёт ПОЧТИ ПОСТОЯННЫМ диаметром, и детали на нём "
                + "разделены канавками, а не перетяжкой. Утончи середину сильнее — и "
                + "силуэт перевернётся в гантель: два кома по краям и ниточка между "
                + "ними. Это уже случилось однажды и стоило целого круга правок");
            Assert.Less(BathMixerLayout.BodyTubeRadiusMM(spec),
                BathMixerLayout.BodyRadiusMM(spec),
                "но и не заподлицо: без ступени у головок нечего различать, и корпус "
                + "снова станет одним батоном");
        }

        [Test]
        public void BathMixerLayout_Groove_IsNarrowerThanBothTheBodyAndTheHeadItSeparates()
        {
            var spec = BathMixerSpec.Default;
            var groove = BathMixerLayout.Groove(spec, 1f);
            var head = BathMixerControls.Shoulder(spec, 1f);

            Assert.AreEqual(BathMixerLayout.InletXMM(spec, 1f), groove.FromMM.x, Tol,
                "канавка начинается на оси подключения — там, где кончается корпус");
            Assert.AreEqual(groove.ToMM.x, head.FromMM.x, Tol,
                "и упирается в головку без разрыва");
            Assert.Less(groove.FromRadiusMM, BathMixerLayout.BodyTubeRadiusMM(spec),
                "канавка УЖЕ корпуса");
            Assert.Less(groove.FromRadiusMM, head.FromRadiusMM,
                "и уже головки: две ступени подряд — это и есть то, чем настоящая "
                + "хромированная арматура отделяет деталь от детали");
            Assert.Less(groove.LengthMM, BathMixerLayout.HeadLengthMM(spec),
                "и она КОРОТКАЯ: растянутая канавка перестаёт быть канавкой и снова "
                + "становится талией");
        }

        [Test]
        public void BathMixerControls_Handles_ReachTheBodyEndsAndChamferAtTheVeryTip()
        {
            var spec = BathMixerSpec.Default;
            var flowCap = BathMixerControls.Cap(spec, -1f);
            var thermostatCap = BathMixerControls.Cap(spec, 1f);
            var shoulder = BathMixerControls.Shoulder(spec, -1f);

            Assert.AreEqual(-spec.BodyLengthMM * 0.5f, flowCap.ToMM.x, Tol,
                "левый торец корпуса — это торец рукоятки расхода");
            Assert.AreEqual(spec.BodyLengthMM * 0.5f, thermostatCap.ToMM.x, Tol,
                "правый — торец термоголовки");
            Assert.AreEqual(shoulder.ToMM, flowCap.FromMM,
                "плечо и торцевая фаска идут встык");
            Assert.Greater(shoulder.ToRadiusMM, shoulder.FromRadiusMM,
                "головка расширяется НАРУЖУ от канавки: за рукоятку берутся рукой");
            Assert.Less(flowCap.ToRadiusMM, flowCap.FromRadiusMM,
                "а на самом торце снята фаска: обрубленный плоский торец выглядит "
                + "распилом, а не деталью");
        }

        [Test]
        public void BathMixerControls_Lever_StandsOffTheBodyAxisWhereACylinderCannotHide()
        {
            var spec = BathMixerSpec.Default;
            var lever = BathMixerControls.Lever(spec);
            float z = BathMixerLayout.BodyAxisZMM(spec);

            Assert.Less(lever.FromMM.x, 0f,
                "рычаг расхода сидит на ЛЕВОЙ головке — по референсу расход слева, "
                + "термостат справа");
            Assert.Less(Radial(lever.FromMM, z), BathMixerLayout.BodyRadiusMM(spec),
                "корень рычага утоплен в головку: снаружи он дал бы кольцевой шов");
            Assert.Greater(Radial(lever.ToMM, z), BathMixerLayout.BodyRadiusMM(spec),
                "а его конец ВЫХОДИТ за поверхность головки. Это единственная деталь "
                + "смесителя, унесённая с главной оси вбок, и потому единственная, "
                + "которую нельзя спутать с очередным кольцом на трубе");
            Assert.Less(lever.ToMM.y, lever.FromMM.y,
                "и смотрит он вперёд-вниз, как на референсе, а не в потолок");
        }

        [Test]
        public void BathMixerControls_ScaleCollar_RidesOnTheThermostatHeadAndStandsProud()
        {
            var spec = BathMixerSpec.Default;
            var collar = BathMixerControls.ScaleCollar(spec);
            var head = BathMixerControls.Shoulder(spec, 1f);

            Assert.Greater(collar.FromMM.x, head.FromMM.x,
                "кольцо шкалы сидит НА термоголовке, а не на канавке");
            Assert.Less(collar.ToMM.x, spec.BodyLengthMM * 0.5f,
                "и не свисает с её торца");
            Assert.Greater(collar.FromRadiusMM, BathMixerLayout.BodyRadiusMM(spec),
                "кольцо ВЫСТУПАЕТ над головкой: заподлицо оно невидимо, а это "
                + "единственная деталь, по которой термоголовка отличается от рукоятки "
                + "расхода");
        }

        [Test]
        public void BathMixerControls_LimitButton_SitsOnTopOfTheHeadAndNotOnItsEndFace()
        {
            var spec = BathMixerSpec.Default;
            var button = BathMixerControls.LimitButton(spec);

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
            Assert.Greater(BathMixerControls.CapLengthMM(widest), 0f,
                "и торцевая фаска на ней ещё существует: обнулись она, и головка "
                + "кончилась бы плоским распилом");
        }

        [Test]
        public void BathMixerOutlets_Spout_IsAThickBranchAndNotAStrawInTheBody()
        {
            var spec = BathMixerSpec.Default;
            var shoulder = BathMixerOutlets.SpoutShoulder(spec);

            Assert.Greater(shoulder.FromRadiusMM,
                BathMixerLayout.BodyTubeRadiusMM(spec) * BathMixerOutlets.MinBranchRatio,
                "излив — САМАЯ УЗНАВАЕМАЯ деталь смесителя, и он обязан быть толстым "
                + "ответвлением, а не трубочкой: тонкий излив тонет в силуэте корпуса и "
                + "на кадре его просто нет");
            Assert.Less(Radial(shoulder.FromMM, BathMixerLayout.BodyAxisZMM(spec)),
                BathMixerLayout.BodyTubeRadiusMM(spec),
                "корень излива утоплен в корпус: посади его на поверхность — и на стыке "
                + "появится кольцевой шов");
        }

        [Test]
        public void BathMixerOutlets_Spout_LeavesTheBodyForwardAndDownAndBreaksAtTheMouth()
        {
            var spec = BathMixerSpec.Default;
            var shoulder = BathMixerOutlets.SpoutShoulder(spec);
            var run = BathMixerOutlets.SpoutRun(spec);
            var mouth = BathMixerOutlets.SpoutMouth(spec);

            Assert.AreEqual(shoulder.ToMM, run.FromMM, "плечо и прямой участок встык");
            Assert.AreEqual(run.ToMM, mouth.FromMM, "прямой участок и носик — тоже");
            Assert.Greater(run.ToMM.z, run.FromMM.z, "излив уходит ВПЕРЁД от стены");
            Assert.Less(run.ToMM.y, run.FromMM.y,
                "и ВНИЗ: горизонтальный излив лил бы мимо ванны");
            Assert.Less(run.ToRadiusMM, run.FromRadiusMM,
                "излив сужается к носику — он конический, а не трубка постоянного сечения");
            Assert.Greater(Slope(mouth), Slope(run),
                "а носик падает КРУЧЕ прямого участка: этот излом и есть то, по чему "
                + "излив читается изливом, а не палкой, воткнутой в корпус");
            Assert.Greater(Slope(run), Slope(shoulder),
                "и весь излив ЛОМАЕТСЯ трижды, всё круче вниз: одна прямая от корпуса до "
                + "носика читается спицей");
            Assert.Greater(mouth.ToRadiusMM, mouth.FromRadiusMM,
                "срез носика слегка развальцован наружу");
        }

        [Test]
        public void BathMixerOutlets_SpoutMouth_EndsAtTheDeclaredReachWellBelowTheBody()
        {
            var spec = BathMixerSpec.Default;
            var mouth = BathMixerOutlets.SpoutMouth(spec);

            Assert.AreEqual(BathMixerLayout.BodyAxisZMM(spec) + spec.SpoutLengthMM,
                mouth.ToMM.z, Tol,
                "вылет излива отсчитывается от ОСИ корпуса: это то расстояние, на которое "
                + "струя выносится за край ванны");
            Assert.Less(mouth.ToMM.y,
                -BathMixerLayout.BodyRadiusMM(spec) - BathMixerLayout.BodyTubeRadiusMM(spec),
                "и носик висит НИЖЕ корпуса с запасом в целую трубу: подберись он ближе — "
                + "и на изометрии сверху излив снова спрячется в силуэте");
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
            Assert.Greater(knob.FromMM.y, nipple.FromMM.y, "кнопка сверху, штуцер снизу");
            Assert.Greater(knob.FromMM.x, 0f,
                "и весь узел смещён к термоголовке, как на референсе, а не стоит по центру "
                + "под изливом");
            Assert.Less(knob.FromMM.x + knob.FromRadiusMM,
                BathMixerLayout.InletXMM(spec, 1f),
                "но не заезжает на канавку: клапан сидит в КОРПУСЕ, между подключениями");
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
            Assert.Greater(nipple.FromMM.y, -BathMixerLayout.BodyTubeRadiusMM(spec),
                "верх штуцера утоплен в корпус: начни его от нижней образующей, и на "
                + "стыке будет видна щель при любом наклоне камеры");
            Assert.Less(nipple.ToMM.y, -BathMixerLayout.BodyRadiusMM(spec),
                "а низ выходит ЗА нижнюю образующую головок: короткий штуцер прячется в "
                + "силуэте корпуса, и его на кадре просто нет");
        }

        [Test]
        public void BathMixerOutlets_Spout_DoesNotCollideWithTheHoseNipple()
        {
            var spec = BathMixerSpec.Default;
            var shoulder = BathMixerOutlets.SpoutShoulder(spec);
            var nipple = BathMixerOutlets.HoseNipple(spec);

            Assert.Greater(Mathf.Abs(nipple.FromMM.x - shoulder.FromMM.x),
                shoulder.FromRadiusMM + nipple.FromRadiusMM,
                "излив и штуцер висят снизу рядом, и их оси обязаны разойтись дальше суммы "
                + "радиусов: иначе они срастаются в одну каплю под корпусом");
        }

        [Test]
        public void BathMixerLayout_FaceLooksAwayFromTheWall_SoTheIsoFrameMustTurnItAround()
        {
            var spec = BathMixerSpec.Default;

            Assert.AreEqual(0f, BathMixerLayout.BoundsMM(spec).min.z, Tol,
                "задняя грань габарита — это плоскость стены. Сдвиг здесь превращается в "
                + "зазор или в утопленный в стену смеситель при посадке на грань");
            Assert.Greater(BathMixerOutlets.SpoutMouthMM(spec).z,
                BathMixerLayout.BodyAxisZMM(spec),
                "а лицо — в +Z: излив, рычаг расхода и носик уходят ОТ стены. Камера "
                + "изометрии стоит со стороны -Z, поэтому кадр смесителя обязан его "
                + "развернуть; неразвёрнутый снимок показывает затылки отражателей, и "
                + "излива на нём нет вовсе");
            Assert.Greater(BathMixerControls.Lever(spec).ToMM.z,
                BathMixerLayout.BodyAxisZMM(spec),
                "рычаг смотрит туда же, куда излив: обе опознавательные детали на одной "
                + "стороне, и обе теряются при съёмке со стены");
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
                "длина по крайним точкам с референса: рукоятка расхода и термоголовка — "
                + "это торцы корпуса, и красная кнопка-ограничитель сидит СВЕРХУ головки, "
                + "а не на её торце, иначе габарит уехал бы за 270 мм");
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

        private static float Radial(Vector3 pointMM, float axisZMM) =>
            new Vector2(pointMM.y, pointMM.z - axisZMM).magnitude;

        private static float Slope(PipeSegment segment)
        {
            var axis = segment.AxisMM;
            float forward = new Vector2(axis.x, axis.z).magnitude;
            return forward > Tolerance.EpsilonUnits ? -axis.y / forward : float.MaxValue;
        }
    }
}
