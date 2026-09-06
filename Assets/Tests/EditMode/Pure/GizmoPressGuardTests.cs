using NUnit.Framework;
using KitchenDesigner.Core;

namespace KitchenDesigner.Tests.Pure
{
    /// <summary>Одно правило «клик достался гизмо, деталь его не получает» на двух
    /// потребителей. SelectionManager проверял ручку ресайза, а ElementMover — нет:
    /// у него порядок 0, у менеджера ручек 100, поэтому на кадре нажатия
    /// TryBeginPress всегда успевал взвести _pressed на детали под лучом. Обычно
    /// это гасил флаг IsResizing, но при отказе BeginDrag (грань вне диапазона,
    /// нетрансформируемый объект) начиналось перетаскивание ЧУЖОЙ детали.</summary>
    public class GizmoPressGuardTests
    {
        [Test]
        public void PointerOverResizeHandle_BlocksThePress()
        {
            Assert.IsTrue(GizmoPressGuard.BlocksPress(
                resizing: false, pointerOverResizeHandle: true,
                overlayActive: false, pointerOverOverlayHandle: false),
                "курсор над ручкой ресайза — нажатие принадлежит ручке, а не детали "
                + "под ней: иначе луч уходит СКВОЗЬ прозрачную стрелку в соседа");
        }

        [Test]
        public void Resizing_BlocksThePress_EvenAwayFromEveryHandle()
        {
            Assert.IsTrue(GizmoPressGuard.BlocksPress(
                resizing: true, pointerOverResizeHandle: false,
                overlayActive: false, pointerOverOverlayHandle: false),
                "во время тяги курсор давно ушёл с ручки, но жест продолжается");
        }

        [Test]
        public void OverlayHandle_BlocksOnlyWhileTheOverlayIsBeingEdited()
        {
            Assert.IsTrue(GizmoPressGuard.BlocksPress(false, false,
                overlayActive: true, pointerOverOverlayHandle: true));
            Assert.IsFalse(GizmoPressGuard.BlocksPress(false, false,
                overlayActive: false, pointerOverOverlayHandle: true),
                "правка области не идёт — ручек накладки в сцене нет, и «курсор над "
                + "ручкой» тут означало бы блокировку по призраку прошлого кадра");
        }

        [Test]
        public void NothingUnderTheCursor_LetsThePressThrough()
        {
            Assert.IsFalse(GizmoPressGuard.BlocksPress(false, false, false, false),
                "положительный контроль: без этой строки предикат мог бы всегда "
                + "запрещать нажатие и все остальные проверки остались бы зелёными");
        }
    }
}
