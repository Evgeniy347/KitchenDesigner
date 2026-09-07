using NUnit.Framework;
using KitchenDesigner.Core;

namespace KitchenDesigner.Tests.Geometry
{
    /// <summary>Три состояния ПРИСУТСТВИЯ кромки на торце и цикл, по которому их
    /// перещёлкивает клик по полосе в контекстном меню.
    ///
    /// Почему три, а не флаг «ручная сторона»: старый бит значил «не ругайся
    /// правилом EDG-01» и на раскрой не влиял вовсе — жёлтая полоса врала,
    /// потому что читалась как «кромка тут есть». Теперь состояние отвечает
    /// ровно на вопрос спецификации: есть на этой стороне кромка или нет.
    /// `Forced` — есть по решению человека, `Suppressed` — нет по решению
    /// человека, `Auto` — как посчитает сцена. Оба ЯВНЫХ состояния молчаливо
    /// снимают EDG-01: правило существует потому, что перекрытие торца
    /// неоднозначно, а явный выбор человека неоднозначность снимает.</summary>
    public class EdgeStatesTests
    {
        [Test]
        public void EdgeStates_Of_SuppressedBitWins_OverForcedBit()
        {
            var state = EdgeStates.Of(EdgeManual.AllMask, EdgeManual.Bit(EdgeSide.W1), EdgeSide.W1);

            Assert.AreEqual(EdgeSideState.Suppressed, state,
                "две маски физически могут пересечься (битый файл, чужая правка JSON); "
                + "читатель обязан дать ОДИН ответ, а не зависеть от порядка проверок");
        }

        [Test]
        public void EdgeStates_Next_CyclesAutoForcedSuppressedAndBack()
        {
            Assert.AreEqual(EdgeSideState.Forced, EdgeStates.Next(EdgeSideState.Auto),
                "первый клик по стороне ДОБАВЛЯЕТ кромку: так было до передела (жёлтый с первого "
                + "клика), и разворачивать эту мышечную память без нужды незачем");
            Assert.AreEqual(EdgeSideState.Suppressed, EdgeStates.Next(EdgeSideState.Forced),
                "порядок цикла решён владельцем: авто → есть → убрать → и по кругу; он читается "
                + "как «добавить → убрать → вернуть автомат»");
            Assert.AreEqual(EdgeSideState.Auto, EdgeStates.Next(EdgeSideState.Suppressed),
                "цикл замкнут: три клика по полосе возвращают сторону туда, откуда начали");
        }

        [Test]
        public void EdgeStates_Next_ThreeClicks_ReturnToTheStart()
        {
            foreach (var start in new[]
                { EdgeSideState.Auto, EdgeSideState.Forced, EdgeSideState.Suppressed })
                Assert.AreEqual(start,
                    EdgeStates.Next(EdgeStates.Next(EdgeStates.Next(start))),
                    $"{start}: цикл из трёх состояний, а не из двух");
        }

        [Test]
        public void EdgeStates_HasEdge_ExplicitStates_IgnoreTheScene()
        {
            foreach (bool auto in new[] { true, false })
            {
                Assert.IsTrue(EdgeStates.HasEdge(EdgeSideState.Forced, auto),
                    "«принудительно есть» ставит кромку и на закрытый торец");
                Assert.IsFalse(EdgeStates.HasEdge(EdgeSideState.Suppressed, auto),
                    "«убрать» снимает кромку и с открытого торца — это и есть красная сторона, "
                    + "которая даёт в спецификации на одну кромку меньше");
            }
        }

        [Test]
        public void EdgeStates_HasEdge_Auto_RepeatsTheScene()
        {
            Assert.IsTrue(EdgeStates.HasEdge(EdgeSideState.Auto, true),
                "в «авто» состояние не добавляет ничего от себя — отвечает расчёт по сцене");
            Assert.IsFalse(EdgeStates.HasEdge(EdgeSideState.Auto, false),
                "закрытый торец в «авто» кромки не получает — так было и до трёх состояний");
        }

        [Test]
        public void EdgeStates_IsExplicit_TrueForBothHumanDecisions()
        {
            Assert.IsFalse(EdgeStates.IsExplicit(EdgeSideState.Auto),
                "«авто» — это отсутствие решения человека, и EDG-01 обязан его разбирать");
            Assert.IsTrue(EdgeStates.IsExplicit(EdgeSideState.Forced),
                "жёлтая сторона молчала по EDG-01 и до передела — это поведение сохранено");
            Assert.IsTrue(EdgeStates.IsExplicit(EdgeSideState.Suppressed),
                "EDG-01 молчит на ОБОИХ явных состояниях: правило ловит неоднозначность, "
                + "а человек её уже снял");
        }

        [Test]
        public void EdgeStates_MaskWith_MovesTheBitBetweenTheTwoMasks()
        {
            int forced = EdgeManual.Bit(EdgeSide.L1);
            int suppressed = 0;

            forced = EdgeStates.ForcedMaskWith(forced, EdgeSide.L1, EdgeSideState.Suppressed);
            suppressed = EdgeStates.SuppressedMaskWith(suppressed, EdgeSide.L1, EdgeSideState.Suppressed);

            Assert.AreEqual(0, forced, "сторона не может быть одновременно «есть» и «убрать»");
            Assert.AreEqual(EdgeManual.Bit(EdgeSide.L1), suppressed,
                "бит не потерялся по дороге: он переехал во вторую маску");
        }
    }
}
