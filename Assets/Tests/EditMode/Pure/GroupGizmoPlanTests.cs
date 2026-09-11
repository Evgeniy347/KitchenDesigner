using NUnit.Framework;
using KitchenDesigner.Core;

namespace KitchenDesigner.Tests.Pure
{
    /// <summary>Что рисует гизмо при выделении: одна деталь — прежние ручки своего
    /// режима, две и больше — ОБЩИЙ габарит и стрелки переноса. Противоположные
    /// входы здесь несущие: правило «стрелки всегда» проверяется только тем, что
    /// режим растяжения ВКЛЮЧЁН, а стрелки всё равно требуются.</summary>
    public class GroupGizmoPlanTests
    {
        [Test]
        public void For_OneElement_KeepsTheHandlesOfTheCurrentMode_AndDrawsNoGroupBox()
        {
            var resize = GroupGizmoPlan.For(selectedCount: 1, resizeMode: true);
            Assert.IsTrue(resize.PerElementResizeHandles,
                "одна деталь в режиме растяжения — ручки прежние, задача про группу "
                + "не имеет права трогать одиночное выделение");
            Assert.IsFalse(resize.PerElementMoveHandles,
                "режим один, и ручки одного вида: две стрелки на одной грани неразличимы");
            Assert.IsFalse(resize.GroupOutline,
                "общий габарит на одной детали — это её же габарит, нарисованный вторым "
                + "контуром поверх первого");
            Assert.IsFalse(resize.GroupMoveArrows,
                "и стрелок группы на одиночном выделении нет — иначе их было бы шесть лишних");

            var move = GroupGizmoPlan.For(selectedCount: 1, resizeMode: false);
            Assert.IsTrue(move.PerElementMoveHandles,
                "положительный контроль: без него режим мог бы вообще не доходить до плана");
            Assert.IsFalse(move.PerElementResizeHandles,
                "в режиме переноса ручек растяжения у детали нет");
        }

        [Test]
        public void For_TwoElements_DrawsTheGroupBoxWithMoveArrows_EvenInResizeMode()
        {
            var inResizeMode = GroupGizmoPlan.For(selectedCount: 2, resizeMode: true);
            Assert.IsTrue(inResizeMode.GroupOutline,
                "два объекта — общий габарит на всю группу");
            Assert.IsTrue(inResizeMode.GroupMoveArrows,
                "стрелки переноса даются ВСЕГДА, независимо от режима: растягивать "
                + "группу нечем, а двигать её нужно");
            Assert.IsFalse(inResizeMode.PerElementResizeHandles,
                "ручки одной детали при групповом выделении не строятся — иначе клик "
                + "по ним растянул бы одного члена группы вместо переноса всей");
            Assert.IsFalse(inResizeMode.PerElementMoveHandles,
                "и ручек переноса одной детали тоже нет — переносится группа целиком");
        }

        [Test]
        public void For_TwoOrMore_ModeDoesNotChangeTheGroupHalfOfThePlan()
        {
            var inResizeMode = GroupGizmoPlan.For(selectedCount: 5, resizeMode: true);
            var inMoveMode = GroupGizmoPlan.For(selectedCount: 5, resizeMode: false);

            Assert.AreEqual(inMoveMode.GroupOutline, inResizeMode.GroupOutline,
                "габарит группы от режима не зависит");
            Assert.AreEqual(inMoveMode.GroupMoveArrows, inResizeMode.GroupMoveArrows,
                "и стрелки группы от режима не зависят");
            Assert.IsTrue(inMoveMode.GroupMoveArrows,
                "обе половины равны И обе истинны — равенство двух «нет» было бы зелёным ни о чём");
        }

        [Test]
        public void For_NothingSelected_DrawsNothingAtAll()
        {
            var plan = GroupGizmoPlan.For(selectedCount: 0, resizeMode: true);
            Assert.IsFalse(plan.GroupOutline,
                "пустое выделение: габарит не от чего считать");
            Assert.IsFalse(plan.GroupMoveArrows,
                "пустое выделение: двигать нечего");
            Assert.IsFalse(plan.PerElementResizeHandles,
                "пустое выделение: растягивать нечего");
            Assert.IsFalse(plan.PerElementMoveHandles,
                "пустое выделение: ручек переноса тоже нет");
        }
    }
}
