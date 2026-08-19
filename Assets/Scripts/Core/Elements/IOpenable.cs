namespace KitchenDesigner.Core
{
    /// <summary>
    /// Элемент с «дверным» состоянием, которое можно переключить одним нажатием —
    /// дверь, окно, фасад, ящик, духовка, посудомойка. Объединяет всё, что
    /// откликается на «E» в сцене и на кнопку «Открыть/закрыть» в контекстном
    /// меню фасада, чтобы не разводить по вызовам копию
    /// <c>is DoorElement / WindowElement / …</c>.
    ///
    /// Сдвоенные ящики (<see cref="DrawerElement.CycleDoubleState"/>) — НЕ часть
    /// контракта: это приватная семантика самого <see cref="DrawerElement"/>, и
    /// вызывающий код обязан её знать (один <c>is DrawerElement</c> в
    /// <c>CameraController.ToggleSelectedOpenables</c> и его напарнике в
    /// <c>ContextMenuUI</c>).
    /// </summary>
    public interface IOpenable
    {
        void ToggleOpen();
    }
}
