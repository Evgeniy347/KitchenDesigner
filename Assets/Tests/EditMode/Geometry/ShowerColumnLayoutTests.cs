using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

namespace KitchenDesigner.Tests.Geometry
{
    /// <summary>Душевая стойка с тропическим душем: вертикальная штанга на
    /// двух настенных кронштейнах, изогнутый гусак с круглой лейкой Ø 250 мм
    /// сверху, подвижный держатель с ручной лейкой Ø 110 мм на штанге, блок
    /// переключателя внизу и гибкий шланг петлёй между ними. Своего смесителя
    /// у стойки НЕТ — нижний блок это дивертор, без вентилей.
    ///
    /// Штанга и гусак — ОДНА ломаная, а не три детали: прямой участок,
    /// четверть окружности и горизонтальный вынос идут в протяжку одним
    /// массивом точек, потому что стык двух отдельных труб виден кольцевым
    /// швом ровно там, где на настоящей стойке гладкий гиб.
    ///
    /// И вот на чём это сломалось, и почему тестов на ломаную теперь три.
    /// Дуга гиба строится от центра по двум осям — «откуда начать» и «куда
    /// мести». Начальную ось задали вниз вместо назад, и дуга поехала: её
    /// первая точка оказалась не на верхнем конце прямого участка, а на
    /// радиус гиба НИЖЕ и на радиус ВПЕРЁД. Ломаная от этого не порвалась —
    /// протяжка честно соединила прямой участок с уехавшей точкой наклонным
    /// куском, и вся стойка встала в габаритной коробке по диагонали. Никакой
    /// тест этого не поймал: концы ломаной остались на своих местах, длина
    /// звеньев осталась ненулевой, габарит сошёлся. Ловится это только
    /// проверкой ВНУТРЕННОСТИ ломаной: ниже центра гиба штанга строго
    /// вертикальна, длинных звеньев ровно два (прямой участок и вынос), и обе
    /// касательные гиба лежат на радиусе от его центра. Проверено обратной
    /// подстановкой сломанных осей — краснеют все три; проверка «шаг не
    /// длиннее самого длинного законного» при этом оставалась зелёной, потому
    /// что прямой участок штанги длиннее любой кривой перемычки.
    ///
    /// Отсюда же ограничение на вынос: он обязан быть больше, чем вылет
    /// штанги от стены плюс радиус гиба, иначе четверть окружности
    /// заканчивается ДАЛЬШЕ точки, куда её ведут, и гусак загибается назад к
    /// стене.
    ///
    /// Второй урок, оплаченный целым кругом правок: почти все детали стойки —
    /// соосные цилиндры близких диаметров, и потому они СЛИПАЮТСЯ в колбасу.
    /// Кронштейн, надетый на штангу, читается вздутием самой трубы, а не
    /// хомутом. Лечится это тремя вещами разом, и все три здесь под тестом:
    /// заметная ступень диаметра, ЗАЗОР между деталями (плечо кронштейна
    /// тоньше и розетки, и хомута), и детали, унесённые с главной оси вбок
    /// (рычаг дивертора, вынос держателя).
    ///
    /// Ручная лейка висит в держателе НАКЛОННО — и её наклон задаёт не сама
    /// лейка, а держатель: центр чашки лежит на оси рукоятки, и вся лейка
    /// строится от этой точки вверх и вниз по одному направлению. Поэтому
    /// шланг приходит не «куда-то вниз», а точно в нижний торец рукоятки и
    /// вдоль её оси.
    ///
    /// Лицо стойки смотрит в +Z: туда уходят гусак, вынос держателя и рычаг
    /// дивертора. Камера изометрии стоит со стороны -Z, поэтому кадр стойки
    /// обязан её развернуть — иначе лейка снимается с изнанки и выглядит
    /// ложкой.
    ///
    /// Габарит стойки НЕ равен её высоте: петля шланга свисает ниже нижнего
    /// штуцера, и коробка обязана её накрыть — иначе шланг торчит из
    /// выделения и не попадает в проверки пересечений.</summary>
    public class ShowerColumnLayoutTests
    {
        private const float Tol = 1e-3f;

        private const float LongStepFactor = 1.5f;

        [Test]
        public void ShowerColumnLayout_RiserPath_RunsFromTheDiverterToTheArmEnd()
        {
            var spec = ShowerColumnSpec.Default;
            var path = ShowerColumnLayout.RiserPath(spec);

            Assert.AreEqual(spec.WallOffsetMM, path[0].z, Tol,
                "штанга начинается на своей оси, вынесенной от стены кронштейнами");
            Assert.AreEqual(0f, path[0].y, Tol,
                "нижний конец штанги сидит в диверторе, у самого низа стойки");
            Assert.AreEqual(spec.ArmReachMM, path[path.Length - 1].z, Tol,
                "гусак доводит трубу до точки подвеса тропической лейки");
            Assert.AreEqual(ShowerColumnLayout.ArmAxisYMM(spec), path[path.Length - 1].y, Tol,
                "и делает это на высоте оси горизонтального выноса");
        }

        [Test]
        public void ShowerColumnLayout_RiserPath_StandsVerticalBelowTheBend()
        {
            var spec = ShowerColumnSpec.Default;

            foreach (var point in ShowerColumnLayout.RiserPath(spec))
            {
                if (point.y > ShowerColumnLayout.BendCentreMM(spec).y) continue;
                Assert.AreEqual(spec.WallOffsetMM, point.z, Tol,
                    "ниже центра гиба штанга строго ВЕРТИКАЛЬНА. Так это и сломалось: "
                    + "начальная ось дуги смотрела вниз вместо «назад», дуга родилась в "
                    + "стороне от прямого участка, и протяжка соединила их наклонной "
                    + "перемычкой — стойка встала в коробке по диагонали");
                Assert.AreEqual(0f, point.x, Tol,
                    "и никуда не уходит вбок: боковой увод невозможно отличить от "
                    + "повёрнутого элемента, пока не посмотришь на габарит");
            }
        }

        [Test]
        public void ShowerColumnLayout_RiserPath_HasExactlyTwoStraightRunsAndTheArcBetweenThem()
        {
            var spec = ShowerColumnSpec.Default;
            var path = ShowerColumnLayout.RiserPath(spec);
            float chord = 2f * ShowerColumnLayout.BendRadiusMM(spec.RiserDiameterMM)
                * Mathf.Sin(Mathf.PI * 0.25f / ShowerColumnLayout.GooseneckArcSegments);
            int longSteps = 0;
            int lastLong = -1;

            for (int i = 1; i < path.Length; i++)
            {
                float step = (path[i] - path[i - 1]).magnitude;
                Assert.Greater(step, Tolerance.ContactMm,
                    "совпавшие точки на стыке прямой и дуги: направление между ними не "
                    + "определено, и кольцо протяжки в этом узле схлопнется");
                if (step <= chord * LongStepFactor) continue;
                longSteps++;
                lastLong = i;
            }

            Assert.AreEqual(2, longSteps,
                "длинных звеньев в ломаной ровно ДВА — прямой участок штанги и "
                + "горизонтальный вынос. Всё между ними короче хорды дуги. Третье "
                + "длинное звено означает, что дуга родилась в стороне и протяжка "
                + "дотянулась до неё перемычкой: это и есть перекос стойки");
            Assert.AreEqual(path.Length - 1, lastLong,
                "и второе длинное звено — последнее в ломаной: вынос идёт ПОСЛЕ гиба, а "
                + "не где-то в его середине");
        }

        [Test]
        public void ShowerColumnLayout_RiserPath_JoinsTheBendToBothStraightRuns()
        {
            var spec = ShowerColumnSpec.Default;
            var path = ShowerColumnLayout.RiserPath(spec);
            float bend = ShowerColumnLayout.BendRadiusMM(spec.RiserDiameterMM);
            var centre = ShowerColumnLayout.BendCentreMM(spec);

            Assert.AreEqual(bend, (new Vector3(0f, centre.y, spec.WallOffsetMM) - centre)
                .magnitude, Tol,
                "верх прямого участка лежит РОВНО на радиусе гиба от его центра: сдвинь "
                + "центр — и дуга начнётся не там, где кончилась труба");
            Assert.AreEqual(bend,
                (new Vector3(0f, ShowerColumnLayout.ArmAxisYMM(spec), spec.WallOffsetMM + bend)
                    - centre).magnitude, Tol,
                "и низ горизонтального выноса — тоже: обе касательные гиба выходят на "
                + "прямые без излома");
            Assert.AreEqual(ShowerColumnLayout.ArmAxisYMM(spec), path[path.Length - 2].y, Tol,
                "последняя точка дуги уже стоит на высоте выноса — дальше труба идёт "
                + "строго горизонтально");
        }

        [Test]
        public void ShowerColumnLayout_BoundsMM_StartsAtTheWallPlaneAndEndsAtTheColumnTop()
        {
            var spec = ShowerColumnSpec.Default;
            var bounds = ShowerColumnLayout.BoundsMM(spec);

            Assert.AreEqual(0f, bounds.min.z, Tol,
                "розетки кронштейнов и задняя стенка дивертора лежат в плоскости стены: "
                + "это та грань, которой стойка садится на стену");
            Assert.AreEqual(spec.ColumnHeightMM, bounds.max.y, Tol,
                "верх габарита — это верх стойки: заявленная высота обязана быть той самой "
                + "высотой, а не приблизительной");
        }

        [Test]
        public void ShowerColumnLayout_FaceLooksAwayFromTheWall_SoTheIsoFrameMustTurnItAround()
        {
            var spec = ShowerColumnSpec.Default;

            Assert.Greater(spec.ArmReachMM, spec.WallOffsetMM,
                "гусак уходит ОТ стены в +Z");
            Assert.Greater(ShowerColumnLayout.HolderCentreMM(spec).z, spec.WallOffsetMM,
                "держатель ручной лейки вынесен туда же");
            Assert.Greater(ShowerColumnLayout.DiverterLever(spec).ToMM.z,
                ShowerColumnLayout.DiverterDepthMM(spec),
                "и рычаг дивертора торчит из передней стенки блока. Все три опознавательные "
                + "детали на одной стороне, камера изометрии стоит с противоположной, и "
                + "потому кадр стойки обязан её развернуть: снятая с изнанки лейка "
                + "выглядит ложкой");
        }

        [Test]
        public void ShowerColumnLayout_DimensionsMM_AreAsWideAsTheRainHead()
        {
            var spec = ShowerColumnSpec.Default;

            Assert.AreEqual(spec.HeadDiameterMM, ShowerColumnLayout.DimensionsMM(spec).x,
                "самая широкая деталь стойки — тропическая лейка: всё остальное узкое и "
                + "стоит в её проекции");
        }

        [Test]
        public void ShowerColumnLayout_BoundsMM_AreDraggedDownByTheHoseAndNothingElse()
        {
            var spec = ShowerColumnSpec.Default;
            var bounds = ShowerColumnLayout.BoundsMM(spec);
            float hoseBottom = PipePath.LowestPoint(ShowerColumnLayout.HosePath(spec)).y
                - ShowerColumnLayout.HoseDiameterMM * 0.5f;

            Assert.AreEqual(hoseBottom, bounds.min.y, Tol,
                "низ коробки задаёт ИМЕННО петля шланга: разойдись эти два числа — либо "
                + "шланг вываливается из габарита, либо под стойкой висит пустой воздух");
            Assert.Greater(ShowerColumnLayout.DimensionsMM(spec).y, spec.ColumnHeightMM,
                "и потому габарит выше самой стойки: петля свисает ниже дивертора");
        }

        [Test]
        public void ShowerColumnLayout_HosePath_HangsFromTheDiverterToTheHandShower()
        {
            var spec = ShowerColumnSpec.Default;
            var hose = ShowerColumnLayout.HosePath(spec);

            Assert.Less((hose[0] - ShowerColumnLayout.HoseOutletMM(spec)).magnitude, Tol,
                "шланг выходит из дивертора");
            Assert.Less((hose[hose.Length - 1] - ShowerColumnHandShower.GripBottomMM(spec))
                .magnitude, Tol,
                "и приходит в нижний торец рукоятки ручной лейки — не «примерно туда», а "
                + "в ту самую точку, от которой построена сама рукоятка");
            Assert.Less(PipePath.LowestPoint(hose).y, 0f,
                "и провисает петлёй НИЖЕ дивертора: натянутый по прямой шланг сразу выдаёт "
                + "нарисованную по двум точкам модель");
        }

        [Test]
        public void ShowerColumnLayout_HosePath_NeverGoesBehindTheWall()
        {
            var spec = ShowerColumnSpec.Default;

            foreach (var point in ShowerColumnLayout.HosePath(spec))
                Assert.GreaterOrEqual(point.z + Tol, ShowerColumnLayout.HoseDiameterMM * 0.5f,
                    "шланг целиком перед стеной: провалившись за z=0, он ушёл бы в кладку "
                    + "и потянул бы туда же заднюю грань габарита, которой стойка садится "
                    + "на стену");
        }

        [Test]
        public void ShowerColumnLayout_Bracket_IsARosetteAThinArmAndAClampWithStepsBetween()
        {
            var spec = ShowerColumnSpec.Default;
            float upper = spec.ColumnHeightMM * ShowerColumnLayout.UpperBracketRatio;
            var rosette = ShowerColumnLayout.BracketRosette(spec, upper);
            var arm = ShowerColumnLayout.BracketArm(spec, upper);
            var collar = ShowerColumnLayout.BracketCollar(spec, upper);

            Assert.AreEqual(0f, rosette.FromMM.z, Tol,
                "кронштейн упирается розеткой в стену");
            Assert.AreEqual(rosette.FromRadiusMM, rosette.ToRadiusMM, Tol,
                "розетка — плоский диск, а не конус: у конуса нет силуэта");
            Assert.AreEqual(spec.WallOffsetMM, arm.ToMM.z, Tol,
                "плечо доходит ровно до оси штанги: короче — щель, длиннее — труба "
                + "протыкает кронштейн насквозь");
            Assert.Less(arm.FromRadiusMM, collar.FromRadiusMM,
                "плечо ТОНЬШЕ хомута");
            Assert.Less(arm.FromRadiusMM, rosette.FromRadiusMM,
                "и тоньше розетки: именно этот перепад создаёт ЗАЗОР, в котором глаз "
                + "видит, что кронштейн — отдельная деталь, а не вздутие трубы");
            Assert.Greater(collar.FromRadiusMM, ShowerColumnLayout.RiserRadiusMM(spec),
                "а хомут ОХВАТЫВАЕТ штангу: заподлицо он неотличим от самой трубы");
        }

        [Test]
        public void ShowerColumnLayout_Brackets_HoldTheRiserAtTwoDifferentHeights()
        {
            var spec = ShowerColumnSpec.Default;

            Assert.Greater(ShowerColumnLayout.UpperBracketRatio,
                ShowerColumnLayout.LowerBracketRatio,
                "верхний кронштейн выше нижнего: стойка держится на двух точках, а не "
                + "висит на одной");
            Assert.Greater(ShowerColumnLayout.LowerBracketRatio * spec.ColumnHeightMM,
                ShowerColumnLayout.DiverterHeightMM(spec),
                "и нижний стоит ВЫШЕ дивертора: сядь он на блок — и два узла срастутся в "
                + "один ком у самого пола");
        }

        [Test]
        public void ShowerColumnLayout_Holder_WrapsTheHandShowerGripOnItsOwnAxis()
        {
            var spec = ShowerColumnSpec.Default;
            var cup = ShowerColumnLayout.HolderCup(spec);
            var grip = ShowerColumnHandShower.Grip(spec);
            var arm = ShowerColumnLayout.HolderArm(spec);
            var centre = ShowerColumnLayout.HolderCentreMM(spec);

            Assert.Less(Vector3.Cross(cup.AxisMM.normalized, grip.AxisMM.normalized).magnitude,
                Tolerance.EpsilonUnits,
                "чашка держателя СООСНА рукоятке: развернись она вертикально, лейка "
                + "торчала бы из неё наискось");
            Assert.Less(Vector3.Cross(centre - grip.FromMM, grip.AxisMM.normalized).magnitude
                / grip.LengthMM, Tolerance.EpsilonUnits,
                "и её центр лежит НА оси рукоятки — именно от него рукоятка и построена, "
                + "вверх и вниз по одному направлению");
            Assert.Greater(cup.FromRadiusMM,
                ShowerColumnHandShower.GripRadiusAtHolderMM(spec),
                "чашка ОХВАТЫВАЕТ рукоятку: уже неё — и держатель прячется внутри лейки");
            Assert.Less(arm.FromRadiusMM, cup.FromRadiusMM,
                "а плечо держателя тоньше чашки: та же ступень, что у кронштейна, и по "
                + "той же причине");
        }

        [Test]
        public void ShowerColumnLayout_Holder_CarriesTheHandShowerClearOfTheRiser()
        {
            var spec = ShowerColumnSpec.Default;
            var grip = ShowerColumnHandShower.Grip(spec);
            float gap = grip.FromMM.z - spec.WallOffsetMM
                - ShowerColumnLayout.RiserRadiusMM(spec)
                - Mathf.Max(grip.FromRadiusMM, grip.ToRadiusMM);

            Assert.Greater(gap, 0f,
                "между штангой и рукояткой ручной лейки остаётся ВОЗДУХ. Прижми лейку к "
                + "трубе — и на общем виде обе сольются в одно утолщение: так и было, "
                + "пока вынос держателя был вдвое короче");
            Assert.Greater(ShowerColumnLayout.HolderCentreMM(spec).z - spec.WallOffsetMM,
                ShowerColumnLayout.SliderCollar(spec).FromRadiusMM,
                "и сам вынос длиннее радиуса хомута: иначе чашка сидит внутри хомута");
        }

        [Test]
        public void ShowerColumnHandShower_LeansForwardWithItsHeadUp()
        {
            var spec = ShowerColumnSpec.Default;
            var grip = ShowerColumnHandShower.Grip(spec);
            var face = ShowerColumnHandShower.HeadFace(spec);

            Assert.Greater(grip.ToMM.y, grip.FromMM.y, "рукоятка стоит головкой ВВЕРХ");
            Assert.Greater(grip.ToMM.z, grip.FromMM.z,
                "и наклонена ВПЕРЁД: строго вертикальная лейка в держателе выглядит "
                + "приклеенной к штанге");
            Assert.Greater(grip.ToRadiusMM, grip.FromRadiusMM,
                "рукоятка расширяется кверху: снизу на неё садится шланг, сверху — лейка");
            Assert.AreEqual(spec.HandShowerDiameterMM * 0.5f, face.FromRadiusMM, Tol,
                "головка ручной лейки Ø 110 мм с референса");
            Assert.Less(face.ToRadiusMM, face.FromRadiusMM,
                "и её лицевая сторона слегка завалена внутрь, а не срезана плоско");
        }

        [Test]
        public void ShowerColumnHandShower_Head_IsADiscWithAStraightRim()
        {
            var spec = ShowerColumnSpec.Default;
            var rim = ShowerColumnHandShower.HeadRim(spec);

            Assert.AreEqual(rim.FromRadiusMM, rim.ToRadiusMM, Tol,
                "у головки есть ПРЯМОЙ поясок постоянного радиуса — та самая видимая "
                + "толщина, без которой лейка читается блямбой");
            Assert.Greater(rim.LengthMM, 0f, "и он не выродился в ноль");
            Assert.Greater(ShowerColumnHandShower.HeadThicknessMM(spec)
                / spec.HandShowerDiameterMM, ShowerColumnHandShower.MinHeadStoutnessRatio,
                "и головка достаточно толстая относительно своего диаметра: плоский блин "
                + "на изометрии виден с ребра и превращается в палку поперёк рукоятки");
        }

        [Test]
        public void ShowerColumnHandShower_Parts_FormOneUnbrokenChain()
        {
            var spec = ShowerColumnSpec.Default;
            var parts = ShowerColumnHandShower.Parts(spec);

            for (int i = 1; i < parts.Length; i++)
            {
                Assert.AreEqual(parts[i - 1].ToMM, parts[i].FromMM,
                    "рукоятка, шейка и головка идут встык: разрыв между ними — это дырка "
                    + "в лейке, а нахлёст — тот самый комок вместо детали");
                Assert.AreEqual(parts[i - 1].ToRadiusMM, parts[i].FromRadiusMM, Tol,
                    "и радиусы на стыке совпадают: ступенька здесь читается как трещина");
            }
        }

        [Test]
        public void ShowerColumnHandShower_StaysClearOfTheRainHead()
        {
            var spec = ShowerColumnSpec.Default;
            float handTop = ShowerColumnHandShower.HeadFaceMM(spec).y
                + spec.HandShowerDiameterMM * 0.5f;

            Assert.Less(handTop, ShowerColumnRainHead.BottomYMM(spec),
                "ручная лейка целиком НИЖЕ тропической: наклон подымает её головку, и "
                + "стоит перестараться с ним — две лейки срастутся в одну кляксу");
        }

        [Test]
        public void ShowerColumnRainHead_HangsUnderTheArmOnABossAndKeepsAFlatDisc()
        {
            var spec = ShowerColumnSpec.Default;
            var boss = ShowerColumnRainHead.Boss(spec);
            var rim = ShowerColumnRainHead.Rim(spec);
            var bottom = ShowerColumnRainHead.BottomChamfer(spec);

            Assert.AreEqual(spec.ArmReachMM, rim.FromMM.z, Tol, "лейка соосна концу выноса");
            Assert.AreEqual(ShowerColumnRainHead.TopYMM(spec), boss.ToMM.y, Tol,
                "бобышка касается нижней образующей выноса: щель между ними видно "
                + "насквозь, а нахлёст даёт кольцевой шов");
            Assert.Greater(boss.FromRadiusMM, ShowerColumnLayout.RiserRadiusMM(spec),
                "бобышка ТОЛЩЕ трубы");
            Assert.Less(boss.FromRadiusMM, rim.FromRadiusMM,
                "и тоньше диска: это переходная ступень между трубой и лейкой, без "
                + "которой труба втекает в диск и всё вместе читается ложкой");
            Assert.AreEqual(rim.FromRadiusMM, rim.ToRadiusMM, Tol,
                "поясок диска — ПРЯМОЙ цилиндр постоянного радиуса: это и есть видимая "
                + "толщина лейки, ради которой всё затевалось");
            Assert.AreEqual(spec.HeadDiameterMM * 0.5f, rim.FromRadiusMM, Tol,
                "и его радиус — заявленные Ø 250 мм, а не меньше на величину заваленной "
                + "кромки");
            Assert.Less(bottom.FromRadiusMM, bottom.ToRadiusMM,
                "снизу кромка завалена фаской: острый край в литом пластике не бывает");
            Assert.Less(ShowerColumnRainHead.TopChamferMM(spec),
                ShowerColumnRainHead.BottomChamferMM(spec),
                "а СВЕРХУ фаска заметно у́же нижней. Камера изометрии смотрит на лейку "
                + "под тридцать градусов сверху, и широкая верхняя фаска в этой "
                + "проекции даёт кольцо во всю ширину диска — она-то и съедала весь "
                + "поясок, из-за чего лейка читалась плоской пилюлей");
            Assert.Greater(rim.LengthMM, spec.HeadThicknessMM * 0.5f,
                "и прямой поясок занимает БОЛЬШЕ половины толщины: он и есть та "
                + "видимая толщина, ради которой всё затевалось, а фаски — только "
                + "притупление кромок");
            Assert.AreEqual(spec.HeadThicknessMM,
                ShowerColumnRainHead.DiscTopYMM(spec) - ShowerColumnRainHead.BottomYMM(spec),
                Tol,
                "а вся пачка колец в сумме даёт заявленные 30 мм толщины: бобышка сверх "
                + "них, она часть подвеса, а не лейки");
        }

        [Test]
        public void ShowerColumnLayout_DiverterBox_SitsOnTheWallAndCarriesALeverOffAxis()
        {
            var spec = ShowerColumnSpec.Default;
            var box = ShowerColumnLayout.DiverterBox(spec);
            var lever = ShowerColumnLayout.DiverterLever(spec);

            Assert.AreEqual(0f, box.CentreMM.y - box.SizeMM.y * 0.5f, Tol,
                "низ дивертора — это низ стойки");
            Assert.AreEqual(0f, box.CentreMM.z - box.SizeMM.z * 0.5f, Tol,
                "задняя стенка дивертора лежит на стене");
            Assert.Greater(box.CentreMM.z + box.SizeMM.z * 0.5f,
                spec.WallOffsetMM + ShowerColumnLayout.RiserRadiusMM(spec),
                "и блок накрывает штангу целиком: труба, торчащая из передней стенки "
                + "переключателя, выглядит как ошибка сборки");
            Assert.Greater(lever.ToMM.z, box.CentreMM.z + box.SizeMM.z * 0.5f,
                "рычаг ВЫХОДИТ за переднюю стенку блока: без него дивертор остаётся "
                + "гладким комом, по которому не понять, что это орган управления");
            Assert.Less(lever.FromMM.z, box.CentreMM.z + box.SizeMM.z * 0.5f,
                "а корень его утоплен внутрь — иначе на стенке будет кольцевой шов");
        }

        [Test]
        public void ShowerColumnSpec_Clamped_PushesTheArmOutPastTheBend()
        {
            var spec = ShowerColumnSpec.Clamped(1150, 32, 250, 30, 80, 60, 110, 1000);

            Assert.Greater(spec.ArmReachMM,
                spec.WallOffsetMM + ShowerColumnLayout.BendRadiusMM(spec.RiserDiameterMM),
                "вынос короче гиба невозможен: четверть окружности закончилась бы дальше "
                + "точки подвеса, и гусак загнулся бы назад к стене");
        }

        [Test]
        public void ShowerColumnLayout_CentreAboveFloorMM_PutsTheRainHeadOverhead()
        {
            var spec = ShowerColumnSpec.Default;
            float centre = ShowerColumnLayout.CentreAboveFloorMM(spec);
            var bounds = ShowerColumnLayout.BoundsMM(spec);
            float diverterBottom = centre - bounds.center.y;

            Assert.AreEqual(ShowerColumnLayout.DiverterAboveFloorMM, diverterBottom, Tol,
                "стойка вешается по НИЗУ ДИВЕРТОРА: центр габарита утянут вниз петлёй "
                + "шланга, и по нему стойка села бы почти на полметра ниже");
            Assert.Greater(diverterBottom + spec.ColumnHeightMM, 2000f,
                "верх стойки обязан оказаться выше человека: тропическая лейка на высоте "
                + "плеча — это не душ, а мойка");
        }

        [Test]
        public void ShowerColumnSpec_Default_MatchesTheReferencePhoto()
        {
            var spec = ShowerColumnSpec.Default;

            Assert.AreEqual(250, spec.HeadDiameterMM, "тропическая лейка Ø 250 мм");
            Assert.AreEqual(30, spec.HeadThicknessMM, "лейка плоская, около 30 мм");
            Assert.AreEqual(110, spec.HandShowerDiameterMM, "ручная лейка Ø 110 мм");
            Assert.AreEqual(1150, spec.ColumnHeightMM,
                "высота стойки от дивертора до верха гусака");
        }
    }
}
