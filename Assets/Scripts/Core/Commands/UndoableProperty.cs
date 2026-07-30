using System;

namespace KitchenDesigner.Core
{
    /// <summary>
    /// Свойство элемента, которое правит пользователь и которое ОБЯЗАНО
    /// откатываться кнопками «Отменить»/«Повторить».
    ///
    /// Пометка — не документация, а рабочий механизм: окно свойств снимает
    /// значения всех помеченных свойств до и после применения (см.
    /// <see cref="UndoableProperties.Capture"/>) и само кладёт разницу в стек
    /// команд. Новое свойство достаточно пометить — undo для него появляется
    /// без единой строки в UI.
    ///
    /// Полнота пометок — это тест: <c>UndoableCoverageTests</c> валит сборку,
    /// если у публичного свойства элемента нет ни <see cref="UndoableAttribute"/>,
    /// ни <see cref="NotUndoableAttribute"/>. Забыть невозможно.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, Inherited = true)]
    public sealed class UndoableAttribute : Attribute
    {
        /// <summary>Порядок записи при откате: меньший пишется раньше. Нужен там,
        /// где одно свойство клампится по другому — габарит детали (−100) идёт
        /// прежде выреза варочной, иначе вырез подрежется по старой плите.</summary>
        public int Order { get; set; }
    }

    /// <summary>
    /// Свойство сознательно НЕ участвует в общем снимке undo. Причина
    /// обязательна: либо у свойства своя команда (пазы, накладки, материал),
    /// либо это служебное состояние, которое не является правкой документа
    /// (открыта ли дверца, к какой стене прилипло окно).
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, Inherited = true)]
    public sealed class NotUndoableAttribute : Attribute
    {
        public string Reason { get; }

        public NotUndoableAttribute(string reason)
        {
            Reason = reason;
        }
    }
}
