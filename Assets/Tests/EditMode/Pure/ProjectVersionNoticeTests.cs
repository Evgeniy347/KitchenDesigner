using KitchenDesigner.Core;
using NUnit.Framework;

namespace KitchenDesigner.Tests.Pure
{
    /// <summary>Предупреждение «проект новее программы» показывается ровно в одном
    /// случае, и противоположный вход тут важнее прямого: ВЕСЬ сегодняшний парк
    /// файлов поля версии не имеет вовсе, и такой файл обязан открываться молча,
    /// как раньше. Иначе первая же встреча пользователя с новой версией — окно с
    /// вопросом на каждом старом проекте.
    ///
    /// Номер версии тут тот же, которым живут сборка и инсталлятор
    /// (`BuildInfo.Version`, счётчик коммитов — «0.1611»), и сравнивает их тот же
    /// `VersionUtil`, что и обновлятор: второй нумерации и второго компаратора в
    /// продукте быть не должно. Проверяется здесь РЕШЕНИЕ, а не разбор строки —
    /// разбор стережёт `VersionUtilTests`.</summary>
    public class ProjectVersionNoticeTests
    {
        [Test]
        public void AFileFromANewerBuild_IsNewer()
        {
            Assert.IsTrue(ProjectVersionNotice.FileIsNewerThanApp("0.1700", "0.1611"));
        }

        [Test]
        public void AFileFromTheSameOrAnOlderBuild_IsNot()
        {
            Assert.IsFalse(ProjectVersionNotice.FileIsNewerThanApp("0.1611", "0.1611"));
            Assert.IsFalse(ProjectVersionNotice.FileIsNewerThanApp("0.1500", "0.1611"));
        }

        [Test]
        public void AFileWithoutTheVersionField_IsNeverNewer()
        {
            Assert.IsFalse(ProjectVersionNotice.FileIsNewerThanApp("", "0.1611"),
                "все существующие сохранения такие — предупреждения быть не должно");
            Assert.IsFalse(ProjectVersionNotice.FileIsNewerThanApp(null, "0.1611"));
        }

        [Test]
        public void AVersionThatIsNotANumber_IsNeverNewer()
        {
            Assert.IsFalse(ProjectVersionNotice.FileIsNewerThanApp("dev", "0.1611"));
        }

        [Test]
        public void SegmentsAreComparedAsNumbers_NotAsText()
        {
            Assert.IsTrue(ProjectVersionNotice.FileIsNewerThanApp("0.1611", "0.999"),
                "строковое сравнение поставило бы 999 выше 1611");
            Assert.IsFalse(ProjectVersionNotice.FileIsNewerThanApp("0.999", "0.1611"));
        }

        [Test]
        public void AMissingThirdSegment_CountsAsZero()
        {
            Assert.IsTrue(ProjectVersionNotice.FileIsNewerThanApp("2.4.1", "2.4"));
            Assert.IsFalse(ProjectVersionNotice.FileIsNewerThanApp("2.4", "2.4.1"));
        }
    }
}
