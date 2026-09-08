using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

namespace KitchenDesigner.Tests.Geometry
{
    /// <summary>Посадка по устьям: правило, по которому снэп сажает деталь ТОЧКОЙ В ТОЧКУ,
    /// а не гранью к грани.
    ///
    /// Здесь оно проверяется на голых коробках с портами, без всякой сантехники: у
    /// правила нет ни одного знания о трубах, и это существенно — устье порта для него
    /// просто точка с направлением. Сантехнический замер — `PipeFittingPairSnapTests` и
    /// `PipeFittingSnapProbeTests`, сцена — `PipeFittingSnapSceneProbeTests`.
    ///
    /// Правило живёт в ОДНОЙ функции, которую зовут и отбор (`SnapCore.TrySnap`), и
    /// оракул (`SnapSystem.Diagnose` через `SnapNeighbourFacts`), и изменение размера
    /// (`ResizeSnap`) — см. AGENTS.md → «A rule added to candidate SELECTION must reach
    /// Diagnose in the same commit». Сторож на третье место — `SnapPortRuleSingleSourceTests`.</summary>
    public class SnapPortSeatTests
    {
        private const float ToU = AppConstants.MM_TO_UNITS;

        private const float Threshold = 50f * ToU;

        private static ElementGeometry Boxed(string name, Vector3 centre, params SnapPort[] ports)
            => ElementGeometry.Box(name, centre, new Vector3(0.1f, 0.1f, 0.1f),
                Quaternion.identity, false, default, 0f, ports);

        private sealed class Posed : IPosedGeometry
        {
            private readonly Vector3 _mouthOffset;
            private readonly Vector3 _outward;

            public Posed(Vector3 mouthOffset, Vector3 outward)
            {
                _mouthOffset = mouthOffset;
                _outward = outward;
            }

            public ElementGeometry At(Vector3 position) =>
                Boxed("Moved", position, new SnapPort(position + _mouthOffset, _outward));
        }

        [Test]
        public void Best_MouthsFacingEachOtherWithinThreshold_ReturnsTheTranslationThatMeetsThem()
        {
            var moved = Boxed("A", Vector3.zero, new SnapPort(Vector3.zero, Vector3.up));
            var other = Boxed("B", Vector3.zero,
                new SnapPort(new Vector3(0.01f, 0.02f, 0f), Vector3.down));

            var seat = SnapPortSeat.Best(moved, other, Threshold);

            Assert.IsTrue(seat.taken, "устья в 22 мм при пороге 50 мм — посадка обязана быть");
            Assert.IsTrue(seat.opposed, "оси устьев встречные");
            Assert.AreEqual(new Vector3(0.01f, 0.02f, 0f), seat.delta,
                "сдвиг — это ровно вектор от своего устья к чужому: посадка совмещает "
                + "ТОЧКИ, поэтому форма и размер коробки в неё не входят никак");
        }

        [Test]
        public void Best_MouthsBeyondTheThreshold_IsNotTakenButStillReportsTheDistance()
        {
            var moved = Boxed("A", Vector3.zero, new SnapPort(Vector3.zero, Vector3.up));
            var other = Boxed("B", Vector3.zero, new SnapPort(new Vector3(0f, 0.2f, 0f), Vector3.down));

            var seat = SnapPortSeat.Best(moved, other, Threshold);

            Assert.IsFalse(seat.taken, "200 мм больше порога 50 мм — посадки нет");
            Assert.AreEqual(0.2f, seat.mouthGapUnits, 1e-5f,
                "но замер обязан доехать до отчёта: AGENTS.md → «Measure first, reject "
                + "after» — ранний выход, который экономит ветку и обесценивает "
                + "диагностику, уже стоил 78 ложных находок. Без этого числа "
                + "snap_diagnose не сможет сказать, НАСКОЛЬКО деталь не дотянулась");
        }

        [Test]
        public void Best_ANearerMouthPairThatDoesNotFaceUs_LosesToAFartherFacingOne()
        {
            var moved = Boxed("A", Vector3.zero, new SnapPort(Vector3.zero, Vector3.up));
            var other = Boxed("B", Vector3.zero,
                new SnapPort(new Vector3(0.005f, 0f, 0f), Vector3.up),
                new SnapPort(new Vector3(0.03f, 0f, 0f), Vector3.down));

            var seat = SnapPortSeat.Best(moved, other, Threshold);

            Assert.AreEqual(1, seat.otherPort,
                "устье в 5 мм смотрит В ТУ ЖЕ сторону, что наше — труба вошла бы в трубу "
                + "встык наружу. Встречная пара в 30 мм физически осмысленна, поэтому "
                + "выигрывает она, а не ближайшая");
            Assert.IsTrue(seat.opposed,
                "и отчёт обязан подтвердить, что выбрана именно встречная пара: без этого "
                + "флага snap_diagnose не отличит настоящий стык от подставленного устья");
        }

        [Test]
        public void Best_NoFacingPairInRange_StillSeatsTheNearestMouth()
        {
            var moved = Boxed("A", Vector3.zero, new SnapPort(Vector3.zero, Vector3.up));
            var other = Boxed("B", Vector3.zero, new SnapPort(new Vector3(0.01f, 0f, 0f), Vector3.up));

            var seat = SnapPortSeat.Best(moved, other, Threshold);

            Assert.IsTrue(seat.taken,
                "посадка — это ПОМОЩЬ В РАССТАНОВКЕ, а не стык: устье кладётся на устье и "
                + "тогда, когда оси ещё не сведены. Пользователь после этого поворачивает "
                + "деталь, и встречная пара выигрывает по правилу выше — «поднёс — "
                + "повернулось — соединилось». Отказ здесь оставил бы деталь на граневом "
                + "детенте, то есть ровно в том промахе, из-за которого работа и делалась");
            Assert.IsFalse(seat.opposed,
                "и отчёт обязан говорить, что оси НЕ встречные: иначе snap_diagnose "
                + "пообещает стык там, где PipeJoint его не увидит");
        }

        [Test]
        public void Best_OneSideHasNoPorts_IsNotAPortSeatAtAll()
        {
            var moved = Boxed("A", Vector3.zero, new SnapPort(Vector3.zero, Vector3.up));
            var plain = ElementGeometry.Box("Wall", Vector3.zero, new Vector3(1f, 1f, 1f));

            var seat = SnapPortSeat.Best(moved, plain, Threshold);

            Assert.IsFalse(seat.bothSidesCarryPorts,
                "у обычной детали устьев нет, и правило обязано молча уступить дорогу "
                + "граневому отбору: CONVENTIONS.md → «A gate that can only refuse must "
                + "have somewhere to fall back to»");
            Assert.IsFalse(seat.taken,
                "и никакой посадки по устьям для этой пары нет — иначе деталь притянуло бы "
                + "к стене по несуществующему устью");
        }

        [Test]
        public void TrySnap_WithAMouthInRange_SeatsMouthToMouth_BeforeAnyFaceDetent()
        {
            var host = Boxed("Host", Vector3.zero, new SnapPort(Vector3.zero, Vector3.down));
            var start = new Vector3(0.009f, -0.007f, 0.005f);
            var moved = new Posed(Vector3.zero, Vector3.up);

            var snap = SnapCore.TrySnap(moved, new List<ElementGeometry> { host }, start, Threshold);

            Assert.IsTrue(snap.snapped,
                "деталь поднесена устьем к устью на 12,5 мм по диагонали — снэп обязан "
                + "сработать");
            Assert.AreEqual(0f, Vector3.Distance(snap.position, Vector3.zero), 1e-5f,
                "посадка по устьям идёт ДО граневых детентов и с приоритетом над ними: "
                + "именно кромочный детент раньше утаскивал уголок с правильного места "
                + "на 6,7 мм");
        }

        [Test]
        public void TrySnap_WithTheMouthsAlreadyMet_LeavesThePartExactlyWhereItStands()
        {
            var host = Boxed("Host", Vector3.zero, new SnapPort(Vector3.zero, Vector3.down));
            var moved = new Posed(Vector3.zero, Vector3.up);

            var snap = SnapCore.TrySnap(moved, new List<ElementGeometry> { host }, Vector3.zero,
                Threshold);

            Assert.IsTrue(snap.snapped,
                "уже сидящая деталь остаётся «прилипшей» — иначе граневой отбор получил бы "
                + "её обратно и увёл бы с правильного места");
            Assert.AreEqual(Vector3.zero, snap.position,
                "и не двигается ни на микрон");
        }

        [Test]
        public void ResizeSnap_DraggingAFaceThatCarriesAMouth_StopsOnTheNeighboursMouth()
        {
            var self = Boxed("Run", Vector3.zero, new SnapPort(new Vector3(0f, 0.05f, 0f), Vector3.up));
            var other = Boxed("Fit", new Vector3(0f, 0.2f, 0f),
                new SnapPort(new Vector3(0f, 0.062f, 0f), Vector3.down));

            bool found = ResizeSnap.SnapDelta(new Vector3(0f, 0.05f, 0f), Vector3.up,
                Vector3.right, Vector3.forward, new Vector2(0.1f, 0.1f),
                new List<ElementGeometry> { other }, self, Threshold, out float gap);

            Assert.IsTrue(found,
                "растягивание — вторая реализация той же геометрии (AGENTS.md → «Snap "
                + "subsystem — two implementations, one geometry»), и правило про устья "
                + "обязано жить в обеих. Иначе труба перетаскивается на стык, но не "
                + "растягивается до него");
            Assert.AreEqual(0.012f, gap, 1e-5f,
                "тянуть остаётся ровно до чужого устья: 12 мм вдоль нормали грани");
        }

        [Test]
        public void ResizeSnap_AMouthOffToTheSide_IsNotAStopForThisFace()
        {
            var self = Boxed("Run", Vector3.zero, new SnapPort(new Vector3(0f, 0.05f, 0f), Vector3.up));
            var other = Boxed("Fit", new Vector3(0.3f, 0.2f, 0f),
                new SnapPort(new Vector3(0.3f, 0.062f, 0f), Vector3.down));

            bool found = ResizeSnap.SnapDelta(new Vector3(0f, 0.05f, 0f), Vector3.up,
                Vector3.right, Vector3.forward, new Vector2(0.1f, 0.1f),
                new List<ElementGeometry> { other }, self, Threshold, out _);

            Assert.IsFalse(found,
                "отрицательный контроль к тесту выше: устье, отстоящее на 300 мм ПОПЕРЁК "
                + "оси, растягиванием не достаётся — вдоль нормали его не догнать. Без "
                + "этой пары первый тест был бы зелён и на правиле «любое чужое устье "
                + "останавливает любую грань»");
        }
    }
}
