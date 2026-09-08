using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

namespace KitchenDesigner.Tests.Geometry
{
    /// <summary>Доворот по устьям: правило, по которому деталь САМА поворачивается нужной
    /// стороной, а не только подъезжает.
    ///
    /// Посадка `SnapPortSeat` умеет только транслировать, и это осознанно: она работает
    /// на каждом кадре перетаскивания, а перебор поворотов там был бы лишним. Поэтому
    /// уголок, поднесённый к трубе не тем устьем, до сих пор не стыковался ничем, кроме
    /// ручного поворота: «устья сошлись» и «устья смотрят навстречу» — разные условия, и
    /// снэп проверял только первое.
    ///
    /// Здесь живёт второе. Доворот считается ОДИН раз — при отпускании кнопки, через
    /// `IAutoSeated.SeatAfterMove`, — поэтому у него своя функция и свой файл, а не ветка
    /// внутри покадрового отбора. Ядро не строит поворот (Quaternion.AngleAxis — ECall и
    /// под dotnet не живёт): оно возвращает ОСЬ и УГОЛ, а собирает из них позу
    /// `PipeDocking` на стороне сцены. Сквозная проверка «применили план — устье село на
    /// устье» — `PipeFittingDockSceneTests`, там же отмена.
    ///
    /// Конкуренцию между несколькими целями решает КУРСОР, а не зазор: пользователь
    /// показывает мышью, к какой трубе он несёт деталь. Пара тестов
    /// `..._TheOneNearestTheCursorWins...` / `..._WithoutACursor_...` держит оба правила
    /// врозь — сцена в них одна и та же, и побеждают в ней РАЗНЫЕ трубы.</summary>
    public class SnapPortDockTests
    {
        private const float ToU = AppConstants.MM_TO_UNITS;

        private const float Threshold = 50f * ToU;

        private static PortedPart Part(string name, params SnapPort[] ports) =>
            new PortedPart(name.GetHashCode(), name, ports);

        private static SnapPort Mouth(float xMm, float yMm, float zMm, Vector3 outward) =>
            new SnapPort(new Vector3(xMm * ToU, yMm * ToU, zMm * ToU), outward);

        [Test]
        public void For_TheMouthPointsAwayFromTheTarget_AsksForTheHalfTurnThatMeetsIt()
        {
            var moved = Part("Elbow", Mouth(0f, 0f, 0f, Vector3.right));
            var pipe = Part("Pipe", Mouth(30f, 0f, 0f, Vector3.right));

            var dock = SnapPortDock.For(moved, pipe, Threshold, SnapCursor.None);

            Assert.IsTrue(dock.taken, "устья в 30 мм при пороге 50 мм — доворот обязан быть");
            Assert.AreEqual(180f, dock.rotationDegrees, 0.01f,
                "оба устья смотрят в +X: чужое надо встретить своим, а значит развернуться "
                + "на 180°. Это ровно тот случай, который до появления доворота не "
                + "стыковался никогда — сдвиг сводил ТОЧКИ, а трубы всё равно смотрели в "
                + "одну сторону");
        }

        [Test]
        public void For_TheMouthStandsAtRightAnglesToTheTarget_AsksForAQuarterTurn()
        {
            var moved = Part("Elbow", Mouth(0f, 0f, 0f, Vector3.right));
            var pipe = Part("Pipe", Mouth(0f, 30f, 0f, Vector3.up));

            var dock = SnapPortDock.For(moved, pipe, Threshold, SnapCursor.None);

            Assert.AreEqual(90f, dock.rotationDegrees, 0.01f,
                "чужое устье смотрит вверх, значит наше обязано смотреть вниз: от +X до "
                + "-Y ровно четверть оборота");
        }

        [Test]
        public void For_TheMouthsAlreadyFaceEachOther_AsksForNoTurnAtAll()
        {
            var moved = Part("Elbow", Mouth(0f, 0f, 0f, Vector3.up));
            var pipe = Part("Pipe", Mouth(0f, 30f, 0f, Vector3.down));

            var dock = SnapPortDock.For(moved, pipe, Threshold, SnapCursor.None);

            Assert.AreEqual(0f, dock.rotationDegrees, 0.01f,
                "деталь уже повёрнута правильно — доворот обязан быть нулевым, иначе "
                + "каждое отпускание кнопки дёргало бы уже стоящую на месте деталь");
        }

        [Test]
        public void For_AThreeMouthedPart_DocksWithTheMouthItWasBroughtUpWith()
        {
            var tee = Part("Tee",
                Mouth(0f, -50f, 0f, Vector3.down),
                Mouth(0f, 50f, 0f, Vector3.up),
                Mouth(50f, 0f, 0f, Vector3.right));
            var pipe = Part("Pipe", Mouth(80f, 0f, 0f, Vector3.left));

            var dock = SnapPortDock.For(tee, pipe, Threshold, SnapCursor.None);

            Assert.AreEqual(2, dock.movedPort,
                "боковое устье тройника в 30 мм от трубы, верхнее и нижнее — дальше. "
                + "Стыкуется то устье, КОТОРЫМ ДЕТАЛЬ ПОДНОСЯТ, иначе тройник развернулся "
                + "бы к трубе случайной ножкой");
            Assert.AreEqual(0f, dock.rotationDegrees, 0.01f,
                "это устье уже смотрит навстречу — доворачивать нечего");
        }

        [Test]
        public void For_TwoMouthsAtTheSameGap_TheOneWithTheSmallerTurnWins()
        {
            var coupling = Part("Cpl",
                Mouth(0f, 0f, 0f, Vector3.down),
                Mouth(0f, 0f, 0f, Vector3.up));
            var pipe = Part("Pipe", Mouth(0f, 30f, 0f, Vector3.down));

            var dock = SnapPortDock.For(coupling, pipe, Threshold, SnapCursor.None);

            Assert.AreEqual(1, dock.movedPort,
                "оба устья муфты в одной точке, зазор у них одинаковый — при равном "
                + "зазоре выигрывает то, которому доворачиваться меньше. Без этого "
                + "правила симметричная деталь кувыркалась бы на 180° без всякой причины");
            Assert.AreEqual(0f, dock.rotationDegrees, 0.01f,
                "и выбранное устье действительно то, которому доворачиваться нечего — "
                + "иначе индекс совпал бы случайно, а план поворота остался бы чужим");
        }

        [Test]
        public void For_EveryMouthBeyondTheThreshold_IsNotTakenButStillReportsTheDistance()
        {
            var moved = Part("Elbow", Mouth(0f, 0f, 0f, Vector3.up));
            var pipe = Part("Pipe", Mouth(0f, 200f, 0f, Vector3.down));

            var dock = SnapPortDock.For(moved, pipe, Threshold, SnapCursor.None);

            Assert.IsFalse(dock.taken, "200 мм больше порога 50 мм — доворота нет");
            Assert.AreEqual(200f * ToU, dock.mouthGapUnits, 1e-5f,
                "но замер обязан доехать до отчёта: AGENTS.md — «Measure first, reject "
                + "after». Без этого числа snap_diagnose не сможет сказать, НАСКОЛЬКО "
                + "деталь не дотянулась, и ранний выход снова обесценит оракула");
        }

        [Test]
        public void For_TheOtherSideCarriesNoMouths_IsNotADockAtAll()
        {
            var moved = Part("Elbow", Mouth(0f, 0f, 0f, Vector3.up));
            var board = Part("Board");

            var dock = SnapPortDock.For(moved, board, Threshold, SnapCursor.None);

            Assert.IsFalse(dock.bothSidesCarryPorts,
                "у доски устьев нет: правило про устья к ней неприменимо, и отчёт обязан "
                + "это различать, а не молча возвращать «не сошлось»");
            Assert.IsFalse(dock.taken,
                "и доворота к доске быть не может ни при каком зазоре");
        }

        private static List<PortedPart> TwoPipesAroundTheOrigin() => new List<PortedPart>
        {
            Part("NearByGap", Mouth(20f, 0f, 0f, Vector3.down)),
            Part("NearToCursor", Mouth(0f, 0f, 30f, Vector3.down)),
        };

        private static PortedPart TheElbow() =>
            Part("Elbow", Mouth(0f, 0f, 0f, Vector3.up));

        [Test]
        public void Best_TwoPipesInRange_TheOneNearestTheCursorWins_NotTheOneWithTheSmallestGap()
        {
            var cursor = SnapCursor.AlongRay(new Vector3(0f, 1f, 30f * ToU), Vector3.down);

            var dock = SnapPortDock.Best(TheElbow(), TwoPipesAroundTheOrigin(), Threshold, cursor);

            Assert.IsTrue(dock.taken,
                "обе трубы в пределах порога — доворот обязан состояться, вопрос только "
                + "в том, к КОТОРОЙ");
            Assert.AreEqual("NearToCursor", dock.targetName,
                "по зазору выигрывает NearByGap (20 мм против 30 мм), по курсору — "
                + "NearToCursor: луч проходит ровно через её устье. Побеждать обязан "
                + "курсор — пользователь показывает мышью, К КАКОЙ трубе он несёт деталь, "
                + "и «ближайшая по зазору» ему в этот момент не видна никак");
            Assert.IsTrue(dock.decidedByCursor,
                "и отчёт обязан сказать, что выбор сделан курсором: без этого "
                + "snap_diagnose без курсора ответит уверенно и не тем правилом");
        }

        [Test]
        public void Best_TheSameTwoPipesWithoutACursor_FallBackToTheSmallestGap()
        {
            var dock = SnapPortDock.Best(TheElbow(), TwoPipesAroundTheOrigin(), Threshold,
                SnapCursor.None);

            Assert.AreEqual("NearByGap", dock.targetName,
                "сцена та же, что и в тесте выше, а победитель ДРУГОЙ — этим два правила "
                + "и различаются. Без этой пары «выигрывает ближайшая к курсору» нельзя "
                + "отличить от «выигрывает ближайшая по зазору»: в любой сцене, где они "
                + "совпадают, зелены оба");
            Assert.IsFalse(dock.decidedByCursor,
                "и решение принято без курсора — оракул обязан говорить это вслух, а не "
                + "выдавать ответ по зазору за ответ по указателю");
        }

        [Test]
        public void Best_NothingInRange_ReportsTheNearestMouthAnyway()
        {
            var far = new List<PortedPart> { Part("Pipe", Mouth(0f, 300f, 0f, Vector3.down)) };

            var dock = SnapPortDock.Best(TheElbow(), far, Threshold, SnapCursor.None);

            Assert.IsFalse(dock.taken, "300 мм больше порога 50 мм — доворота нет");
            Assert.IsTrue(dock.bothSidesCarryPorts,
                "устья есть у обоих — это не «нечего сравнивать», а «далеко»");
            Assert.AreEqual(300f * ToU, dock.mouthGapUnits, 1e-5f,
                "и отбор по всей сцене обязан донести замер до отчёта так же, как отбор "
                + "по одной паре: иначе snap_diagnose снова начнёт молчать о том, "
                + "насколько деталь не дотянулась");
        }

        [Test]
        public void TurnOnto_AMouthPointingStraightBack_TurnsAboutTheUpright_SoThePartStaysUpright()
        {
            SnapPortDock.TurnOnto(Vector3.right, Vector3.left, out Vector3 axis,
                out float degrees);

            Assert.AreEqual(180f, degrees, 0.01f,
                "устья смотрят строго в разные стороны — доворот на полоборота");
            Assert.AreEqual(1f, Mathf.Abs(Vector3.Dot(axis, Vector3.up)), 0.01f,
                "разворот на 180° вокруг ЛЮБОЙ перпендикулярной оси сводит устья, но "
                + "только вокруг вертикали он не кладёт фитинг набок. Ось здесь — не "
                + "деталь реализации: от неё зависит, куда после доворота смотрят "
                + "ОСТАЛЬНЫЕ устья тройника");
        }

        [Test]
        public void TurnOnto_AVerticalMouthPointingStraightBack_FallsBackToAHorizontalAxis()
        {
            SnapPortDock.TurnOnto(Vector3.up, Vector3.down, out Vector3 axis, out float degrees);

            Assert.AreEqual(180f, degrees, 0.01f,
                "вертикальное устье тоже разворачивается на полоборота");
            Assert.AreEqual(0f, Vector3.Dot(axis, Vector3.up), 0.01f,
                "для вертикального устья вертикаль осью быть не может — вокруг неё "
                + "устье никуда не повернётся, и деталь молча осталась бы стоять не той "
                + "стороной");
        }

        [Test]
        public void DistanceTo_APointBehindTheCursor_IsMeasuredFromTheEyeAndNotThroughTheWall()
        {
            var cursor = SnapCursor.AlongRay(Vector3.zero, Vector3.forward);

            Assert.AreEqual(2f, cursor.DistanceTo(new Vector3(0f, 0f, -2f)), 1e-4f,
                "точка позади камеры не лежит на луче: мерить до неё расстояние по "
                + "бесконечной ПРЯМОЙ значило бы объявить деталь за спиной ближайшей к "
                + "указателю");
            Assert.AreEqual(0f, cursor.DistanceTo(new Vector3(0f, 0f, 5f)), 1e-4f,
                "а точка прямо по лучу — на нулевом расстоянии от него: это "
                + "положительный контроль, без него мера могла бы быть сломана целиком");
        }

        [Test]
        public void DistanceTo_WithoutACursor_AnswersThatItDoesNotKnow()
        {
            Assert.AreEqual(-1f, SnapCursor.None.DistanceTo(Vector3.one),
                "отсутствие курсора обязано быть ОТЛИЧИМО от нулевого расстояния: иначе "
                + "деталь без курсора выглядит как деталь прямо под указателем и "
                + "выигрывает любую конкуренцию");
        }
    }
}
