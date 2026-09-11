using KitchenDesigner.Core;
using NUnit.Framework;

namespace KitchenDesigner.Tests.Pure
{
    /// <summary>Как СТАРАЯ версия узнаёт, что перед ней объект, которого она не
    /// знает. Флаги `isXxx` тут не помощники: у неизвестного типа флаг называется
    /// именем, которого в `ElementData` этой версии нет, и `JsonUtility` его
    /// выбрасывает — остаётся запись, неотличимая от обычной детали. Отличает их
    /// только строка типа: она есть у каждого сохранённого объекта, и если
    /// восстановитель дошёл до замыкающей записи (обычная деталь), а в файле
    /// написано что-то другое — тип неизвестен.</summary>
    public class UnknownElementTypeTests
    {
        [Test]
        public void ATypeThisVersionHasNoRestorerFor_IsUnknown()
        {
            Assert.IsTrue(UnknownElementType.IsUnknown("washer", restoredAsPlainBoard: true));
        }

        [Test]
        public void APlainBoard_IsNotUnknown()
        {
            Assert.IsFalse(UnknownElementType.IsUnknown(UnknownElementType.PlainBoard,
                restoredAsPlainBoard: true));
        }

        [Test]
        public void AFileWithoutTheTypeField_IsNotUnknown()
        {
            Assert.IsFalse(UnknownElementType.IsUnknown("", restoredAsPlainBoard: true),
                "старые сохранения строки типа не несут — они не нарушение");
            Assert.IsFalse(UnknownElementType.IsUnknown(null, restoredAsPlainBoard: true));
        }

        [Test]
        public void ATypeThatWasRestoredByItsOwnRestorer_IsNotUnknown()
        {
            Assert.IsFalse(UnknownElementType.IsUnknown("dishwasher", restoredAsPlainBoard: false));
        }
    }
}
