using KitchenDesigner.Core;
using NUnit.Framework;

namespace KitchenDesigner.Tests.Pure
{
    /// <summary>Шлюз открытия проекта: спросить пользователя ровно тогда, когда
    /// файл сделан БОЛЕЕ НОВОЙ версией, и не спрашивать во всех остальных
    /// случаях. Противоположный вход тут несущий: файл без поля версии — это весь
    /// сегодняшний парк сохранений, и он обязан открываться молча.
    ///
    /// Второе важное: «Отмена» обязана НЕ открыть. Диалог тут заменён счётчиком,
    /// потому что вопрос не в кнопках, а в том, что открытие происходит только
    /// через переданное продолжение — иначе окно превратилось бы в украшение
    /// поверх уже случившейся подмены.</summary>
    public class NewerVersionPromptTests
    {
        private int _opened;
        private string _asked = "";

        [SetUp]
        public void SetUp()
        {
            _opened = 0;
            _asked = "";
            NewerVersionPrompt.Show = null;
        }

        [TearDown]
        public void TearDown() => NewerVersionPrompt.Show = null;

        [Test]
        public void AFileFromAnOlderBuild_OpensWithoutAsking()
        {
            NewerVersionPrompt.Show = (message, open) => { _asked = message; open(); };

            NewerVersionPrompt.Confirm("0.1500", "0.1611", () => _opened++);

            Assert.AreEqual(1, _opened);
            Assert.AreEqual("", _asked, "вопроса быть не должно");
        }

        [Test]
        public void AFileWithoutTheVersionField_OpensWithoutAsking()
        {
            NewerVersionPrompt.Show = (message, open) => { _asked = message; open(); };

            NewerVersionPrompt.Confirm("", "0.1611", () => _opened++);

            Assert.AreEqual(1, _opened);
            Assert.AreEqual("", _asked);
        }

        [Test]
        public void AFileFromANewerBuild_AsksAndNamesBothVersions()
        {
            NewerVersionPrompt.Show = (message, open) => _asked = message;

            NewerVersionPrompt.Confirm("0.1700", "0.1611", () => _opened++);

            Assert.AreEqual(0, _opened, "пока пользователь не ответил, проект не открывается");
            StringAssert.Contains("0.1700", _asked);
            StringAssert.Contains("0.1611", _asked);
            StringAssert.Contains("нарушением", _asked,
                "предупреждение обязано сказать, ЧТО будет с незнакомыми объектами");
        }

        [Test]
        public void TheAnswerOpen_RunsTheOpening()
        {
            NewerVersionPrompt.Show = (message, open) => open();

            NewerVersionPrompt.Confirm("0.1700", "0.1611", () => _opened++);

            Assert.AreEqual(1, _opened);
        }

        [Test]
        public void TheAnswerCancel_LeavesTheProjectUnopened()
        {
            NewerVersionPrompt.Show = (message, open) => { };

            NewerVersionPrompt.Confirm("0.1700", "0.1611", () => _opened++);

            Assert.AreEqual(0, _opened);
        }

        [Test]
        public void WithNoDialogAvailable_TheProjectStillOpens()
        {
            NewerVersionPrompt.Confirm("0.1700", "0.1611", () => _opened++);

            Assert.AreEqual(1, _opened,
                "шлюз без окна не имеет права стать глухой стеной: batch-прогон, "
                + "автозагрузка последнего проекта и тесты живут без UI");
        }
    }
}
